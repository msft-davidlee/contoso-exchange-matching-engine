param([string]$BUILD_ENV)
$groups = az group list --tag stack-environment=$BUILD_ENV | ConvertFrom-Json
$resourceGroup = ($groups | Where-Object { $_.tags.'stack-id' -eq 'artifacts-repo' }).name
Write-Host "::set-output name=ResourceGroup::$resourceGroup"