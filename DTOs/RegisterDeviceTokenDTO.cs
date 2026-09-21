namespace NotificationMicroservice.DTOs
{
    public class RegisterDeviceTokenDTO
    {
        public required string DeviceId { get; set; }
        public required string DeviceToken { get; set; }
        public string? DeviceModel { get; set; }
        public string? DevicePlatform { get; set; } // Android/Ios/Web
    }
}
