# // Copyright 2023 Keyfactor
# // Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
# // You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
# // Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
# // WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
# // and limitations under the License.

[hashtable]$context
$Thumb = $context["thumb"]
$CA = $context["CAConfiguration"]
$Template = $context["Template"]
$CertLocations = $context["locations"]

# script variables
#############################################################
$apiUrl = "https://dev.dukog.gcc/KeyfactorAPI" # update to be Keyfactor API endpoint
$LogDest = "C:\Keyfactor\logs\AkamaiExpirationHandler.log" # the location for the error log file, the script will create this file
#############################################################

Function LogWrite($LogString)
{
    Add-Content $LogDest -value $LogString
}

Function GetId
{
    try
    {
        $searchURL = $apiUrl + "/certificates/?verbose=1&maxResults=50&page=1&query=Thumbprint%20-eq%20`""+$Thumb+"`""

        $certificateResponse = Invoke-RestMethod `
        -Method Get `
        -Uri $searchUrl `
        -UseDefaultCredentials `
        -ContentType "application/json"

        Return $certificateResponse.Id
    }
    catch
    {
        LogWrite "An error occurred looking up the certificate in Keyfactor"
        LogWrite $_
        return "SEARCH_ERROR"        
    }
}

Function GetLocations($certId)
{
    try
    {
        $locationsURL = $apiUrl + "/certificates/locations/" + $certId
        $locationsResponse = Invoke-RestMethod `
        -Method Get `
        -Uri $locationsURL `
        -UseDefaultCredentials `
        -ContentType "application/json"

        return $locationsResponse
    }
    catch
    {
        LogWrite "An error occurred looking up the certificate locations in Keyfactor"
        LogWrite $_
        return "LOCATIONS_ERROR"
    }
}

Function GetStoreInventory($storeId)
{
    try
    {
        $inventoryURL = $apiUrl + "/certificatestores/" + $storeId + "/inventory"
        $inventoryResponse = Invoke-RestMethod `
        -Method Get `
        -Uri $inventoryURL `
        -UseDefaultCredentials `
        -ContentType "application/json"

        return $inventoryResponse #
    }
    catch
    {
        LogWrite "An error occurred while retrieving the inventory of certs in the store with id: " + $storeId
        LogWrite $_
        return "GET_INVENTORY_ERROR"
    }
}

Function FindInventoryParameters($storeInventoryList, $certId)
{
    try
    {
        # TODO: search through the list for matching cert
        return $storeInventoryList[0].Parameters
    }
    catch
    {
        LogWrite "An error occurred while parsing the job parameters from the cert store inventory"
        LogWrite $_
        return "PARSE_INVENTORY_ERROR"
    }
}

Function ScheduleReenrollment($storeInventoryItem)
{
    try
    {
        $headers = New-Object "System.Collections.Generic.Dictionary[[String],[String]]"
        $headers.Add('content-type', 'application/json')
        $headers.Add("X-Keyfactor-Requested-With", "APIClient")
        $body = @"
{
  "KeystoreId": $storeId,
  "SubjectName": $subject,
  "AgentGuid": $storeOrchId,
  "Alias": $alias,
  "JobProperties": $storeInventoryItem,
  "CertificateAuthority": $CA,
  "CertificateTemplate": $Template
}
"@
        $reenrollmentURL = $apiUrl + "/certificatestores/reenrollment"
        $reenrollmentResponse = Invoke-RestMethod `
        -Method Post `
        -Uri $reenrollmentUrl `
        -Headers $headers `
        -Body $body `
        -ContentType "application/json"

    }
    catch
    {
        LogWrite "An error occurred while scheduling the reenrollment job"
        LogWrite $_
        return "REENROLLMENT_ERROR"
    }
}

try
{
    LogWrite (Get-Date).ToUniversalTime().ToString()
    LogWrite $Thumb
    LogWrite $CA
    LogWrite $Template
    LogWrite $CertLocations
    LogWrite $context
}
catch
{
    LogWrite "An error occurred reading info into the logs"
    LogWrite $_
    return "INFO_ERROR"
}

try
{
    # get CertID
    $CertID = GetId
    LogWrite $CertId        
    # get cert locations
    $Locs = GetLocations($CertId)
    # get inventory of locations
    $InventoryList = GetStoreInventory($Locs.storeId)
    # parse inventory parameters for reenrollment
    $InventoryItemParameters = FindInventoryParameters($InventoryList, $CertId)
    # schedule reenrollment with parameters
    ScheduleReenrollment($InventoryItemParameters)
}
catch
{
    LogWrite (Get-Date).ToUniversalTime().ToString()
    LogWrite "Script Failed Gracefully"
}