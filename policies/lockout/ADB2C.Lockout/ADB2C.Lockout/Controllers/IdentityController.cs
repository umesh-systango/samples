using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ADB2C.Lockout.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AADB2C.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    public class IdentityController : Controller
    {
        private readonly ILogger<IdentityController> _logger;
        private static readonly ConcurrentDictionary<string, User> users = new ConcurrentDictionary<string, User>();

        public IdentityController(ILogger<IdentityController> logger)
        {
            _logger = logger;
        }

        [HttpPost(Name = "signin")]
        public async Task<ActionResult> SignIn()
        {
            string input = null;

            // If no data came in, then return
            if (this.Request.Body == null)
            {
                _logger.LogWarning("SignIn attempt with null request body");
                return StatusCode((int)HttpStatusCode.BadRequest, new B2CResponseModel("Request content is null", HttpStatusCode.BadRequest));
            }

            // Read the input claims from the request body
            using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                input = await reader.ReadToEndAsync();
            }

            // Check input content value
            if (string.IsNullOrEmpty(input))
            {
                _logger.LogWarning("SignIn attempt with empty request body");
                return StatusCode((int)HttpStatusCode.BadRequest, new B2CResponseModel("Request content is empty", HttpStatusCode.BadRequest));
            }

            // Convert the input string into InputClaimsModel object
            InputClaimsModel inputClaims = InputClaimsModel.Parse(input);

            if (inputClaims == null)
            {
                _logger.LogWarning("SignIn attempt with invalid input claims format");
                return StatusCode((int)HttpStatusCode.BadRequest, new B2CResponseModel("Cannot deserialize input claims", HttpStatusCode.BadRequest));
            }

            if (string.IsNullOrEmpty(inputClaims.signInName))
            {
                _logger.LogWarning("SignIn attempt with null or empty username");
                return StatusCode((int)HttpStatusCode.BadRequest, new B2CResponseModel("Username is null or empty", HttpStatusCode.BadRequest));
            }

            try
            {
                string userName = inputClaims.signInName.ToLower().Trim();
                User user = null;

                // Get or create the user object
                if (!users.ContainsKey(userName))
                {
                    user = new User() 
                    { 
                        Count = 1, 
                        timeStamp = DateTime.UtcNow.Ticks,
                        LastFailedAttempt = DateTime.UtcNow,
                        UserName = userName,
                        IsLocked = false
                    };
                    users.TryAdd(userName, user);
                }
                else
                {
                    user = users[userName];
                    
                    // Check if account is locked
                    if (user.IsLocked && user.LockoutStartTime.HasValue)
                    {
                        var lockoutDuration = DateTime.UtcNow - user.LockoutStartTime.Value;
                        var unlockAfterMinutes = TimeSpan.FromMinutes(Consts.UNLOCK_AFTER);
                        
                        if (lockoutDuration < unlockAfterMinutes)
                        {
                            var remainingTime = unlockAfterMinutes - lockoutDuration;
                            _logger.LogInformation($"Account {userName} is locked. Remaining lockout time: {remainingTime.TotalSeconds:F0} seconds");
                            
                            return StatusCode((int)HttpStatusCode.TooManyRequests, new B2CResponseModel(
                                $"Your account is locked. Please try again in {remainingTime.TotalSeconds:F0} seconds", 
                                HttpStatusCode.TooManyRequests)
                            {
                                developerMessage = $"Account locked. Failed attempts: {user.Count}, Lockout started: {user.LockoutStartTime.Value:yyyy-MM-dd HH:mm:ss}"
                            });
                        }
                        else
                        {
                            // Unlock the account after the lockout period
                            user.IsLocked = false;
                            user.LockoutStartTime = null;
                            user.Count = 1;
                            user.LastFailedAttempt = DateTime.UtcNow;
                            _logger.LogInformation($"Account {userName} unlocked after lockout period");
                        }
                    }
                    else
                    {
                        user.Count += 1;
                        user.LastFailedAttempt = DateTime.UtcNow;
                    }
                }

                // If user successfully sign-in, reset the counter and remove lockout
                if (!string.IsNullOrEmpty(inputClaims.objectId))
                {
                    user.Count = 0;
                    user.IsLocked = false;
                    user.LockoutStartTime = null;
                    users.AddOrUpdate(userName, user, (key, oldValue) => user);
                    
                    _logger.LogInformation($"Successful sign-in for user: {userName}");
                    return StatusCode((int)HttpStatusCode.OK, new B2CResponseModel("Successful sign-in", HttpStatusCode.OK));
                }

                // Check if account should be locked
                if (user.Count >= Consts.LOCKOUT_AFTER)
                {
                    user.IsLocked = true;
                    user.LockoutStartTime = DateTime.UtcNow;
                    users.AddOrUpdate(userName, user, (key, oldValue) => user);
                    
                    _logger.LogWarning($"Account {userName} locked after {user.Count} failed attempts");
                    
                    return StatusCode((int)HttpStatusCode.TooManyRequests, new B2CResponseModel(
                        $"Your account is locked for {Consts.UNLOCK_AFTER} minute(s) due to too many failed attempts. Please try again later.", 
                        HttpStatusCode.TooManyRequests)
                    {
                        developerMessage = $"Account locked. Failed attempts: {user.Count}, Lockout started: {user.LockoutStartTime.Value:yyyy-MM-dd HH:mm:ss}"
                    });
                }
                
                // Update the counter and return error for failed login
                users.AddOrUpdate(userName, user, (key, oldValue) => user);

                _logger.LogInformation($"Failed login attempt for user: {userName}. Attempt {user.Count} of {Consts.LOCKOUT_AFTER}");
                
                return StatusCode((int)HttpStatusCode.Unauthorized, new B2CResponseModel("Invalid username or password", HttpStatusCode.Unauthorized)
                {
                    developerMessage = $"Failed login attempt {user.Count} of {Consts.LOCKOUT_AFTER}. Last failed attempt: {user.LastFailedAttempt:yyyy-MM-dd HH:mm:ss}"
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing sign-in request for user: {inputClaims?.signInName}");
                return StatusCode((int)HttpStatusCode.InternalServerError, new B2CResponseModel("An error occurred while processing your request", HttpStatusCode.InternalServerError));
            }
        }

        [HttpGet("status/{username}")]
        public ActionResult GetAccountStatus(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return BadRequest("Username is required");
            }

            string userName = username.ToLower().Trim();
            
            if (users.TryGetValue(userName, out User user))
            {
                double remainingTime = 0;
                if (user.IsLocked && user.LockoutStartTime.HasValue)
                {
                    var lockoutDuration = DateTime.UtcNow - user.LockoutStartTime.Value;
                    var unlockAfterMinutes = TimeSpan.FromMinutes(Consts.UNLOCK_AFTER);
                    remainingTime = Math.Max(0, (unlockAfterMinutes - lockoutDuration).TotalSeconds);
                }

                var status = new
                {
                    UserName = user.UserName,
                    FailedAttempts = user.Count,
                    IsLocked = user.IsLocked,
                    LastFailedAttempt = user.LastFailedAttempt,
                    LockoutStartTime = user.LockoutStartTime,
                    RemainingLockoutTime = remainingTime
                };
                
                return Ok(status);
            }
            
            return NotFound($"No account information found for user: {username}");
        }

        [HttpPost("reset/{username}")]
        public ActionResult ResetAccount(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return BadRequest("Username is required");
            }

            string userName = username.ToLower().Trim();
            
            if (users.TryRemove(userName, out User user))
            {
                _logger.LogInformation($"Account reset for user: {userName}");
                return Ok($"Account reset successfully for user: {username}");
            }
            
            return NotFound($"No account found to reset for user: {username}");
        }
    }
}