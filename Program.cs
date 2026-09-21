using System.IdentityModel.Tokens.Jwt;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NotificationMicroservice.Data;
using NotificationMicroservice.Consumers;
using NotificationMicroservice.Services;
using Serilog;
using NotificationMicroservice.Configuration;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

var keyPath = builder.Configuration["Firebase:CredentialFilePath"] ?? "firebase-key.json";
if (!Path.IsPathRooted(keyPath))
{
    keyPath = Path.Combine(builder.Environment.ContentRootPath, keyPath);
}

if (File.Exists(keyPath) && FirebaseApp.DefaultInstance == null)
{
    try
    {
        var credential = CredentialFactory.FromFile<ServiceAccountCredential>(keyPath).ToGoogleCredential();
        FirebaseApp.Create(new AppOptions
        {
            Credential = credential
        });
        Log.Information("FirebaseApp initialized successfully with credentials from: {KeyPath}", keyPath);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to initialize FirebaseApp from: {KeyPath}", keyPath);
    }
}
else if (!File.Exists(keyPath))
{
    Log.Warning("Firebase credential file not found at: {KeyPath}", keyPath);
}

// Add controllers
builder.Services.AddControllers();

// Database Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("db"))
);

// Register Services
builder.Services.AddScoped<IPushNotificationService, FcmPushNotificationService>();
builder.Services.AddScoped<IDeviceTokenService, DeviceTokenServiceImpl>();
builder.Services.AddScoped<INotificationService, NotificationServiceImpl>();

// Configure MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PushNotificationEventConsumer>();
    x.AddConsumer<SendEmailEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("push_notifications_queue", e =>
        {
            // Retry transient failures 5x with growing delay. When the attempts are used up
            // MassTransit moves the message to "push_notifications_queue_error" - the DLQ.
            e.UseMessageRetry(r =>
                r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(3))
            );

            e.ConfigureConsumer<PushNotificationEventConsumer>(context);
        });


        cfg.ReceiveEndpoint("send_email_queue", e =>
        {
            e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(3)));

            e.ConfigureConsumer<SendEmailEventConsumer>(context);
        });
    });
});

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// Bypasses signature validation for local unsigned test tokens
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = authHeader["Bearer ".Length..].Trim();
                    var handler = new JwtSecurityTokenHandler();
                    if (handler.CanReadToken(token))
                    {
                        var jwt = handler.ReadJwtToken(token);
                        context.Principal = new System.Security.Claims.ClaimsPrincipal(
                            new System.Security.Claims.ClaimsIdentity(jwt.Claims, "Bearer")
                        );
                        context.Success();
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

// Swagger Configuration with JWT Bearer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Notification Microservice API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your JWT token here"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
