param($InstallAppNames)

if (!$InstallAppNames) {
    if (Test-Path "C:\args.txt") {
        $InstallAppNames = Get-Content "C:\args.txt"
    }
    else {
        $InstallAppNames = "client1,client2,fixmsgprocessor,matchingengine,marketdata1,marketdata2"
    }    
}

$ErrorActionPreference = "Stop"

if (!(Test-Path "C:\Program Files\SwXtch.io\Swxtch-xNIC")) {
    Write-Host "Switch IO..."
    Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('http://[SwitchIOName]/services/install/swxtch-xnic-win-install.ps1'))
}

$DownloadSas = "[DownloadSas]"

$workDir = "C:\dev"
if (!(Test-Path $workDir)) {
    mkdir $workDir 
}

Push-Location $workDir

if (!(Test-Path "dotnet-runtime-6.0.5-win-x64.exe")) {
    
    Write-Host "Installing .NET..."

    Invoke-WebRequest -Uri https://download.visualstudio.microsoft.com/download/pr/b395fa18-c53b-4f7f-bf91-6b2d3c43fedb/d83a318111da9e15f5ecebfd2d190e89/dotnet-runtime-6.0.5-win-x64.exe `
        -OutFile dotnet-runtime-6.0.5-win-x64.exe -UseBasicParsing

    .\dotnet-runtime-6.0.5-win-x64.exe /install /quiet
}

# Kill processes before deploying code, in case it's running.
taskkill /IM Demo.CustomerOrder.exe /F
taskkill /IM Demo.FIXMessageProcessor.exe /F
taskkill /IM Demo.MatchingEngine.exe /F
taskkill /IM Demo.MarketDataRecipient.exe /F

# Now we are ready to deploy code
$apps = $InstallAppNames.Split(',')
$apps | ForEach-Object {
    $app = $_
    $sas = $DownloadSas.Replace("apps?", "apps/$app.zip?")
    Invoke-WebRequest -Uri $sas -OutFile "$app.zip" -UseBasicParsing
    Expand-Archive "$app.zip" .\$app -Force 
}

if ($InstallAppNames.Contains("fixmsgprocessor")) {
    New-NetFirewallRule -Name allowFixClient1 -DisplayName 'AllowFixClient1' -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 5001
    New-NetFirewallRule -Name allowFixClient2 -DisplayName 'AllowFixClient2' -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 5002

    $oneTimePath = "$workDir\onetime.txt"
    if (!(Test-Path $oneTimePath)) {
        Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
        iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
                
        reg add HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\w32time\TimeProviders\VMICTimeProvider /v Enabled /t REG_DWORD /d 1 /f
        reg add HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\w32time\TimeProviders\NtpClient /v Enabled /t REG_DWORD /d 0 /f
        net stop w32time
        net start w32time

        Set-Content -Path $oneTimePath -Value "true"
    }
}
else {
    Write-Host "Client install only"
}

$sshWinPath = "C:\Windows\system32\config\systemprofile\AppData\Local\Temp\"
if (!(Test-Path $sshWinPath)) {
    New-Item -ItemType Directory -Force -Path $sshWinPath
}

try {
    # https://docs.microsoft.com/en-us/windows-server/administration/openssh/openssh_install_firstuse
    $state = (Get-WindowsCapability -Name OpenSSH.Server~~~~0.0.1.0 -Online).State    
}
catch {
    # May get this error which we should retry: Ensure that the path to the temporary folder exists and that you have Read/Write permissions on the folder.
    # C:\Windows\system32\config\systemprofile\AppData\Local\Temp\ could not be created.
    Write-Host "An error occured while detecting SSH. Trying again."
    $state = (Get-WindowsCapability -Name OpenSSH.Server~~~~0.0.1.0 -Online).State
}

if ($state -ne "Installed") {
    Write-Host "Installing SSH"
    Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
    Set-Service -Name sshd -StartupType 'Automatic'
    Start-Service sshd
    New-NetFirewallRule -Name sshd -DisplayName 'OpenSSH Server (sshd)' -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22
    Stop-Service sshd

    $PubKeyFile = "pub"
    $sas = $DownloadSas.Replace("certs?", "certs/$PubKeyFile.txt?")
    Invoke-WebRequest -Uri $sas -OutFile "$PubKeyFile.txt" -UseBasicParsing
    Copy-Item -Path "$PubKeyFile.txt" -Destination "C:\ProgramData\ssh\administrators_authorized_keys" -Force

    $content = Get-Content "C:\ProgramData\ssh\sshd_config"
    $content = $content.Replace("#PubkeyAuthentication yes", "PubkeyAuthentication yes")
    $content = $content.Replace("#PasswordAuthentication yes", "PasswordAuthentication no")
    $content = $content.Replace("#StrictModes yes", "StrictModes no")

    Set-Content -Path "C:\ProgramData\ssh\sshd_config" -Value $content

    Start-Service sshd
}
else {
    Write-Host "SSH already installed"
}

Pop-Location

if ((Get-Service -Name swXtchNIC).Status -ne "Running") {
    throw "Dependency is not running!"
}