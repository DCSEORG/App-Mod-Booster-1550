#!/bin/bash
set -e

# =============================================================================
# deploy.sh - Deploy Expense Management App (without GenAI/Chat)
# =============================================================================
# Prerequisites:
#   - Azure CLI installed and logged in (az login)
#   - jq installed
#   - Python 3 with pip3 available
#   - Resource group must already exist
#
# Usage:
#   1. Update the variables below
#   2. Run: bash deploy.sh
#
# App URL after deployment: https://<app-name>.azurewebsites.net/Index
# =============================================================================

# ─── Variables - UPDATE THESE ─────────────────────────────────────────────────
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"
ADMIN_OBJECT_ID=""   # Your Azure AD Object ID (run: az ad signed-in-user show --query id -o tsv)
ADMIN_LOGIN=""       # Your UPN / email (run: az account show --query user.name -o tsv)
# ──────────────────────────────────────────────────────────────────────────────

if [ -z "$ADMIN_OBJECT_ID" ] || [ -z "$ADMIN_LOGIN" ]; then
  echo "ERROR: Please set ADMIN_OBJECT_ID and ADMIN_LOGIN before running this script."
  echo "  ADMIN_OBJECT_ID: az ad signed-in-user show --query id -o tsv"
  echo "  ADMIN_LOGIN:     az account show --query user.name -o tsv"
  exit 1
fi

echo "================================================"
echo " Expense Management Deployment (no GenAI)"
echo " Resource Group: $RESOURCE_GROUP"
echo "================================================"

# Step 1: Create resource group if it doesn't exist
echo ""
echo "Step 1: Ensuring resource group exists..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

# Step 2: Deploy infrastructure (App Service + SQL)
echo ""
echo "Step 2: Deploying infrastructure (App Service + Azure SQL)..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file infra/main.bicep \
  --parameters \
    location="$LOCATION" \
    adminObjectId="$ADMIN_OBJECT_ID" \
    adminLogin="$ADMIN_LOGIN" \
    deployGenAI=false \
  --query properties.outputs \
  -o json)

echo "Infrastructure deployment complete."

# Extract outputs
APP_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.appServiceName.value')
APP_URL=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.appServiceUrl.value')
SQL_SERVER_FQDN=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.sqlServerFqdn.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.managedIdentityClientId.value')
MANAGED_IDENTITY_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.managedIdentityName.value')

echo "  App Service:     $APP_NAME"
echo "  SQL Server:      $SQL_SERVER_FQDN"
echo "  Identity Name:   $MANAGED_IDENTITY_NAME"
echo "  Identity Client: $MANAGED_IDENTITY_CLIENT_ID"

# Step 3: Configure App Service settings
echo ""
echo "Step 3: Configuring App Service settings..."
az webapp config appsettings set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --settings \
    "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
    "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
    "ConnectionStrings__DefaultConnection=Server=tcp:$SQL_SERVER_FQDN,1433;Database=Northwind;Authentication=Active Directory Managed Identity;User Id=$MANAGED_IDENTITY_CLIENT_ID;" \
  --output none
echo "App Service settings configured."

# Step 4: Wait for SQL to be ready
echo ""
echo "Step 4: Waiting 30 seconds for SQL Server to be ready..."
sleep 30

# Step 5: Add firewall rules
echo ""
echo "Step 5: Configuring SQL firewall rules..."
MY_IP=$(curl -s https://api.ipify.org)
SQL_SERVER_NAME=$(echo "$SQL_SERVER_FQDN" | cut -d'.' -f1)

az sql server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER_NAME" \
  --name "AllowAllAzureIPs" \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0 \
  --output none

az sql server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER_NAME" \
  --name "AllowDeploymentIP" \
  --start-ip-address "$MY_IP" \
  --end-ip-address "$MY_IP" \
  --output none

echo "Firewall rules added (Azure services + $MY_IP)."
echo "Waiting 15 seconds for firewall rules to propagate..."
sleep 15

# Step 6: Install Python packages
echo ""
echo "Step 6: Installing Python packages..."
pip3 install --quiet pyodbc azure-identity

# Step 7: Update Python scripts with actual server/client values
echo ""
echo "Step 7: Updating Python scripts with deployment values..."
sed -i.bak "s/REPLACE_SERVER/$SQL_SERVER_NAME/g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s/REPLACE_SERVER/$SQL_SERVER_NAME/g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s/REPLACE_SERVER/$SQL_SERVER_NAME/g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak

# Step 8: Import database schema
echo ""
echo "Step 8: Importing database schema..."
python3 run-sql.py

# Step 9: Configure database roles for managed identity
echo ""
echo "Step 9: Configuring database roles for managed identity..."
# Use a temporary copy of script.sql to keep the original template intact
cp script.sql script.sql.run
sed -i.bak "s/MANAGED-IDENTITY-NAME/$MANAGED_IDENTITY_NAME/g" script.sql.run && rm -f script.sql.run.bak
# Override SQL_SCRIPT_FILE for this run using env var approach
SQL_SCRIPT_FILE_ORIG=$(grep "^SQL_SCRIPT_FILE" run-sql-dbrole.py | head -1)
sed -i.bak 's|SQL_SCRIPT_FILE = "script.sql"|SQL_SCRIPT_FILE = "script.sql.run"|' run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
python3 run-sql-dbrole.py
# Restore original SQL_SCRIPT_FILE in script
sed -i.bak 's|SQL_SCRIPT_FILE = "script.sql.run"|SQL_SCRIPT_FILE = "script.sql"|' run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
rm -f script.sql.run

# Step 10: Deploy stored procedures
echo ""
echo "Step 10: Deploying stored procedures..."
python3 run-sql-stored-procs.py

# Step 11: Build and deploy application
echo ""
echo "Step 11: Building and deploying application..."
cd app
dotnet publish -c Release -o publish
cd publish
zip -r ../app.zip .
cd ../..

az webapp deploy \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --src-path ./app/app.zip \
  --type zip

echo ""
echo "================================================"
echo " Deployment COMPLETE!"
echo "================================================"
echo " App URL: $APP_URL/Index"
echo " NOTE: Navigate to $APP_URL/Index (not root URL)"
echo " API Docs: $APP_URL/swagger"
echo "================================================"
