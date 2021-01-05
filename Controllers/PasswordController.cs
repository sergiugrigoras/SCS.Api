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

namespace SCS.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PasswordController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITokenService _tokenService;

        public PasswordController(ITokenService tokenService, AppDbContext userContext)
        {
            this._context = userContext ?? throw new ArgumentNullException(nameof(userContext));
            this._tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        }

        [HttpPost, Route("change")]
        public async Task<IActionResult> ChangePasswordAsync([FromBody] Password pass)
        { 
            var userId = HttpContext.User.FindFirst(JwtRegisteredClaimNames.Jti).Value;
            var user = await  _context.Users.FindAsync(userId);
            if (user == null)
            {
                return Unauthorized();
            }

            if (BC.Verify(pass.OldPassword, user.Password))
            {
                user.Password = BC.HashPassword(pass.NewPassword);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Success"});
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

            /*var url = $@"mailto:{user.Email} - {Request.Scheme}://{Request.Host}/password/reset?token={token}&id={resetToken.Id}";*/
/*            System.IO.File.WriteAllText(@"C:\tmp\resetToken.txt", $"Hello, \n\nPlease use below link to reset your password\n{Request.Scheme}://{Request.Host}/password/reset?token={token}&id={resetToken.Id}");*/
            
            sendEmail(user.Email, "Password reset instructions", $"Hello, \n\nPlease use below link to reset your password\n{Request.Scheme}://{Request.Host}/password/reset?token={token}&id={resetToken.Id}");
            return Ok( new { mail = Regex.Replace(user.Email, @"(?<=[\w]{1})[\w-\._\+%]*(?=[\w]{2}@)", m => new string('*', m.Length)) });
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

        private static void sendEmail(string toEmailAddress, string emailSubject, string emailBody)
        {
            MailAddress to = new MailAddress(toEmailAddress);
            MailAddress from = new MailAddress("support@mail.sergiug.space", "SCS Support");

            MailMessage message = new MailMessage(from, to);
            message.Subject = emailSubject;
            message.Body = emailBody;

            SmtpClient client = new SmtpClient("localhost", 25);
            try
            {
                client.Send(message);
            }
            catch (SmtpException ex)
            {
                throw ex;
            }
        }
    }

    public class Password
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
    }
    public class PasswordResetRequest
    {
        public int TokenId { get; set; }
        public string Token { get; set; }
        public string NewPassword { get; set; }
    }
}
