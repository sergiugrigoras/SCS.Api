using System;
using System.Collections.Generic;

namespace SCS.Api.Models
{
    public partial class Note
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime ModificationDate { get; set; }
        public string Color { get; set; }
        public string Type { get; set; }
        public string ShareKey { get; set; }
    }
}
