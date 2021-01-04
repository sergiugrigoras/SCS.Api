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

namespace SCS.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PasswordController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PasswordController(AppDbContext userContext)
        {
            this._context = userContext ?? throw new ArgumentNullException(nameof(userContext));
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
        [HttpGet, Route("token")]
        public IActionResult GetResetToken()
        {
            var randomNumber = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
            }
            var token = Convert.ToBase64String(randomNumber).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var url = $@"{Request.Scheme}://{Request.Host}/password/reset?token={token}";
            System.IO.File.WriteAllText(@"C:\tmp\resetToken.txt", url);

            return Ok();
        }
        public class Password
        {
            public string OldPassword { get; set; }
            public string NewPassword { get; set; }
        }
    }
}
