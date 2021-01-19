using BC = BCrypt.Net.BCrypt;
using Microsoft.EntityFrameworkCore;
using SCS.Api.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace SCS.Api.Services
{
    public interface IUserService
    {
        Task<User> GetUserFromPrincipalAsync(ClaimsPrincipal principal);
        Task<User> GetUserByNameAsync(string name);
        Task<User> GetUserByEmailAsync(string email);
        Task<User> GetUserByIdAsync(string id);
        Task UpdateUserAsync(User user);
        Task CreateUserAsync(User user);
        List<Claim> GetUserClaims(User user);
        Task<PasswordResetToken> CreatePasswordResetTokenAsync(User user, string token);
        Task<PasswordResetToken> GetPasswordResetTokenByIdAsync(int id);
        Task UpdatePasswordResetTokenAsync(PasswordResetToken passwordResetToken);

        string GenerateToken();
    }

    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        public UserService(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateUserAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
        }

        public async Task<PasswordResetToken> CreatePasswordResetTokenAsync(User user, string token)
        {
            var passwordResetToken = new PasswordResetToken();
            passwordResetToken.UserId = user.Id;
            passwordResetToken.TokenHash = BC.HashPassword(token);
            passwordResetToken.ExpirationDate = DateTime.Now.AddHours(1);

            await _context.ResetTokens.AddAsync(passwordResetToken);
            await _context.SaveChangesAsync();
            return passwordResetToken;
        }

        public async Task<User> GetUserByEmailAsync(string email)
        {
            if (email == null)
            {
                return null;
            }
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == email.ToLower());
            return user;
        }

        public async Task<User> GetUserByNameAsync(string name)
        {
            if (name == null)
            {
                return null;
            }
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Username.ToLower() == name.ToLower());
            return user;
        }

        public List<Claim> GetUserClaims(User user)
        {
            if (user != null)
            {
                return new List<Claim>
                {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(JwtRegisteredClaimNames.Email,user.Email),
                        new Claim(JwtRegisteredClaimNames.Jti,user.Id),
                };
            }
            else
                return null;

        }

        public async Task<User> GetUserFromPrincipalAsync(ClaimsPrincipal principal)
        {
            if (principal.FindFirst(JwtRegisteredClaimNames.Jti) == null)
            {
                return null;
            }
            else
            {
                var id = principal.FindFirst(JwtRegisteredClaimNames.Jti).Value;
                var user = await _context.Users.FindAsync(id);
                return user;
            }

        }

        public async Task UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public string GenerateToken()
        {
            var randomNumber = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
            }
            var token = Convert.ToBase64String(randomNumber).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return token;
        }

        public async Task<PasswordResetToken> GetPasswordResetTokenByIdAsync(int id)
        {
            var passwordResetToken = await _context.ResetTokens.FindAsync(id);
            return passwordResetToken;
        }

        public async Task<User> GetUserByIdAsync(string id)
        {
            var user = await _context.Users.FindAsync(id);
            return user;
        }

        public async Task UpdatePasswordResetTokenAsync(PasswordResetToken passwordResetToken)
        {
            _context.ResetTokens.Update(passwordResetToken);
            await _context.SaveChangesAsync();
        }
    }
}
