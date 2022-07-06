# Use this script to deploy Blueprint which will create the necessary resource groups in your environment
# and assigning the Contributor role to the Service principal in those resource groups.

# 1. Versioning is built into the script and you can add a switch to indicate a major or minor change.
# 2. Change notes is based on last git change log.
param(
    [Parameter(Mandatory = $true)][string]$BUILD_ENV,
    [Parameter(Mandatory = $true)][string]$SVC_PRINCIPAL_ID,
    [Parameter(Mandatory = $true)][string]$MY_PRINCIPAL_ID,
    [switch]$Major,
    [switch]$Minor)

   
$ErrorActionPreference = "Stop"
# Short for Contoso Exchange
$PREFIX = "cntex"

if (((az extension list | ConvertFrom-Json) | Where-Object { $_.name -eq "blueprint" }).Length -eq 0) {
    az extension add --upgrade -n blueprint    
}

$blueprintName = "$PREFIX-$BUILD_ENV"

$subscriptionId = (az account show --query id --output tsv)
if ($LastExitCode -ne 0) {
    throw "An error has occured. Subscription id query failed."
}

$outputs = (az deployment sub create --name "deploy-$blueprintName-blueprint" --location 'centralus' --template-file blueprint.bicep `
        --subscription $subscriptionId `
        --parameters stackEnvironment=$BUILD_ENV svcPrincipalId=$SVC_PRINCIPAL_ID myPrincipalId=$MY_PRINCIPAL_ID `
        blueprintName=$blueprintName prefix=$PREFIX | ConvertFrom-Json)

if ($LastExitCode -ne 0) {
    throw "An error has occured. Deployment failed."
}

$msg = (git log --oneline -n 1)

$outputs.properties.outputs.blueprints.value | ForEach-Object {
    $blueprintName = $_.name

    $versions = (az blueprint version list --blueprint-name $blueprintName | ConvertFrom-Json)
    if ($LastExitCode -ne 0) {
        throw "An error has occured. Version query failed for $blueprintName."
    }

    if (!$versions -or $versions.Length -eq 0) {
        $appliedVersion = '0.1'
    }
    else {
        $lastVersions = $versions[$versions.Length - 1].name.Split('.')
        $lastMajor = [int]$lastVersions[0]
        $lastMinor = [int]$lastVersions[1]

        if (!$Major -and !$Minor) {
            $lastMinor += 1
        }
        else {

            if ($Major) {
                $lastMajor += 1
            }

            if ($Minor) {
                $lastMinor += 1
            }
        }
        $appliedVersion = "$lastMajor.$lastMinor"
    }

    $blueprintJson = az blueprint publish --blueprint-name $blueprintName --version $appliedVersion --change-notes $msg --subscription $subscriptionId

    if ($LastExitCode -ne 0) {
        throw "An error has occured. Publish failed."
    }

    $blueprintId = ($blueprintJson | ConvertFrom-Json).id

    $assignmentName = "assign-$blueprintName-$appliedVersion"

    if ($BUILD_ENV -eq "dev") {
        if ($_.allResourcesDoNotDeleteInDev) {
            $lockMode = "AllResourcesDoNotDelete"
        }
        else {
            $lockMode = "None"
        }
    }
    else {
        $lockMode = "AllResourcesDoNotDelete"
    }
   

    az blueprint assignment create --subscription $subscriptionId --name $assignmentName `
        --location centralus --identity-type SystemAssigned --blueprint-version $blueprintId `
        --parameters "{}" --locks-mode $lockMode

    if ($LastExitCode -ne 0) {
        throw "An error has occured. Assignment failed."
    }

    az blueprint assignment wait --subscription $subscriptionId --name $assignmentName --created
    if ($LastExitCode -ne 0) {
        throw "An error has occured. Assignment failed (wait)."
    }
}

# This portion of the script handles the role assignments between the managed identities and shared resources.
$ids = az identity list | ConvertFrom-Json
if ($LastExitCode -ne 0) {
    throw "An error has occured. Identity listing failed."
}

$platformRes = (az resource list --tag stack-name='shared-key-vault' | ConvertFrom-Json)
if (!$platformRes) {
    throw "Unable to find eligible platform resource!"
}

if ($platformRes.Length -eq 0) {
    throw "Unable to find 'ANY' eligible platform resource!"
}

# Platform specific Azure Key Vault as a Shared resource
$akvid = ($platformRes | Where-Object { $_.type -eq 'Microsoft.KeyVault/vaults' -and $_.tags.'stack-environment' -eq $BUILD_ENV }).id

$ids | ForEach-Object {

    $id = $_
    az role assignment create --assignee $id.principalId --role 'Key Vault Secrets User' --scope $akvid
    if ($LastExitCode -ne 0) {
        throw "An error has occured on role assignment."
    }
}