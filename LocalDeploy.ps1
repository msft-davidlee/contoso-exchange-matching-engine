
param([string]$BuildEnvironment, [string]$DeployApp)

function GetResource([string]$stackId, [string]$stackEnvironment) {
    $platformRes = (az resource list --tag stack-id=$stackName | ConvertFrom-Json)
    if (!$platformRes) {
        throw "Unable to find eligible $stackId resource!"
    }
    if ($platformRes.Length -eq 0) {
        throw "Unable to find 'ANY' eligible $stackId resource!"
    }
                
    $res = ($platformRes | Where-Object { $_.tags.'stack-environment' -eq $stackEnvironment })
    if (!$res) {
        throw "Unable to find resource by environment!"
    }
                
    return $res
}

$ErrorActionPreference = "Stop"


$ContainerName = "tmx"
$end = (Get-Date).AddDays(1).ToString("yyyy-MM-dd")
$start = (Get-Date).ToString("yyyy-MM-dd")

# Section: Self discover Storage Account
$foundResources = GetResource -stackId "demo" -stackEnvironment $BuildEnvironment
$foundStorageResource = ($foundResources | Where-Object { $_.type -eq "Microsoft.Storage/storageAccounts" })[0]
$AccountName = $foundStorageResource.name
$foundStorageResource = ((az resource list --name $AccountName | ConvertFrom-Json) | Where-Object { $_.type -eq "Microsoft.Storage/storageAccounts" })[0]

$AccountKey = (az storage account keys list -g $foundStorageResource.resourceGroup -n $AccountName | ConvertFrom-Json)[0].value
$sas = (az storage container generate-sas -n $ContainerName --account-name $AccountName --account-key $AccountKey --permissions rl --expiry $end --start $start --https-only | ConvertFrom-Json)
$InstallAppScript = "https://$AccountName.blob.core.windows.net/$ContainerName/AppInstall.ps1?$sas"

$VMList = az vm list -g $foundStorageResource.resourceGroup --query "[].name" -o tsv 

$VMList | ForEach-Object {
    $VM = $_

    if ($VM.Contains($DeployApp)) {
        if ($VM.Contains("-cli1")) {
            $AppNames = "client1"
        }
    
        if ($VM.Contains("-cli2")) {
            $AppNames = "client2"
        }
    
        if ($VM.Contains("-mkt1")) {
            $AppNames = "marketdata1"
        }
    
        if ($VM.Contains("-mkt2")) {
            $AppNames = "marketdata2"
        }
    
        if ($VM.Contains("-trd1")) {
            $AppNames = "fixmsgprocessor,matchingengine"
        }
             
        az vm run-command invoke --resource-group $foundStorageResource.resourceGroup `
            --scripts "Set-Content -Path C:\args.txt -Value '$AppNames' -Force; Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('$InstallAppScript'))" `
            --command-id RunPowerShellScript --name $VM
    
        if ($LastExitCode -ne 0) {
            throw "An error has occured. Unable to install apps on $VM."
        }
        return
    }    
}