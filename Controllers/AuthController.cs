using BC = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SCS.Api.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SCS.Api.Services;

namespace SCS.Api.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly IUserService _userService;
        private readonly IFsoService _fsoService;

        public AuthController(ITokenService tokenService, IUserService userService, IFsoService fsoService)
        {
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _fsoService = fsoService ?? throw new ArgumentNullException(nameof(fsoService));
        }

        [HttpPost, Route("login")]
        public async Task<IActionResult> LoginAsync([FromBody] User request)
        {
            if (request == null)
            {
                return BadRequest("Invalid client request");
            }

            User user;
            if (request.Username != "")
            {
                user = await _userService.GetUserByNameAsync(request.Username);
            }
            else
            {
                user = await _userService.GetUserByEmailAsync(request.Email);
            }

            if (user == null || !BC.Verify(request.Password, user.Password))
            {
                return NotFound();
            }

            var claims = _userService.GetUserClaims(user);

            var accessToken = _tokenService.GenerateAccessToken(claims);
            var refreshToken = _tokenService.GenerateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
            await _userService.UpdateUserAsync(user);

            return Ok(new TokenApiModel(accessToken, refreshToken));
        }

        [HttpPost, Route("register")]
        public async Task<IActionResult> RegisterAsync([FromBody] User request)
        {
            if (request == null || String.IsNullOrEmpty(request.Username) || String.IsNullOrEmpty(request.Email) || String.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Invalid client request");
            }

            if (await _userService.GetUserByNameAsync(request.Username) != null || await _userService.GetUserByEmailAsync(request.Email) != null)
            {
                return BadRequest("Invalid client request, not unique");
            }

            var userDriveFso = await _fsoService.CreateFsoAsync("root", null, null, true, null);
            request.Id = Guid.NewGuid().ToString();
            var claims = _userService.GetUserClaims(request);
            var accessToken = _tokenService.GenerateAccessToken(claims);
            var refreshToken = _tokenService.GenerateRefreshToken();
            var hashPassword = BC.HashPassword(request.Password);

            request.Password = hashPassword;
            request.RefreshToken = refreshToken;
            request.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
            request.DriveId = userDriveFso.Id;

            await _userService.CreateUserAsync(request);

            return Ok(new TokenApiModel(accessToken, refreshToken));
        }

        [HttpPost("checkunique")]
        public async Task<bool> UniqueUsernameAsync([FromBody] User request)
        {
            return await _userService.GetUserByNameAsync(request.Username) == null && await _userService.GetUserByEmailAsync(request.Email) == null;
        }
    }
}
