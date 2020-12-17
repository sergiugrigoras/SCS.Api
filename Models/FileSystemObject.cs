using System;
using System.Collections.Generic;

namespace SCS.Api.Models
{
    public partial class FileSystemObject
    {
        public FileSystemObject()
        {
            InverseParent = new HashSet<FileSystemObject>();
            Users = new HashSet<User>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public int? ParentId { get; set; }
        public bool IsFolder { get; set; }
        public string FileName { get; set; }
        public long? FileSize { get; set; }
        public DateTime Date { get; set; }

        public virtual FileSystemObject Parent { get; set; }
        public virtual ICollection<FileSystemObject> InverseParent { get; set; }
        public virtual ICollection<User> Users { get; set; }
    }
}
