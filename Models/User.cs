using System;
using System.Collections.Generic;

namespace SCS.Api.Models
{
    public partial class User
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public int? DriveId { get; set; }
        public string RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        public virtual FileSystemObject Drive { get; set; }
    }

    public class TokenApiModel
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }

        public TokenApiModel()
        {
        }
        public TokenApiModel(string accessToken, string refreshToken)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
        }
    }

    public class PasswordResetToken
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string TokenHash { get; set; }
        public DateTime ExpirationDate { get; set; }
        public bool TokenUsed { get; set; }
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
