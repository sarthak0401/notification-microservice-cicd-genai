using System.ComponentModel.DataAnnotations;

namespace NotificationMicroservice.Models
{
    public class UserDeviceToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        [StringLength(255)]
        public required string DeviceId { get; set; }

        [StringLength(450)]
        public required string DeviceToken { get; set; }

        [StringLength(50)]
        public string? DevicePlatform { get; set; }

        [StringLength(100)]
        public string? DeviceModel { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
