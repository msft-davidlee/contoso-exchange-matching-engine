@description('Location for all resources.')
param location string = resourceGroup().location

@description('Admin username')
param adminUsername string = 'devuser'

@description('Admin password')
@secure()
param adminPassword string

@description('Azure deployment environment')
param appEnvironment string

@description('Prefix for resource name')
param prefix string

@description('Matchingengine Subnet resource Id')
param subnetRef1 string

@description('Switch IO Data Subnet resource Id')
param subnetRef2 string

var tags = {
  'stack-name': 'matchingengine'
  'stack-environment': appEnvironment
}

var vmSize = [
  'Standard_D2_v4'
  'Standard_D4s_v3'
]

var subnetName1 = 'matchingengine'
var subnetName2 = 'switchiodata'

var numberOfInstances = 5
var zones = [
  '1'
]
//VM Variables
var vmNamePrefix = [
  '${prefix}-cli1'
  '${prefix}-cli2'
  '${prefix}-trd1'
  '${prefix}-mkt1'
  '${prefix}-mkt2'
]
//App Insights 
var appInsightName = '${prefix}-ins'

// Log Analytics 

var logAnlayticsWorkSpacename = '${prefix}-log'
var retentionInDays = 30
var lasku = 'PerGB2018'

//Storage Acccount to store VM diag logs

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' = {
  name: '${prefix}str'
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

// Trading platform network interface cards
// Trading VM require Public Associated with it. Added a condition to make sure only Trading VM nic should be associated with Public IP
resource nic1 'Microsoft.Network/networkInterfaces@2021-08-01' = [for i in range(0, numberOfInstances): {
  name: '${vmNamePrefix[i]}-${subnetName1}-nic'
  location: location
  tags: tags
  properties: {
    enableAcceleratedNetworking: true
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          privateIPAllocationMethod: 'Dynamic'
          primary: true
          subnet: {
            id: subnetRef1
          }
        }
      }
    ]
  }
}]

resource nic2 'Microsoft.Network/networkInterfaces@2020-06-01' = [for i in range(0, numberOfInstances): {
  name: '${vmNamePrefix[i]}-${subnetName2}-nic'
  location: location
  tags: tags
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig2'
        properties: {
          privateIPAllocationMethod: 'Dynamic'
          subnet: {
            id: subnetRef2
          }
        }
      }

    ]

  }
}]

resource ppg 'Microsoft.Compute/proximityPlacementGroups@2022-03-01' = {
  name: 'demoppg'
  location: location
  properties: {
    proximityPlacementGroupType: 'Standard'
  }
}

// Virtual Machines - Client VM, Trading Platform, Market Data
resource vm_resource 'Microsoft.Compute/virtualMachines@2021-11-01' = [for i in range(0, numberOfInstances): {
  name: vmNamePrefix[i]
  location: location
  tags: tags
  zones: zones
  properties: {
    proximityPlacementGroup: {
      id: ppg.id
    }
    hardwareProfile: {
      vmSize: (i == 2) ? vmSize[1] : vmSize[0]
    }
    osProfile: {
      computerName: vmNamePrefix[i]
      adminUsername: adminUsername
      adminPassword: adminPassword
    }
    storageProfile: {
      imageReference: {
        publisher: 'MicrosoftWindowsServer'
        offer: 'WindowsServer'
        sku: (i == 2) ? '2022-datacenter' : '2022-datacenter-core-smalldisk'
        version: 'latest'
      }
      osDisk: {
        name: '${prefix}-${vmNamePrefix[i]}-os'
        createOption: 'FromImage'
        managedDisk: {
          storageAccountType: 'Standard_LRS'
        }
      }
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: nic1[i].id
          properties: {
            primary: true
          }
        }
        {
          id: nic2[i].id
          properties: {
            primary: false
          }
        }
      ]
    }
  }
}]

// log analytics workspace to ingest platform logs, application telemetry
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2021-06-01' = {
  name: logAnlayticsWorkSpacename
  tags: tags
  location: location
  properties: {
    sku: {
      name: lasku
    }
    retentionInDays: retentionInDays
  }
}

// App Insights resource to collect end to end telemetry of trading application 
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightName
  tags: tags
  location: location
  kind: 'string'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}
