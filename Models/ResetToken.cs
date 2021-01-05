using System;
using System.Collections.Generic;

namespace SCS.Api.Models
{
    public partial class ResetToken
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string TokenHash { get; set; }
        public DateTime ExpirationDate { get; set; }
        public bool TokenUsed { get; set; }
    }
}
