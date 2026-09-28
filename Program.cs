using System.Text;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NotificationMicroservice.Configuration;
using NotificationMicroservice.Consumers;
using NotificationMicroservice.Data;
using NotificationMicroservice.Services;
using Serilog;

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



var rabbitHost = builder.Configuration["RabbitMQ:Host"];
var rabbitUser = builder.Configuration["RabbitMQ:Username"];
var rabbitPass = builder.Configuration["RabbitMQ:Password"];

// Configure MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PushNotificationEventConsumer>();
    x.AddConsumer<SendEmailEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser!);
            h.Password(rabbitPass!);
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

// JWT: this service is a resource server. It VALIDATES tokens issued by the identity service
// and never issues them. Locally the identity service is simulated by
// scripts/generate-dev-token.py, which signs with the same Jwt:Key below.
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtOptions>(jwtSection);

var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Key) || Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32)
{
    Log.Warning(
        "Jwt:Key is missing or shorter than 32 bytes. HS256 token validation will reject every request."
    );
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names exactly as they appear in the token (no legacy inbound remapping).
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "UserRowId",
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

app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "Healthy",
        service = "NotificationMicroservice"
    });
});

app.Run();
