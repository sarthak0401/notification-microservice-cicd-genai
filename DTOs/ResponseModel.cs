namespace NotificationMicroservice.DTOs
{
    public class ResponseModel
    {
        public string? Message { get; set; }
        public string? Type { get; set; }
        public bool Success { get; set; }
        public int Status { get; set; }
        public object? Data { get; set; }
    }
}
