param([string]$BuildEnvironment)

$groups = az group list --tag stack-environment=$BuildEnvironment | ConvertFrom-Json
$matchingEngineResourceGroup = ($groups | Where-Object { $_.tags.'stack-name' -eq 'cntex-matchingengine' -and $_.tags.'stack-environment' -eq $BuildEnvironment }).name
Write-Host "::set-output name=resourceGroup::$matchingEngineResourceGroup"

$networkingResourceGroup = ($groups | Where-Object { $_.tags.'stack-name' -eq 'cntex-networking' -and $_.tags.'stack-environment' -eq $BuildEnvironment }).name
$networkingResources = az resource list --resource-group $networkingResourceGroup | ConvertFrom-Json

$vnet = $networkingResources | Where-Object { $_.type -eq "Microsoft.Network/virtualNetworks" }
$subnetRef1 = (az network vnet subnet show -g $networkingResourceGroup -n "matchingengine" --vnet-name $vnet.name | ConvertFrom-Json).id
Write-Host "::set-output name=subnetRef1::$subnetRef1"
$subnetRef2 = (az network vnet subnet show -g $networkingResourceGroup -n "switchiodata" --vnet-name $vnet.name | ConvertFrom-Json).id
Write-Host "::set-output name=subnetRef2::$subnetRef2"

$sharedResourceGroup = ($groups | Where-Object { $_.tags.'stack-name' -eq 'cntex-shared-services' -and $_.tags.'stack-environment' -eq $BuildEnvironment }).name
$sharedResources = az resource list --resource-group $sharedResourceGroup | ConvertFrom-Json
$kv = $sharedResources | Where-Object { $_.type -eq "Microsoft.KeyVault/vaults" }

$vmPassword = (az keyvault secret show -n "vmpassword" --vault-name $kv.name --query value | ConvertFrom-Json)
Write-Host "::set-output name=vmPassword::$vmPassword"