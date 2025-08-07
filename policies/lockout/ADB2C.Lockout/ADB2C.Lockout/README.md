# Azure B2C Lockout API

A secure Azure-based API for monitoring login attempts and implementing account lockout functionality for Azure B2C (Business-to-Consumer) applications.

## Features

- **Login Attempt Monitoring**: Tracks failed login attempts per user
- **Automatic Account Lockout**: Locks accounts after 5 consecutive failed attempts
- **Temporary Lockout**: Accounts are locked for 60 seconds (1 minute)
- **Automatic Unlock**: Accounts automatically unlock after the lockout period
- **Real-time Monitoring**: Provides endpoints for monitoring account status and statistics
- **Azure Integration**: Designed to work seamlessly with Azure B2C policies

## Configuration

### Lockout Settings

The lockout behavior is configured in `Models/Consts.cs`:

```csharp
public class Consts
{
    public const int LOCKOUT_AFTER = 5;  // Lock account after 5 failed attempts
    public const int UNLOCK_AFTER = 1;   // Unlock after 1 minute (60 seconds)
}
```

## API Endpoints

### Authentication Endpoints

#### POST /api/identity/signin
Handles user sign-in attempts and implements lockout logic.

**Request Body:**
```json
{
    "signInName": "user@example.com",
    "objectId": "optional-user-id-for-successful-login"
}
```

**Responses:**
- `200 OK`: Successful login
- `401 Unauthorized`: Invalid credentials (with attempt count)
- `429 Too Many Requests`: Account locked (with remaining lockout time)

### Monitoring Endpoints

#### GET /api/monitoring/health
Health check endpoint for the API.

**Response:**
```json
{
    "Status": "Healthy",
    "Timestamp": "2024-01-01T12:00:00Z",
    "Service": "ADB2C Lockout API",
    "Version": "1.0.0"
}
```

#### GET /api/monitoring/stats
Returns system-wide statistics.

**Response:**
```json
{
    "TotalAccounts": 10,
    "LockedAccounts": 2,
    "ActiveAccounts": 8,
    "RecentFailedAttempts": 5,
    "LockoutThreshold": 5,
    "LockoutDurationMinutes": 1,
    "Timestamp": "2024-01-01T12:00:00Z"
}
```

#### GET /api/monitoring/accounts
Returns detailed information about all monitored accounts.

**Response:**
```json
[
    {
        "UserName": "user@example.com",
        "FailedAttempts": 3,
        "IsLocked": false,
        "LastFailedAttempt": "2024-01-01T11:55:00Z",
        "LockoutStartTime": null,
        "RemainingLockoutTime": 0
    }
]
```

#### GET /api/identity/status/{username}
Returns detailed status for a specific user account.

#### POST /api/monitoring/unlock/{username}
Manually unlocks a specific user account (administrative function).

#### POST /api/identity/reset/{username}
Resets a user's account (removes all tracking data).

#### POST /api/monitoring/clear-all
Clears all account data (administrative function).

## Usage Examples

### Testing Failed Login Attempts

```bash
# First failed attempt
curl -X POST http://localhost:5000/api/identity/signin \
  -H "Content-Type: application/json" \
  -d '{"signInName": "test@example.com"}'

# Response: 401 Unauthorized with attempt count
```

### Checking Account Status

```bash
# Check specific user status
curl http://localhost:5000/api/identity/status/test@example.com

# Response: Account details including lockout status
```

### Monitoring System Health

```bash
# Check API health
curl http://localhost:5000/api/monitoring/health

# Get system statistics
curl http://localhost:5000/api/monitoring/stats
```

## Azure B2C Integration

This API is designed to be called from Azure B2C custom policies. The API expects:

1. **Input Claims**: Username and optional object ID for successful authentication
2. **Output Claims**: Success/failure status and appropriate error messages
3. **HTTP Status Codes**: Proper HTTP status codes for different scenarios

### Azure B2C Policy Integration

In your Azure B2C custom policy, you can call this API using a REST API technical profile:

```xml
<TechnicalProfile Id="REST-LockoutAPI">
  <DisplayName>Lockout API</DisplayName>
  <Protocol Name="Proprietary" Handler="Web.TPEngine.Providers.RestfulProvider, Web.TPEngine, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" />
  <Metadata>
    <Item Key="ServiceUrl">https://your-api-url/api/identity/signin</Item>
    <Item Key="AuthenticationType">None</Item>
    <Item Key="SendClaimsIn">Body</Item>
  </Metadata>
  <InputClaims>
    <InputClaim ClaimTypeReferenceId="signInName" PartnerClaimType="signInName" />
    <InputClaim ClaimTypeReferenceId="objectId" PartnerClaimType="objectId" />
  </InputClaims>
  <OutputClaims>
    <OutputClaim ClaimTypeReferenceId="lockoutStatus" PartnerClaimType="status" />
    <OutputClaim ClaimTypeReferenceId="lockoutMessage" PartnerClaimType="userMessage" />
  </OutputClaims>
</TechnicalProfile>
```

## Security Considerations

1. **Rate Limiting**: Consider implementing rate limiting on the API endpoints
2. **HTTPS**: Always use HTTPS in production
3. **Authentication**: Add authentication for monitoring endpoints in production
4. **Data Persistence**: Consider using Azure Table Storage for persistent lockout data
5. **Monitoring**: Implement proper logging and monitoring for security events

## Deployment

### Local Development

```bash
# Restore packages
dotnet restore

# Run the application
dotnet run
```

### Azure Deployment

1. **Azure App Service**: Deploy as a web app
2. **Azure Container Instances**: Deploy as a container
3. **Azure Kubernetes Service**: Deploy to AKS for high availability

### Environment Variables

Configure the following in your Azure App Service settings:

- `ASPNETCORE_ENVIRONMENT`: Production
- `Logging:LogLevel:Default`: Information
- `Logging:LogLevel:Microsoft`: Warning

## Logging

The API includes comprehensive logging for:

- Failed login attempts
- Account lockouts and unlocks
- Administrative actions
- System errors

Logs are written to:
- Console (development)
- Azure Application Insights (production)

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Submit a pull request

## License

This project is licensed under the MIT License. 