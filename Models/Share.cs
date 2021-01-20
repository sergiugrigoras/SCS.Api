using System;
using System.Collections.Generic;

namespace SCS.Api.Models
{
    public partial class Share
    {
        public Share()
        {
            SharedObjects = new HashSet<SharedObject>();
        }

        public int Id { get; set; }
        public string PublicId { get; set; }
        public string UserId { get; set; }
        public DateTime ShareDate { get; set; }

        public virtual ICollection<SharedObject> SharedObjects { get; set; }
    }

    public partial class SharedObject
    {
        public int ShareId { get; set; }
        public int FsoId { get; set; }

        public virtual Share Share { get; set; }
    }
}
