using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ADB2C.Lockout.Models
{
    public class User
    {
        public int Count { get; set; }
        public long timeStamp { get; set; }
        public DateTime LastFailedAttempt { get; set; }
        public DateTime? LockoutStartTime { get; set; }
        public bool IsLocked { get; set; }
        public string UserName { get; set; }
    }
}
