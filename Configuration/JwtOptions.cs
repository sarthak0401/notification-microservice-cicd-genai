namespace NotificationMicroservice.Configuration
{
    public class JwtOptions
    {
        public string Issuer { get; set; } = "NotificationMicroservice";
        public string Audience { get; set; } = "NotificationMicroserviceClient";

        // HS256 shared secret. In production this microservice would validate RS256 tokens
        // against the identity provider's JWKS endpoint instead of holding a symmetric key.
        public string Key { get; set; } = string.Empty;

        public int ExpiryMinutes { get; set; } = 60;
    }
}
