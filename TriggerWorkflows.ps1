#.github\workflows\cdenvironment.yml
#.github\workflows\cdapps.yml

param([switch]$DeplyEnvironment, [switch]$DeplyApps, [string]$Owner, [string]$Branch, [password]$PersonalAccessToken)
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

$headers = @{"Accept" = "application/vnd.github.v3+json"; "Authorization" = "token $PersonalAccessToken" }
$payload = @{ "ref" = "refs/heads/$Branch"; }
$body = $payload | ConvertTo-Json
$uri = "https://api.github.com/repos/$repo/actions/workflows/$yml/dispatches"

Invoke-WebRequest -Uri $uri -Headers $headers -UseBasicParsing -Body $body -Method POST