using SCS.Api.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;

namespace SCS.Api.Services
{
    public interface IUserService
    {
        Task<User> GetUserFromPrincipalAsync(ClaimsPrincipal principal);
    }

    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        public UserService(AppDbContext context)
        {
            _context = context;
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
    }
}
