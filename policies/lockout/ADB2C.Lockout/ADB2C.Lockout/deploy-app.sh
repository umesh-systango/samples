#!/bin/bash

# Azure B2C Lockout API - Application Deployment Script
# This script deploys only the application to an existing Azure App Service

# Configuration
APP_NAME="adb2c-lockout-api"
RESOURCE_GROUP="Azureadb2c"

echo "=== Azure B2C Lockout API - Application Deployment ==="
echo "App Name: $APP_NAME"
echo "Resource Group: $RESOURCE_GROUP"
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "Error: Azure CLI is not installed. Please install it first."
    exit 1
fi

# Check if .NET SDK is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK is not installed. Please install it first."
    exit 1
fi

# Check if the web app exists
echo "1. Verifying Azure resources..."
if ! az webapp show --name $APP_NAME --resource-group $RESOURCE_GROUP &> /dev/null; then
    echo "Error: Web app '$APP_NAME' not found in resource group '$RESOURCE_GROUP'"
    echo "Please create the web app first using the full deployment script."
    exit 1
fi

echo "Web app found: $APP_NAME"

# Get the web app URL for verification
WEBAPP_URL=$(az webapp show --name $APP_NAME --resource-group $RESOURCE_GROUP --query "defaultHostName" --output tsv)
echo "Web app URL: https://$WEBAPP_URL"

echo ""
echo "2. Building the application..."
# Restore packages
echo "Restoring packages..."
dotnet restore

# Build the application
echo "Building the application..."
dotnet build -c Release

# Publish the application
echo "Publishing the application..."
dotnet publish -c Release -o ./publish

echo ""
echo "3. Creating deployment package..."
# Create a deployment directory
mkdir -p deploy

# Copy all published files to deploy directory
cp -r publish/* deploy/

# Create a startup script for Azure App Service
cat > deploy/startup.sh << 'EOF'
#!/bin/bash
cd /home/site/wwwroot
exec dotnet ADB2C.Lockout.dll
EOF

chmod +x deploy/startup.sh

# Create web.config for .NET Core on Linux App Service
cat > deploy/web.config << 'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet" arguments="ADB2C.Lockout.dll" stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" hostingModel="inprocess" />
  </system.webServer>
</configuration>
EOF

# Create .deployment file
cat > deploy/.deployment << 'EOF'
[config]
command = dotnet ADB2C.Lockout.dll
EOF

# Verify the DLL exists in the deployment directory
echo "Verifying deployment files..."
ls -la deploy/ | grep -E "(ADB2C\.Lockout\.dll|startup\.sh|web\.config)"

# Zip the deployment package
cd deploy
zip -r ../deploy.zip .
cd ..

echo ""
echo "4. Deploying to Azure..."
# Get deployment credentials
echo "Getting deployment credentials..."
DEPLOYMENT_USER=$(az webapp deployment list-publishing-credentials --name $APP_NAME --resource-group $RESOURCE_GROUP --query publishingUserName --output tsv)
DEPLOYMENT_PASS=$(az webapp deployment list-publishing-credentials --name $APP_NAME --resource-group $RESOURCE_GROUP --query publishingPassword --output tsv)

if [ -z "$DEPLOYMENT_USER" ] || [ -z "$DEPLOYMENT_PASS" ]; then
    echo "Error: Failed to get deployment credentials"
    exit 1
fi

echo "Deployment credentials obtained successfully."

# Deploy using curl with basic auth
echo "Uploading deployment package..."
curl -X POST \
  -u "$DEPLOYMENT_USER:$DEPLOYMENT_PASS" \
  -T deploy.zip \
  "https://$APP_NAME.scm.azurewebsites.net/api/zipdeploy"

if [ $? -eq 0 ]; then
    echo "Deployment successful!"
else
    echo "Error: Deployment failed"
    exit 1
fi

echo ""
echo "5. Testing the deployment..."
echo "Waiting for deployment to complete..."
sleep 30

echo "Testing health endpoint..."
HEALTH_RESPONSE=$(curl -s "https://$WEBAPP_URL/api/monitoring/health")
if [[ $HEALTH_RESPONSE == *"status"* ]] || [[ $HEALTH_RESPONSE == *"healthy"* ]]; then
    echo "✅ Health check passed!"
    echo "Response: $HEALTH_RESPONSE"
else
    echo "⚠️  Health check failed or returned unexpected response"
    echo "Response: $HEALTH_RESPONSE"
    echo "The application may still be starting up. Please wait a few minutes and try again."
fi

echo ""
echo "=== Application Deployment Complete ==="
echo ""
echo "API Endpoints:"
echo "- Health Check: https://$WEBAPP_URL/api/monitoring/health"
echo "- Sign In: https://$WEBAPP_URL/api/identity/signin"
echo "- Statistics: https://$WEBAPP_URL/api/monitoring/stats"
echo "- Account Status: https://$WEBAPP_URL/api/identity/status/{username}"
echo "- Reset Account: https://$WEBAPP_URL/api/identity/reset/{username}"
echo ""
echo "Monitoring Endpoints:"
echo "- All Accounts: https://$WEBAPP_URL/api/monitoring/accounts"
echo "- System Stats: https://$WEBAPP_URL/api/monitoring/stats"
echo "- Unlock Account: https://$WEBAPP_URL/api/monitoring/unlock/{username}"
echo "- Clear All: https://$WEBAPP_URL/api/monitoring/clear-all"
echo ""

# Clean up deployment files
echo "Cleaning up deployment files..."
rm -rf deploy deploy.zip publish

echo "Deployment script completed successfully!" 