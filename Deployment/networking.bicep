param primary_location string = 'centralus'
param environment string
param prefix string
var priNetworkPrefix = toLower('${prefix}-${primary_location}')

var tags = {
  'stack-name': prefix
  'stack-environment': toLower(replace(environment, '_', ''))
}

var subnets = [
  'default'
  'switchiodata'
  'matchingengine'
]

resource primary_vnet 'Microsoft.Network/virtualNetworks@2021-05-01' = {
  name: '${priNetworkPrefix}-pri-vnet'
  tags: tags
  location: primary_location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [for (subnetName, i) in subnets: {
      name: subnetName
      properties: {
        addressPrefix: '10.0.${i}.0/24'
      }
    }]
  }
}

resource prinsgs 'Microsoft.Network/networkSecurityGroups@2021-05-01' = [for subnetName in subnets: {
  name: '${priNetworkPrefix}-pri-${subnetName}-subnet-nsg'
  location: primary_location
  tags: tags
  properties: {
    securityRules: []
  }
}]

// Note that all changes related to the subnet must be done on this level rathter than
// on the Virtual network resource declaration above because otherwise, the changes
// may be overwritten on this level.

@batchSize(1)
resource associateprinsg 'Microsoft.Network/virtualNetworks/subnets@2021-05-01' = [for (subnetName, i) in subnets: {
  name: '${primary_vnet.name}/${subnetName}'
  properties: {
    addressPrefix: primary_vnet.properties.subnets[i].properties.addressPrefix
    networkSecurityGroup: {
      id: prinsgs[i].id
    }
    serviceEndpoints: [

      {
        service: 'Microsoft.Storage'
        locations: [
          primary_location
        ]
      }
      {
        service: 'Microsoft.KeyVault'
        locations: [
          primary_location
        ]
      }
    ]
  }
}]
