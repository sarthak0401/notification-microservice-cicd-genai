using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationMicroservice.Contracts;
using NotificationMicroservice.Data;
using NotificationMicroservice.DTOs;
using NotificationMicroservice.Services;

namespace NotificationMicroservice.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController(
        IDeviceTokenService deviceTokenService,
        INotificationService notificationService,
        IPublishEndpoint publishEndpoint,
        AppDbContext dbContext
    ) : ControllerBase
    {
        private readonly IDeviceTokenService _deviceTokenService = deviceTokenService;
        private readonly INotificationService _notificationService = notificationService;
        private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
        private readonly AppDbContext _dbContext = dbContext;

        [Authorize]
        [HttpPost("registerDeviceToken")]
        public async Task<ActionResult> RegisterDeviceToken([FromBody] RegisterDeviceTokenDTO dto)
        {
            var userRowId = User.FindFirst("UserRowId")?.Value ?? string.Empty;

            var res = await _deviceTokenService.RegisterDeviceToken(userRowId, dto);
            return StatusCode(res.Status, res);
        }

        [Authorize]
        [HttpGet("get/deviceTokens")]
        public async Task<ActionResult> GetDeviceTokensByUserId()
        {
            var userRowId = User.FindFirst("UserRowId")?.Value ?? string.Empty;

            var res = await _deviceTokenService.GetMyDeviceTokens(userRowId);
            return StatusCode(res.Status, res);
        }

        [Authorize]
        [HttpDelete("del/deviceToken")]
        public async Task<ActionResult> DeleteDeviceToken([FromQuery] string DeviceToken)
        {
            var userRowId = User.FindFirst("UserRowId")?.Value ?? string.Empty;

            var res = await _deviceTokenService.RemoveDeviceToken(userRowId, DeviceToken);
            return StatusCode(res.Status, res);
        }

        [Authorize]
        [HttpPost("sendTestPush")]
        public async Task<ActionResult> SendTestPush([FromQuery] Guid? recipientUserId)
        {
            var userRowId = User.FindFirst("UserRowId")?.Value;
            if (!Guid.TryParse(userRowId, out var actorGuid))
            {
                return Unauthorized(new { Success = false, Message = "Invalid user token." });
            }

            var target = recipientUserId ?? actorGuid;

            var activeTokens = await _dbContext.UserDeviceTokens
                .Where(d => d.UserId == target && d.IsActive)
                .Select(d => new { d.DeviceId, d.DevicePlatform, d.DeviceModel, d.UpdatedAt })
                .ToListAsync();

            if (activeTokens.Count == 0)
            {
                return NotFound(
                    new
                    {
                        Success = false,
                        Message = $"No active device tokens found for recipient user {target}. Please register a device token first via /api/Notification/registerDeviceToken.",
                        RecipientUserId = target
                    }
                );
            }

            await _notificationService.CreateAndSendAsync(
                target,
                actorGuid,
                "Test",
                Guid.NewGuid()
            );

            return Ok(
                new
                {
                    Success = true,
                    Message = $"Test push notification dispatched to {activeTokens.Count} active device(s) for recipient user {target}.",
                    RecipientUserId = target,
                    ActiveDevices = activeTokens
                }
            );
        }

        // Endpoint to test publishing a RabbitMQ message directly from Swagger!
        [HttpPost("publishTestEvent")]
        public async Task<ActionResult> PublishTestEvent(
            [FromQuery] Guid recipientUserId,
            [FromQuery] string type = "CommentLike"
        )
        {
            await _publishEndpoint.Publish(new PushNotificationEvent
            {
                RecipientUserId = recipientUserId,
                ActorUserId = Guid.NewGuid(),
                Type = type,
                EntityId = Guid.NewGuid()
            });

            return Ok(new
            {
                Success = true,
                Message = $"Published PushNotificationEvent for recipient {recipientUserId} to RabbitMQ."
            });
        }

        [AllowAnonymous]
        [HttpPost("test-raw-token")]
        public async Task<ActionResult> TestRawToken(
            [FromQuery] string deviceToken,
            [FromQuery] string? channelId = null,
            [FromQuery] string? title = null,
            [FromQuery] string? body = null
        )
        {
            try
            {
                if (string.IsNullOrWhiteSpace(deviceToken))
                {
                    return BadRequest(new { Success = false, Error = "deviceToken cannot be empty." });
                }

                if (FirebaseAdmin.FirebaseApp.DefaultInstance == null)
                {
                    return StatusCode(500, new { Success = false, Error = "FirebaseApp is not initialized. Ensure firebase-key.json is present and valid." });
                }

                var resolvedTitle = string.IsNullOrWhiteSpace(title) ? "Backend Test!" : title;
                var resolvedBody = string.IsNullOrWhiteSpace(body) ? "If you see this, the .NET backend is working perfectly." : body;

#pragma warning disable CS0618
                var message = new FirebaseAdmin.Messaging.Message
                {
                    Token = deviceToken,
                    Notification = new FirebaseAdmin.Messaging.Notification
                    {
                        Title = resolvedTitle,
                        Body = resolvedBody
                    },
                    Data = new Dictionary<string, string>
                    {
                        { "title", resolvedTitle },
                        { "body", resolvedBody },
                        { "type", "Test" }
                    },
                    Android = new FirebaseAdmin.Messaging.AndroidConfig
                    {
                        Priority = FirebaseAdmin.Messaging.Priority.High,
                        Notification = new FirebaseAdmin.Messaging.AndroidNotification
                        {
                            Priority = FirebaseAdmin.Messaging.NotificationPriority.HIGH,
                            DefaultSound = true,
                            DefaultVibrateTimings = true,
                            ChannelId = channelId
                        }
                    }
                };
#pragma warning restore CS0618

                var response = await FirebaseAdmin.Messaging.FirebaseMessaging.DefaultInstance.SendAsync(message);
                return Ok(new { Success = true, MessageId = response });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Error = ex.Message });
            }
        }
    }
}
