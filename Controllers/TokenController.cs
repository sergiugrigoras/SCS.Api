using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SCS.Api.Models;
using SCS.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SCS.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly IUserService _userService;

        public TokenController(ITokenService tokenService, IUserService userService, AppDbContext context)
        {
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        [HttpPost]
        [Route("refresh")]
        public async Task<IActionResult> RefreshAsync([FromBody] TokenApiModel tokenApiModel)
        {
            if (tokenApiModel is null || tokenApiModel.AccessToken == String.Empty || tokenApiModel.RefreshToken == String.Empty)
            {
                return BadRequest("Invalid client request");
            }

            string accessToken = tokenApiModel.AccessToken;
            string refreshToken = tokenApiModel.RefreshToken;

            var principal = _tokenService.GetPrincipalFromExpiredToken(accessToken);
            var user = await _userService.GetUserFromPrincipalAsync(principal);
            if (user == null || user.RefreshToken != refreshToken || user.RefreshTokenExpiryTime <= DateTime.Now)
            {
                return BadRequest("Invalid client request");
            }

            var newAccessToken = _tokenService.GenerateAccessToken(principal.Claims);
            var newRefreshToken = _tokenService.GenerateRefreshToken();
            user.RefreshToken = newRefreshToken;
            await _userService.UpdateUserAsync(user);

            var result = new TokenApiModel(newAccessToken, newRefreshToken);
            return new JsonResult(result);
        }

        [HttpDelete, Authorize]
        [Route("revoke")]
        public async Task<IActionResult> RevokeAsync()
        {
            var user = await _userService.GetUserFromPrincipalAsync(this.User);
            if (user == null)
            {
                return BadRequest();
            }
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await _userService.UpdateUserAsync(user);
            return Ok();
        }
    }


}
