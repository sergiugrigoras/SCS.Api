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

namespace SCS.Api.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly AppDbContext _context;

        public AuthController(ITokenService tokenService, AppDbContext userContext)
        {
            this._tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            this._context = userContext ?? throw new ArgumentNullException(nameof(userContext));
        }

        [HttpPost, Route("login")]
        public IActionResult Login([FromBody] User user)
        {
            if (user == null)
            {
                return BadRequest("Invalid client request");
            }

            User u;
            if (user.Username != "")
            {
                u = _context.Users.FirstOrDefault(x => x.Username == user.Username);
            }
            else
            { 
                u = _context.Users.FirstOrDefault(x => x.Email == user.Email);
            }

            if (u == null || !BC.Verify(user.Password, u.Password))
            {
                return NotFound();
            }

            var claims = new List<Claim>
            {
                    new Claim(ClaimTypes.Name, u.Username),
                    new Claim(JwtRegisteredClaimNames.Email,u.Email),
                    new Claim(JwtRegisteredClaimNames.Jti,u.Id),
                    //new Claim(ClaimTypes.Role, "Manager")
            };

            var accessToken = _tokenService.GenerateAccessToken(claims);
            var refreshToken = _tokenService.GenerateRefreshToken();

            u.RefreshToken = refreshToken;
            u.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
            _context.SaveChanges();

            return Ok(new
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            });
        }
        [HttpPost, Route("register")]
        public IActionResult Register([FromBody] User user)
        {
            if (user == null)
            {
                return BadRequest("Invalid client request");
            }

            var u = _context.Users.FirstOrDefault(u => u.Username == user.Username);
            if (u != null)
            {
                return BadRequest("Invalid client request");
            }
            
            var fso = new FileSystemObject
            {
                Name = "root",
                ParentId = null,
                IsFolder = true,
                FileName = null,
                FileSize = null,
                Date = DateTime.Now
            };
            _context.FileSystemObjects.Add(fso);
            _context.SaveChanges();

            var userId = Guid.NewGuid().ToString();

            var claims = new List<Claim>
            {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(JwtRegisteredClaimNames.Jti,userId),
                    //new Claim(ClaimTypes.Role, "Manager")
            };

            var accessToken = _tokenService.GenerateAccessToken(claims);
            var refreshToken = _tokenService.GenerateRefreshToken();

            var password = BC.HashPassword(user.Password);
            user.Password = password;

            user.Id = userId;
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
            user.DriveId = fso.Id;
            
            _context.Users.Add(user);
            _context.SaveChanges();

            return Ok(new
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            });
        }
        [HttpPost("uniqueusername")]
        public bool UniqueUsername([FromBody] Usr user)
        {
            var u = _context.Users.FirstOrDefault(u => u.Username.ToLower() == user.Username.ToLower());
            if (u != null)
            {
                return false;
            }
            else
                return true;
            
        }

        [HttpPost("uniqueemail")]
        public bool UniqueEmail([FromBody] EmailAddress email)
        {
            var u = _context.Users.FirstOrDefault(u => u.Email.ToLower() == email.Email.ToLower());
            if (u != null)
            {
                return false;
            }
            else
                return true;

        }

        public class Usr {
            public string Username { get; set; }
        }

        public class EmailAddress { 
            public string Email { get; set; }
        }
    }
}
