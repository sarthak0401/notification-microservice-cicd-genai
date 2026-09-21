using NotificationMicroservice.DTOs;

namespace NotificationMicroservice.Services
{
    public interface IDeviceTokenService
    {
        Task<ResponseModel> RegisterDeviceToken(string userRowId, RegisterDeviceTokenDTO register);
        Task<ResponseModel> RemoveDeviceToken(string userRowId, string deviceToken);
        Task<ResponseModel> GetMyDeviceTokens(string userRowId);
    }
}
