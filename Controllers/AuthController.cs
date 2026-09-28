using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationMicroservice.DTOs;

namespace NotificationMicroservice.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        // Token ISSUANCE is deliberately not implemented here.
        //
        // This service is a resource server: the identity service signs tokens and this
        // service only validates them (signature, issuer, audience, lifetime - see Program.cs).
        // For local runs mint a token with scripts/generate-dev-token.py, which stands in for
        // the identity service without putting signing material inside this service.

        // Proves validation is real: a forged, unsigned or expired token gets 401.
        [Authorize]
        [HttpGet("me")]
        public ActionResult GetCurrentUser()
        {
            var res = new ResponseModel
            {
                Message = "Token signature, issuer, audience and lifetime were validated.",
                Success = true,
                Type = "Success",
                Status = 200,
                Data = new
                {
                    UserRowId = User.FindFirst("UserRowId")?.Value,
                    Subject = User.FindFirst("sub")?.Value,
                    Email = User.FindFirst("email")?.Value,
                    TokenId = User.FindFirst("jti")?.Value,
                },
            };

            return StatusCode(res.Status, res);
        }
    }
}
