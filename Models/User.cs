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
}
