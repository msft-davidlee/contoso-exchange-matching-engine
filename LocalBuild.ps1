param([string]$BuildEnvironment)

$ErrorActionPreference = "Stop"

$groups = az group list --tag stack-environment=$BUILD_ENV | ConvertFrom-Json
$sharedResourceGroup = ($groups | Where-Object { $_.tags.'stack-name' -eq 'cntex-shared-services' -and $_.tags.'stack-environment' -eq $BuildEnvironment }).name
$sharedResources = az resource list --resource-group $sharedResourceGroup | ConvertFrom-Json

$matchingEngineResourceGroup = ($groups | Where-Object { $_.tags.'stack-name' -eq 'cntex-matchingengine' -and $_.tags.'stack-environment' -eq $BuildEnvironment }).name
$matchingEngineResources = az resource list --resource-group $matchingEngineResourceGroup | ConvertFrom-Json
if ($matchingEngineResources.Length -eq 0) {
    Write-Host "Matching engine resource group is not yet deployed."
    return
}

# Section: Self discover Switch IO
$switches = az resource list --resource-type "Microsoft.Solutions/applications" --resource-group $matchingEngineResourceGroup | ConvertFrom-Json
if (!$switches -or $switches.Length -eq 0) {
    Write-Host "No third party appliance found"
    return
}

$foundSwitch = $switches | Where-Object { $_.resourceGroup.EndsWith($BuildEnvironment) -and $_.plan.name.Contains("swxtch-trial") }

if (!$foundSwitch -or $foundSwitch.Length -eq 0) {
    Write-Host "No cloudSwXtch appliance found"
    return
}

$MulticastIPAddressValue = "239.5.69.4"
$MulticastPortValue = "9999"
$SwitchIOName = $foundSwitch.name

# Section: Self discover Storage Account
$foundStorageResource = ($sharedResources | Where-Object { $_.type -eq "Microsoft.Storage/storageAccounts" })[0]
$AccountName = $foundStorageResource.name
$ContainerName = "apps"
$end = (Get-Date).AddDays(1).ToString("yyyy-MM-dd")
$start = (Get-Date).ToString("yyyy-MM-dd")

$AccountKey = (az storage account keys list -g $foundStorageResource.resourceGroup -n $AccountName | ConvertFrom-Json)[0].value
$AccountReadSas = (az storage container generate-sas -n $ContainerName --account-name $AccountName --account-key $AccountKey --permissions rl --expiry $end --start $start --https-only | ConvertFrom-Json)
$AccountWriteSas = (az storage container generate-sas -n $ContainerName --account-name $AccountName --account-key $AccountKey --permissions acdlrw --expiry $end --start $start --https-only | ConvertFrom-Json)
$StorageAccountReadSas = "https://$AccountName.blob.core.windows.net/$ContainerName`?$AccountReadSas"
$StorageAccountWriteSas = "https://$AccountName.blob.core.windows.net/$ContainerName`?$AccountWriteSas"

#Section: Self discover App Insights
$foundInsightsResource = ($matchingEngineResources | Where-Object { $_.type -eq "microsoft.insights/components" })[0]
$insightsResource = az resource show --name $foundInsightsResource.name --resource-group $foundInsightsResource.resourceGroup --resource-type "microsoft.insights/components" | ConvertFrom-Json
$InstrumentationKey = $insightsResource.properties.InstrumentationKey

$FolderName = "work"

if (!(Test-Path $FolderName)) {
    New-Item $FolderName -ItemType Directory
}

#Section: Self discover trading platform
$nic = ($matchingEngineResources | Where-Object { $_.type -eq "Microsoft.Network/networkInterfaces" -and $_.name.Contains("$Prefix-trd1-matchingengine") })[0]

$nicCfg = (az network nic ip-config show -g $nic.resourceGroup -n ipconfig1 --nic-name $nic.name | ConvertFrom-Json)
$priIP = $nicCfg.privateIpAddress

#Section start build/publish/copy zipped app bits to storage

Push-Location .\Demo.CustomerOrder
dotnet publish -c Release -o ..\$FolderName\client1
dotnet publish -c Release -o ..\$FolderName\client2
Pop-Location

Push-Location .\Demo.FIXMessageProcessor
dotnet publish -c Release -o ..\$FolderName\fixmsgprocessor
Pop-Location

Push-Location .\Demo.MatchingEngine
dotnet publish -c Release -o ..\$FolderName\matchingengine
Pop-Location

Push-Location .\Demo.MarketDataRecipient
dotnet publish -c Release -o ..\$FolderName\marketdata1
dotnet publish -c Release -o ..\$FolderName\marketdata2
Pop-Location

# Setup client 1
$content = Get-Content .\$FolderName\client1\tradeclient.cfg
$content = $content.Replace("SocketConnectHost=127.0.0.1", "SocketConnectHost=$priIP")
Set-Content -Value $content -Path .\$FolderName\client1\tradeclient.cfg

# Make sure client 2 is configured differently
$content = Get-Content .\$FolderName\client2\tradeclient.cfg
$content = $content.Replace("SocketConnectHost=127.0.0.1", "SocketConnectHost=$priIP")
$content = $content.Replace("SocketConnectPort=5001", "SocketConnectPort=5002")
$content = $content.Replace("SenderCompID=CLIENT1", "SenderCompID=CLIENT2")
Set-Content -Value $content -Path .\$FolderName\client2\tradeclient.cfg

if ($InstrumentationKey) {

    $content = Get-Content .\$FolderName\client1\appsettings.json
    $content = $content.Replace("00000000-0000-0000-0000-000000000000", $InstrumentationKey)
    $content = $content.Replace("DemoClientName", "ClientSeller")
    Set-Content -Value $content -Path .\$FolderName\client1\appsettings.json

    $content = Get-Content .\$FolderName\client2\appsettings.json
    $content = $content.Replace("00000000-0000-0000-0000-000000000000", $InstrumentationKey)
    $content = $content.Replace("DemoClientName", "ClientBuyer")
    Set-Content -Value $content -Path .\$FolderName\client2\appsettings.json

    $content = Get-Content .\$FolderName\fixmsgprocessor\appsettings.json
    $content = $content.Replace("00000000-0000-0000-0000-000000000000", $InstrumentationKey)
    Set-Content -Value $content -Path .\$FolderName\fixmsgprocessor\appsettings.json

    $content = Get-Content .\$FolderName\matchingengine\appsettings.json
    $content = $content.Replace("00000000-0000-0000-0000-000000000000", $InstrumentationKey)

    if ($MulticastIPAddressValue) {        
        $content = $content.Replace("MulticastIPAddressValue", $MulticastIPAddressValue)
    }

    if ($MulticastPortValue) {        
        $content = $content.Replace("MulticastPortValue", $MulticastPortValue)
    }

    Set-Content -Value $content -Path .\$FolderName\matchingengine\appsettings.json

    $content = Get-Content .\$FolderName\marketdata1\appsettings.json
    $content = $content.Replace("00000000-0000-0000-0000-000000000000", $InstrumentationKey)
    $content = $content.Replace("DemoMarketDataRecipientClientName", "MarketDataRecipientClient1")

    if ($MulticastIPAddressValue) {        
        $content = $content.Replace("MulticastIPAddressValue", $MulticastIPAddressValue)
    }

    if ($MulticastPortValue) {        
        $content = $content.Replace("MulticastPortValue", $MulticastPortValue)
    }

    Set-Content -Value $content -Path .\$FolderName\marketdata1\appsettings.json

    $content = Get-Content .\$FolderName\marketdata2\appsettings.json
    $content = $content.Replace("00000000-0000-0000-0000-000000000000", $InstrumentationKey)
    $content = $content.Replace("DemoMarketDataRecipientClientName", "MarketDataRecipientClient2")

    if ($MulticastIPAddressValue) {        
        $content = $content.Replace("MulticastIPAddressValue", $MulticastIPAddressValue)
    }

    if ($MulticastPortValue) {        
        $content = $content.Replace("MulticastPortValue", $MulticastPortValue)
    }

    Set-Content -Value $content -Path .\$FolderName\marketdata2\appsettings.json
}

$current = Get-Date
Write-Host "Deployment completed at $current"

if ($StorageAccountWriteSas -and $StorageAccountReadSas) {

    # Ship bits to storage account
    Push-Location .\$FolderName

    if (!(Test-Path AzCopy.zip)) {
        # Install azcopy
        Invoke-WebRequest -Uri "https://aka.ms/downloadazcopy-v10-windows" -OutFile AzCopy.zip -UseBasicParsing
        Expand-Archive ./AzCopy.zip ./AzCopy -Force
        Get-ChildItem ./AzCopy/*/azcopy.exe | Move-Item -Destination .\AzCopy.exe
    }

    $apps = @("client1", "client2", "fixmsgprocessor", "matchingengine", "marketdata1", "marketdata2")

    $apps | ForEach-Object {
    
        $file = $_

        Write-Host "Processing $file"

        Compress-Archive $file\* -DestinationPath "$file.zip" -Force
        $sas = $StorageAccountWriteSas.Replace("apps?", "apps/$file.zip?")
        .\azcopy.exe cp "$file.zip" $sas --overwrite true
        if ($LastExitCode -ne 0) {
            throw "An error has occured. Copy failed for $file.zip. $sas"
        }
    }
    
    $content = Get-Content -Path "../AppInstall.ps1"    
    $content = $content.Replace("[DownloadSas]", $StorageAccountReadSas)
    $content = $content.Replace("[SwitchIOName]", $SwitchIOName)
    Set-Content -Path "AppInstall.ps1" -Value $content

    $sas = $StorageAccountWriteSas.Replace("apps?", "apps/AppInstall.ps1?")
    .\azcopy.exe cp "AppInstall.ps1" $sas --overwrite true
    if ($LastExitCode -ne 0) {
        throw "An error has occured. Copy failed for AppInstall.ps1. $sas"
    }
    Pop-Location    
}
else {
    Write-Host "Skipping deploy"
}

