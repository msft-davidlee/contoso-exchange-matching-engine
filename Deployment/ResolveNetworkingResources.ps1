param(
    [string]$BuildEnvironment)

$ErrorActionPreference = "Stop"
    
# This is the rg where the VNETs should be deployed
$groups = az group list --tag stack-environment=$BuildEnvironment | ConvertFrom-Json
$networkingResourceGroup = ($groups | Where-Object { $_.tags.'stack-name' -eq 'cntex-networking' -and $_.tags.'stack-environment' -eq $BuildEnvironment }).name
Write-Host "::set-output name=resourceGroup::$networkingResourceGroup"