using BC = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SCS.Api.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Net.Mail;
using SCS.Api.Services;

namespace SCS.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PasswordController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly IMailService _mailService;
        private readonly IUserService _userService;

        public PasswordController(AppDbContext userContext, ITokenService tokenService, IMailService mailService, IUserService userService)
        {
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _mailService = mailService ?? throw new ArgumentNullException(nameof(mailService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        [HttpPost, Route("change")]
        public async Task<IActionResult> ChangePasswordAsync([FromBody] Password pass)
        {
            var user = await _userService.GetUserFromPrincipalAsync(this.User);
            if (user == null)
            {
                return Unauthorized();
            }
            if (BC.Verify(pass.OldPassword, user.Password))
            {
                user.Password = BC.HashPassword(pass.NewPassword);
                await _userService.UpdateUserAsync(user);
                return Ok(new { message = "Success" });
            }
            else
            {
                return BadRequest("Invalid Password");
            }
        }

        [AllowAnonymous]
        [HttpPost, Route("token")]
        public async Task<IActionResult> GenerateResetTokenAsync([FromBody] User request)
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

            if (user == null)
            {
                return NotFound("Invalid user");
            }

            var token = _userService.GenerateToken();
            var passwordResetToken = await _userService.CreatePasswordResetTokenAsync(user, token);

            _mailService.SendEmail(
                                    new MailAddress(user.Email),
                                    new MailAddress("support@mail.sergiug.space", "SCS Support"),
                                    "Password reset instructions",
                                    $"Hello, \n\nPlease use below link to reset your password\n{Request.Scheme}://{Request.Host}/password/reset?token={token}&id={passwordResetToken.Id}"
            );
            return Ok(new { mail = Regex.Replace(user.Email, @"(?<=[\w]{1})[\w-\._\+%]*(?=[\w]{2}@)", m => new string('*', m.Length)) });
        }

        [AllowAnonymous]
        [HttpPost, Route("reset")]
        public async Task<IActionResult> ResetPasswordAsync([FromBody] PasswordResetRequest request)
        {
            var resetToken = await _userService.GetPasswordResetTokenByIdAsync(request.TokenId);
            if (resetToken == null || resetToken.ExpirationDate <= DateTime.Now || resetToken.TokenUsed || !BC.Verify(request.Token, resetToken.TokenHash))
            {
                return BadRequest();
            }
            else
            {
                var user = await _userService.GetUserByIdAsync(resetToken.UserId);
                user.Password = BC.HashPassword(request.NewPassword);
                resetToken.TokenUsed = true;

                var claims = _userService.GetUserClaims(user);

                var accessToken = _tokenService.GenerateAccessToken(claims);
                var refreshToken = _tokenService.GenerateRefreshToken();

                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
                await _userService.UpdateUserAsync(user);
                await _userService.UpdatePasswordResetTokenAsync(resetToken);
                return Ok(new TokenApiModel(accessToken, refreshToken));
            }
        }
    }
}
