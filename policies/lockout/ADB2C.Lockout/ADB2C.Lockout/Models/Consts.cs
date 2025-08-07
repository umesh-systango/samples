using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ADB2C.Lockout.Models
{
    public class Consts
    {
        public const int LOCKOUT_AFTER = 5;  // Lock account after 5 failed attempts
        public const int UNLOCK_AFTER = 1;   // Unlock after 1 minute (60 seconds)
    }
}
