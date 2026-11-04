# Creates the Azure resources for the file-manager app.
# Run commands one at a time. No secrets are stored here: connection strings live only in variables.

$rg      = "rg-interview-rocheli-20261104"
$loc     = "northeurope"   # Azure Policy "Allowed locations" permits only northeurope (westeurope is denied)
$plan    = "asp-filemgr-rocheli1104"
$app     = "app-filemgr-rocheli1104"
$storage = "stfilemgrrocheli1104"
$law     = "log-filemgr-rocheli1104"
$appi    = "appi-filemgr-rocheli1104"

# 1. Confirm subscription and resource group
az account show -o table
az group show -n $rg -o table

# 2. Confirm .NET 10 runtime is available on Linux App Service
az webapp list-runtimes --os linux -o tsv | Select-String "DOTNETCORE"

# 3. Storage account (private, TLS 1.2)
az storage account create -g $rg -n $storage -l $loc `
  --kind StorageV2 --sku Standard_LRS --access-tier Hot `
  --min-tls-version TLS1_2 --allow-blob-public-access false

# 4. Storage connection string -> variable only (never echo or write to disk)
$storageConn = az storage account show-connection-string -g $rg -n $storage --query connectionString -o tsv

# 5. Blob container for uploaded files
az storage container create -n files --connection-string $storageConn

# 6. Log Analytics workspace
az monitor log-analytics workspace create -g $rg -n $law -l $loc

# 7. Workspace-based Application Insights (auto-installs the az extension)
az config set extension.use_dynamic_install=yes_without_prompt
$wsId = az monitor log-analytics workspace show -g $rg -n $law --query id -o tsv
az monitor app-insights component create -g $rg -a $appi -l $loc `
  --kind web --application-type web --workspace $wsId
$aiConn = az monitor app-insights component show -g $rg -a $appi --query connectionString -o tsv

# 8. Linux App Service plan (Free F1; fall back to B1 only if F1 fails)
az appservice plan create -g $rg -n $plan -l $loc --is-linux --sku F1
# az appservice plan create -g $rg -n $plan -l $loc --is-linux --sku B1

# 9. Web app on .NET 10
az webapp create -g $rg -p $plan -n $app --runtime "DOTNETCORE:10.0"

# 10. App settings (connection string instead of Managed Identity: no role-assignment rights here)
az webapp config appsettings set -g $rg -n $app --settings `
  "Storage__ConnectionString=$storageConn" `
  "APPLICATIONINSIGHTS_CONNECTION_STRING=$aiConn" `
  --query "[].name" -o tsv

# 11. Allow SCM basic auth so GitHub Actions can deploy with a publish profile
az resource update -g $rg --namespace Microsoft.Web --resource-type basicPublishingCredentialsPolicies `
  --parent sites/$app --name scm --set properties.allow=true

# Verify
az resource list -g $rg -o table
az storage container exists -n files --connection-string $storageConn
