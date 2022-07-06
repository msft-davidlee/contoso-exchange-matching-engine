#.github\workflows\cdenvironment.yml
#.github\workflows\cdapps.yml

param([switch]$DeplyEnvironment, [switch]$DeplyApps, [string]$Owner, [string]$Branch, [securestring]$PersonalAccessToken)
$ErrorActionPreference = "Stop"
$repo = "$Owner/contoso-exchange-matching-engine"

$yml = ""
if ($DeplyApps) {
    $yml = "cdapps.yml"
}

if ($DeplyEnvironment) {
    $yml = "cdenvironment.yml"
}

if ($yml -eq "") {
    throw "Pass in either the DeplyEnvironment or DeplyApps switch";
}

if (!$Branch) {
    $Branch = git symbolic-ref --short HEAD
}

Write-Host "Branch: $Branch"

$bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($PersonalAccessToken)
$token = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)

$headers = @{"Accept" = "application/vnd.github+json"; "Authorization" = "token $token" }
$payload = @{ "ref" = "refs/heads/$Branch"; }
$body = $payload | ConvertTo-Json
$uri = "https://api.github.com/repos/$repo/actions/workflows/$yml/dispatches"

Write-Host $uri 

$createWorkflow = "https://api.github.com/repos/$repo/actions/workflows/$yml/enable"
Invoke-WebRequest -Uri $createWorkflow -Headers $headers -UseBasicParsing -Body $body -Method PUT
Invoke-WebRequest -Uri $uri -Headers $headers -UseBasicParsing -Body $body -Method POST