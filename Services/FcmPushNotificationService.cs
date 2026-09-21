using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using NotificationMicroservice.Data;

namespace NotificationMicroservice.Services
{
    public class FcmPushNotificationService(
        AppDbContext dbContext,
        ILogger<FcmPushNotificationService> logger
    ) : IPushNotificationService
    {
        private readonly AppDbContext _dbContext = dbContext;
        private readonly ILogger<FcmPushNotificationService> _logger = logger;

        public async Task SendAsync(
            Guid recipientUserId,
            Guid actorUserId,
            string type,
            Guid entityId
        )
        {
            // No try/catch here on purpose: if the database or FCM fails, the exception must reach
            // MassTransit so the retry policy can run instead of the notification being dropped.
            var devices = await _dbContext
                .UserDeviceTokens.Where(d => d.UserId == recipientUserId && d.IsActive)
                .Select(d => new { d.Id, d.DeviceToken })
                .ToListAsync();

            if (devices.Count == 0)
            {
                _logger.LogWarning(
                    "No active device tokens found in database for user {UserId}. Skipping FCM push.",
                    recipientUserId
                );
                return;
            }

#pragma warning disable CS0618
            var titleText = type.Contains("CommentLike") ? "New Like!" : "New Notification";
            var bodyText = type.Contains("CommentLike")
                ? "Someone liked your comment."
                : "You have a new update.";

            var messages = devices
                .Select(device => new Message
                {
                    Token = device.DeviceToken,
                    Notification = new FirebaseAdmin.Messaging.Notification
                    {
                        Title = titleText,
                        Body = bodyText,
                    },
                    Data = new Dictionary<string, string>
                    {
                        { "type", type },
                        { "entityId", entityId.ToString() },
                        { "title", titleText },
                        { "body", bodyText },
                    },
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High,
                        Notification = new AndroidNotification
                        {
                            Priority = NotificationPriority.HIGH,
                            DefaultSound = true,
                            DefaultVibrateTimings = true,
                        },
                    },
                })
                .ToList();
#pragma warning restore CS0618

            var response = await FirebaseMessaging.DefaultInstance.SendEachAsync(messages);

            var failures = new List<Exception>();
            for (var i = 0; i < response.Responses.Count; i++)
            {
                var sendResponse = response.Responses[i];
                if (sendResponse.IsSuccess)
                {
                    continue;
                }

                // A token that is no longer registered will never succeed: skip it instead of
                // retrying, and don't poison the whole message because of it.
                if (sendResponse.Exception?.MessagingErrorCode
                    is MessagingErrorCode.Unregistered
                        or MessagingErrorCode.InvalidArgument
                        or MessagingErrorCode.SenderIdMismatch)
                {
                    _logger.LogWarning(
                        "FCM token of device {DeviceId} (user {UserId}) is no longer valid; skipping it.",
                        devices[i].Id,
                        recipientUserId
                    );
                    continue;
                }

                failures.Add(
                    (Exception?)sendResponse.Exception
                        ?? new Exception("FCM send failed without providing an exception.")
                );
            }

            if (failures.Count > 0)
            {
                throw new Exception(
                    $"FCM push failed for {failures.Count} of {devices.Count} device(s) of user {recipientUserId}.",
                    failures[0]
                );
            }

            _logger.LogInformation(
                "FCM Push result for user {UserId}: {SuccessCount} succeeded out of {TotalCount}",
                recipientUserId,
                response.SuccessCount,
                devices.Count
            );
        }
    }
}
