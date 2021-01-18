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
        private readonly AppDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IMailService _mailService;

        public PasswordController(AppDbContext userContext, ITokenService tokenService, IMailService mailService)
        {
            _context = userContext ?? throw new ArgumentNullException(nameof(userContext));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _mailService = mailService ?? throw new ArgumentNullException(nameof(mailService));
        }

        [HttpPost, Route("change")]
        public async Task<IActionResult> ChangePasswordAsync([FromBody] Password pass)
        {
            var userId = HttpContext.User.FindFirst(JwtRegisteredClaimNames.Jti).Value;
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return Unauthorized();
            }

            if (BC.Verify(pass.OldPassword, user.Password))
            {
                user.Password = BC.HashPassword(pass.NewPassword);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Success" });
            }
            else
            {
                return BadRequest("Invalid Password");
            }
        }

        [AllowAnonymous]
        [HttpPost, Route("token")]
        public async Task<IActionResult> GenerateResetTokenAsync([FromBody] User requestUser)
        {
            if (requestUser == null)
            {
                return BadRequest("Invalid client request");
            }

            User user;
            if (requestUser.Username != "")
            {
                user = await _context.Users.FirstOrDefaultAsync(x => x.Username.ToLower() == requestUser.Username.ToLower());
            }
            else
            {
                user = await _context.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == requestUser.Email.ToLower());
            }

            if (user == null)
            {
                return NotFound("Invalid user");
            }

            var randomNumber = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
            }
            var token = Convert.ToBase64String(randomNumber).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var resetToken = new ResetToken();
            resetToken.UserId = user.Id;
            resetToken.TokenHash = BC.HashPassword(token);
            resetToken.ExpirationDate = DateTime.Now.AddHours(1);

            await _context.ResetTokens.AddAsync(resetToken);
            await _context.SaveChangesAsync();

            _mailService.SendEmail(
                                    new MailAddress(user.Email),
                                    new MailAddress("support@mail.sergiug.space", "SCS Support"),
                                    "Password reset instructions",
                                    $"Hello, \n\nPlease use below link to reset your password\n{Request.Scheme}://{Request.Host}/password/reset?token={token}&id={resetToken.Id}"
            );
            return Ok(new { mail = Regex.Replace(user.Email, @"(?<=[\w]{1})[\w-\._\+%]*(?=[\w]{2}@)", m => new string('*', m.Length)) });
        }

        [AllowAnonymous]
        [HttpPost, Route("reset")]
        public async Task<IActionResult> ResetPasswordAsync([FromBody] PasswordResetRequest requestReset)
        {
            var resetToken = await _context.ResetTokens.FindAsync(requestReset.TokenId);
            if (resetToken == null || resetToken.ExpirationDate <= DateTime.Now || resetToken.TokenUsed || !BC.Verify(requestReset.Token, resetToken.TokenHash))
            {
                return BadRequest();
            }
            else
            {
                var user = await _context.Users.FindAsync(resetToken.UserId);
                user.Password = BC.HashPassword(requestReset.NewPassword);
                resetToken.TokenUsed = true;

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(JwtRegisteredClaimNames.Email,user.Email),
                    new Claim(JwtRegisteredClaimNames.Jti,user.Id),
                };

                var accessToken = _tokenService.GenerateAccessToken(claims);
                var refreshToken = _tokenService.GenerateRefreshToken();

                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                });
            }
        }
    }
}
