#!/bin/bash

# Azure B2C Lockout API Deployment Script
# This script builds the application locally and deploys it to Azure App Service

# Configuration
RESOURCE_GROUP="Azureadb2c"
APP_NAME="adb2c-lockout-api"
LOCATION="centralindia"
PLAN_NAME="adb2c-lockout-plan"
SKU="B1"

echo "=== Azure B2C Lockout API Deployment ==="
echo "Resource Group: $RESOURCE_GROUP"
echo "App Name: $APP_NAME"
echo "Location: $LOCATION"
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "Error: Azure CLI is not installed. Please install it first."
    echo "Visit: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check if .NET SDK is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK is not installed. Please install it first."
    echo "Visit: https://dotnet.microsoft.com/download"
    exit 1
fi

# Check if logged in to Azure
if ! az account show &> /dev/null; then
    echo "Please log in to Azure..."
    az login
fi

echo "1. Creating Resource Group..."
az group create --name $RESOURCE_GROUP --location $LOCATION

echo "2. Creating App Service Plan..."
az appservice plan create --name $PLAN_NAME --resource-group $RESOURCE_GROUP --sku $SKU --is-linux

echo "3. Creating Web App with supported .NET runtime..."
# Use .NET 8.0 which is supported on Linux App Service
az webapp create --name $APP_NAME --resource-group $RESOURCE_GROUP --plan $PLAN_NAME --runtime "DOTNETCORE:8.0"

echo "4. Configuring Web App..."
# Configure the startup command for Linux App Service
az webapp config set --name $APP_NAME --resource-group $RESOURCE_GROUP --startup-file "dotnet ADB2C.Lockout.dll"

echo "5. Building the application locally..."
# Restore packages
echo "Restoring packages..."
dotnet restore

# Build the application
echo "Building the application..."
dotnet build -c Release

# Publish the application
echo "Publishing the application..."
dotnet publish -c Release -o ./publish

echo "6. Creating deployment package..."
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

echo "7. Deploying to Azure using zip deployment..."
# Get deployment credentials
DEPLOYMENT_USER=$(az webapp deployment list-publishing-credentials --name $APP_NAME --resource-group $RESOURCE_GROUP --query publishingUserName --output tsv)
DEPLOYMENT_PASS=$(az webapp deployment list-publishing-credentials --name $APP_NAME --resource-group $RESOURCE_GROUP --query publishingPassword --output tsv)

echo "Deployment credentials obtained successfully."

# Deploy using curl with basic auth
echo "Uploading deployment package..."
curl -X POST \
  -u "$DEPLOYMENT_USER:$DEPLOYMENT_PASS" \
  -T deploy.zip \
  "https://$APP_NAME.scm.azurewebsites.net/api/zipdeploy"

echo "8. Getting Web App URL..."
WEBAPP_URL=$(az webapp show --name $APP_NAME --resource-group $RESOURCE_GROUP --query "defaultHostName" --output tsv)
echo "Your API is deployed at: https://$WEBAPP_URL"

echo "9. Testing the deployment..."
echo "Testing health endpoint..."
sleep 30  # Wait for deployment to complete
curl -s "https://$WEBAPP_URL/api/monitoring/health" || echo "Health check failed - deployment may still be in progress"

echo ""
echo "=== Deployment Complete ==="
echo ""
echo "Next Steps:"
echo "1. Update your Azure B2C custom policy to use the new API URL"
echo "2. Configure Application Insights for monitoring (optional)"
echo "3. Set up custom domain and SSL certificate (recommended)"
echo "4. Configure authentication for monitoring endpoints (recommended)"
echo ""
echo "API Endpoints:"
echo "- Health Check: https://$WEBAPP_URL/api/monitoring/health"
echo "- Sign In: https://$WEBAPP_URL/api/identity/signin"
echo "- Statistics: https://$WEBAPP_URL/api/monitoring/stats"
echo ""
echo "For cleanup, run: az group delete --name $RESOURCE_GROUP --yes"

# Clean up deployment files
rm -rf deploy deploy.zip publish 
