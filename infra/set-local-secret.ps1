# Stores the storage connection string in dotnet user-secrets for local development.
# The value is read from Azure into a variable and never printed or written to the repo.
$rg = "rg-interview-rocheli-20261104"
$storage = "stfilemgrrocheli1104"
$project = Join-Path $PSScriptRoot "..\src\FileManager.Api"

$conn = az storage account show-connection-string -g $rg -n $storage -o tsv
if (-not $conn) { throw "Could not read the connection string (are you signed in with az?)" }

dotnet user-secrets set "Storage:ConnectionString" $conn --project $project | Out-Null
Write-Host "Saved Storage:ConnectionString to user-secrets. Keys now stored:"
dotnet user-secrets list --project $project | ForEach-Object { ($_ -split ' = ')[0] }
