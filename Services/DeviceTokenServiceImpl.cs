using Microsoft.EntityFrameworkCore;
using NotificationMicroservice.Data;
using NotificationMicroservice.DTOs;
using NotificationMicroservice.Models;

namespace NotificationMicroservice.Services
{
    public class DeviceTokenServiceImpl(
        AppDbContext dbcontext,
        ILogger<DeviceTokenServiceImpl> logger
    ) : IDeviceTokenService
    {
        public readonly AppDbContext _dbcontext = dbcontext;
        public readonly ILogger<DeviceTokenServiceImpl> _logger = logger;

        public async Task<ResponseModel> GetMyDeviceTokens(string userRowId)
        {
            try
            {
                var userGuid = Guid.Parse(userRowId);

                var devices = await _dbcontext
                    .UserDeviceTokens.Where(x => x.UserId == userGuid && x.IsActive)
                    .Select(x => new
                    {
                        x.Id,
                        x.DeviceId,
                        x.DeviceToken,
                        x.DevicePlatform,
                        x.DeviceModel,
                        x.UpdatedAt,
                    })
                    .ToListAsync();

                return new ResponseModel
                {
                    Message = "Device tokens retrieved successfully.",
                    Success = true,
                    Type = "Success",
                    Status = 200,
                    Data = devices,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retrieving user device tokens.");
                return new ResponseModel
                {
                    Message = "Internal Server Error",
                    Success = false,
                    Type = "Fail",
                    Status = 500,
                };
            }
        }

        public async Task<ResponseModel> RegisterDeviceToken(
            string userRowId,
            RegisterDeviceTokenDTO register
        )
        {
            try
            {
                var userGuid = Guid.Parse(userRowId);

                if (string.IsNullOrWhiteSpace(register.DeviceToken))
                {
                    return new ResponseModel
                    {
                        Message = "Device token is required.",
                        Success = false,
                        Type = "BadRequest",
                        Status = 400,
                    };
                }

                var existingDevice = await _dbcontext.UserDeviceTokens.FirstOrDefaultAsync(x =>
                    x.DeviceId == register.DeviceId || x.DeviceToken == register.DeviceToken
                );

                if (existingDevice != null)
                {
                    existingDevice.UserId = userGuid;
                    existingDevice.DeviceToken = register.DeviceToken;
                    existingDevice.DeviceId = register.DeviceId;
                    if (!string.IsNullOrWhiteSpace(register.DevicePlatform))
                    {
                        existingDevice.DevicePlatform = register.DevicePlatform;
                    }
                    if (!string.IsNullOrWhiteSpace(register.DeviceModel))
                    {
                        existingDevice.DeviceModel = register.DeviceModel;
                    }
                    existingDevice.IsActive = true;
                    existingDevice.UpdatedAt = DateTime.UtcNow;

                    await _dbcontext.SaveChangesAsync();

                    return new ResponseModel
                    {
                        Message = "Device token updated successfully.",
                        Success = true,
                        Type = "Success",
                        Status = 200,
                    };
                }

                var newDevice = new UserDeviceToken
                {
                    Id = Guid.NewGuid(),
                    UserId = userGuid,
                    DeviceId = register.DeviceId,
                    DeviceToken = register.DeviceToken,
                    DevicePlatform = register.DevicePlatform,
                    DeviceModel = register.DeviceModel,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                _dbcontext.UserDeviceTokens.Add(newDevice);
                await _dbcontext.SaveChangesAsync();

                return new ResponseModel
                {
                    Message = "Device token registered successfully.",
                    Success = true,
                    Type = "Success",
                    Status = 200,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while registering device token.");
                return new ResponseModel
                {
                    Message = "Internal Server Error",
                    Success = false,
                    Type = "Fail",
                    Status = 500,
                };
            }
        }

        public async Task<ResponseModel> RemoveDeviceToken(string userRowId, string deviceToken)
        {
            try
            {
                var userGuid = Guid.Parse(userRowId);

                if (string.IsNullOrWhiteSpace(deviceToken))
                {
                    return new ResponseModel
                    {
                        Message = "Device token cannot be empty.",
                        Success = false,
                        Type = "BadRequest",
                        Status = 400,
                    };
                }

                var device = await _dbcontext.UserDeviceTokens.FirstOrDefaultAsync(x =>
                    x.UserId == userGuid && x.DeviceToken == deviceToken
                );

                if (device == null)
                {
                    return new ResponseModel
                    {
                        Message = "Device token not found.",
                        Success = false,
                        Type = "NotFound",
                        Status = 404,
                    };
                }

                device.IsActive = false;
                device.UpdatedAt = DateTime.UtcNow;
                await _dbcontext.SaveChangesAsync();

                return new ResponseModel
                {
                    Message = "Device token removed successfully.",
                    Success = true,
                    Type = "Success",
                    Status = 200,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while removing device token.");
                return new ResponseModel
                {
                    Message = "Internal Server Error",
                    Success = false,
                    Type = "Fail",
                    Status = 500,
                };
            }
        }
    }
}
