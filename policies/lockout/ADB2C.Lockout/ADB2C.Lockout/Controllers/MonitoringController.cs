using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ADB2C.Lockout.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AADB2C.WebAPI.Controllers;

namespace ADB2C.Lockout.Controllers
{
    [Route("api/[controller]")]
    public class MonitoringController : Controller
    {
        private readonly ILogger<MonitoringController> _logger;

        public MonitoringController(ILogger<MonitoringController> logger)
        {
            _logger = logger;
        }

        [HttpGet("health")]
        public ActionResult HealthCheck()
        {
            return Ok(new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Service = "ADB2C Lockout API",
                Version = "1.0.0"
            });
        }

        [HttpGet("stats")]
        public ActionResult GetStatistics()
        {
            try
            {
                // Access the users dictionary from IdentityController using reflection
                var identityControllerType = typeof(IdentityController);
                var usersField = identityControllerType.GetField("users", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                
                if (usersField != null)
                {
                    var users = usersField.GetValue(null) as System.Collections.Concurrent.ConcurrentDictionary<string, User>;
                    
                    if (users != null)
                    {
                        var lockedAccounts = users.Values.Count(u => u.IsLocked);
                        var totalAccounts = users.Count;
                        var recentFailures = users.Values.Count(u => 
                            u.LastFailedAttempt > DateTime.UtcNow.AddMinutes(-5));

                        var stats = new
                        {
                            TotalAccounts = totalAccounts,
                            LockedAccounts = lockedAccounts,
                            ActiveAccounts = totalAccounts - lockedAccounts,
                            RecentFailedAttempts = recentFailures,
                            LockoutThreshold = Consts.LOCKOUT_AFTER,
                            LockoutDurationMinutes = Consts.UNLOCK_AFTER,
                            Timestamp = DateTime.UtcNow
                        };

                        _logger.LogInformation($"Statistics requested. Total accounts: {totalAccounts}, Locked: {lockedAccounts}");
                        return Ok(stats);
                    }
                }

                return Ok(new
                {
                    TotalAccounts = 0,
                    LockedAccounts = 0,
                    ActiveAccounts = 0,
                    RecentFailedAttempts = 0,
                    LockoutThreshold = Consts.LOCKOUT_AFTER,
                    LockoutDurationMinutes = Consts.UNLOCK_AFTER,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving statistics");
                return StatusCode(500, "Error retrieving statistics");
            }
        }

        [HttpGet("accounts")]
        public ActionResult GetAllAccounts()
        {
            try
            {
                var identityControllerType = typeof(IdentityController);
                var usersField = identityControllerType.GetField("users", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                
                if (usersField != null)
                {
                    var users = usersField.GetValue(null) as System.Collections.Concurrent.ConcurrentDictionary<string, User>;
                    
                    if (users != null)
                    {
                        var accounts = users.Values.Select(u => new
                        {
                            UserName = u.UserName,
                            FailedAttempts = u.Count,
                            IsLocked = u.IsLocked,
                            LastFailedAttempt = u.LastFailedAttempt,
                            LockoutStartTime = u.LockoutStartTime,
                            RemainingLockoutTime = u.IsLocked && u.LockoutStartTime.HasValue 
                                ? Math.Max(0.0, (TimeSpan.FromMinutes(Consts.UNLOCK_AFTER) - (DateTime.UtcNow - u.LockoutStartTime.Value)).TotalSeconds)
                                : 0.0
                        }).ToList();

                        _logger.LogInformation($"Account list requested. Total accounts: {accounts.Count}");
                        return Ok(accounts);
                    }
                }

                return Ok(new List<object>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving account list");
                return StatusCode(500, "Error retrieving account list");
            }
        }

        [HttpPost("unlock/{username}")]
        public ActionResult UnlockAccount(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return BadRequest("Username is required");
            }

            try
            {
                var identityControllerType = typeof(IdentityController);
                var usersField = identityControllerType.GetField("users", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                
                if (usersField != null)
                {
                    var users = usersField.GetValue(null) as System.Collections.Concurrent.ConcurrentDictionary<string, User>;
                    
                    if (users != null)
                    {
                        string userName = username.ToLower().Trim();
                        
                        if (users.TryGetValue(userName, out User user))
                        {
                            user.IsLocked = false;
                            user.LockoutStartTime = null;
                            user.Count = 0;
                            
                            users.AddOrUpdate(userName, user, (key, oldValue) => user);
                            
                            _logger.LogInformation($"Account manually unlocked for user: {userName}");
                            return Ok($"Account unlocked successfully for user: {username}");
                        }
                        
                        return NotFound($"No account found for user: {username}");
                    }
                }

                return StatusCode(500, "Unable to access user data");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unlocking account for user: {username}");
                return StatusCode(500, "Error unlocking account");
            }
        }

        [HttpPost("clear-all")]
        public ActionResult ClearAllAccounts()
        {
            try
            {
                var identityControllerType = typeof(IdentityController);
                var usersField = identityControllerType.GetField("users", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                
                if (usersField != null)
                {
                    var users = usersField.GetValue(null) as System.Collections.Concurrent.ConcurrentDictionary<string, User>;
                    
                    if (users != null)
                    {
                        var count = users.Count;
                        users.Clear();
                        
                        _logger.LogWarning($"All accounts cleared. Total accounts removed: {count}");
                        return Ok($"All accounts cleared successfully. {count} accounts removed.");
                    }
                }

                return StatusCode(500, "Unable to access user data");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all accounts");
                return StatusCode(500, "Error clearing accounts");
            }
        }
    }
} 