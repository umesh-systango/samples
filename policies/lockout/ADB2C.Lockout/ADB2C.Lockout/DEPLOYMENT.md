# Deployment Guide

This guide provides multiple options for deploying the Azure B2C Lockout API.

## Option 1: Azure CLI Deployment (Recommended)

### Prerequisites
- Azure CLI installed
- Azure subscription
- Git installed

### Steps

1. **Login to Azure**
   ```bash
   az login
   ```

2. **Run the deployment script**
   ```bash
   chmod +x deploy-to-azure.sh
   ./deploy-to-azure.sh
   ```

3. **Verify deployment**
   - Check the health endpoint: `https://your-app-name.azurewebsites.net/api/monitoring/health`
   - Test the signin endpoint with a sample request

## Option 2: Manual Azure Portal Deployment

### Steps

1. **Create Resource Group**
   - Go to Azure Portal
   - Create a new resource group named `Azureadb2c`

2. **Create App Service Plan**
   - Create a new App Service Plan
   - Choose Linux as the OS
   - Select Basic (B1) or higher tier

3. **Create Web App**
   - Create a new Web App
   - Choose .NET Core 3.1 as the runtime stack
   - Select the App Service Plan created above

4. **Deploy Code**
   - Go to Deployment Center in your Web App
   - Choose Local Git/FTPS credentials
   - Follow the Git deployment instructions

## Option 3: Azure DevOps Pipeline

### Steps

1. **Set up Azure DevOps**
   - Create a new Azure DevOps project
   - Connect your Azure subscription

2. **Create Pipeline**
   - Use the `azure-deploy.yml` file provided
   - Update the subscription name in the YAML file

3. **Run Pipeline**
   - Commit and push your code
   - The pipeline will automatically build and deploy

## Option 4: Visual Studio Deployment

### Steps

1. **Open in Visual Studio**
   - Open the solution in Visual Studio
   - Right-click on the project

2. **Publish**
   - Choose "Publish"
   - Select "Azure App Service"
   - Create new or select existing App Service

## Configuration

### Environment Variables

Set these in your Azure App Service Configuration:

```
ASPNETCORE_ENVIRONMENT=Production
Logging:LogLevel:Default=Information
Logging:LogLevel:Microsoft=Warning
```

### Application Settings

Configure these in Azure App Service:

- **WEBSITE_RUN_FROM_PACKAGE**: 1 (for better performance)
- **DOTNET_RUNNING_IN_CONTAINER**: false

## Testing Deployment

### Health Check
```bash
curl https://your-app-name.azurewebsites.net/api/monitoring/health
```

### Test Lockout Functionality
```bash
# Test failed login attempts
for i in {1..6}; do
  curl -X POST https://your-app-name.azurewebsites.net/api/identity/signin \
    -H "Content-Type: application/json" \
    -d '{"signInName": "test@example.com"}'
  echo ""
done
```

### Check Statistics
```bash
curl https://your-app-name.azurewebsites.net/api/monitoring/stats
```

## Troubleshooting

### Common Issues

1. **Runtime Version Mismatch**
   - Ensure you're using .NET Core 3.1 runtime
   - Update the project file if needed

2. **Deployment Failures**
   - Check the deployment logs in Azure Portal
   - Verify all dependencies are included

3. **API Not Responding**
   - Check the application logs
   - Verify the startup command is correct

### Logs

View logs in Azure Portal:
1. Go to your Web App
2. Navigate to "Log stream"
3. Check for any errors

### Monitoring

Enable Application Insights for better monitoring:
1. Go to your Web App
2. Navigate to "Application Insights"
3. Enable monitoring

## Security Considerations

1. **HTTPS Only**
   - Ensure HTTPS is enforced
   - Redirect HTTP to HTTPS

2. **Authentication**
   - Add authentication for monitoring endpoints
   - Use Azure AD for admin access

3. **Rate Limiting**
   - Consider implementing rate limiting
   - Use Azure Front Door for additional protection

## Cost Optimization

1. **App Service Plan**
   - Start with Basic (B1) tier
   - Scale up as needed

2. **Monitoring**
   - Use Application Insights Basic tier
   - Set up cost alerts

## Cleanup

To remove all resources:
```bash
az group delete --name Azureadb2c --yes
```

## Support

For issues:
1. Check the application logs
2. Review the troubleshooting section
3. Contact Azure support if needed 