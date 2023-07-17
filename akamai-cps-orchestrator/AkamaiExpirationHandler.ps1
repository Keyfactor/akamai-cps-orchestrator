# // Copyright 2023 Keyfactor
# // Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
# // You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
# // Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
# // WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
# // and limitations under the License.

[hashtable]$context
$Thumb = $context["Thumbprint"]
$CA = $context["CAConfiguration"]
$Template = $context["Template"]

# script variables
#############################################################
$apiUrl = "https://my.keyfactor.instance01/KeyfactorAPI" # update to be Keyfactor API endpoint
$LogDest = "C:\Keyfactor\logs\AkamaiExpirationHandler.log" # the location for the error log file, the script will create this file
#############################################################

Function LogWrite($LogString)
{
    Add-Content $LogDest -value $LogString
}

Function GetIdAndSans
{
    try
    {
        $searchURL = $apiUrl + "/certificates/?verbose=1&maxResults=50&page=1&query=Thumbprint%20-eq%20`""+$Thumb+"`""

        $certificateResponse = Invoke-RestMethod `
        -Method Get `
        -Uri $searchUrl `
        -UseDefaultCredentials `
        -ContentType "application/json"

        $certValues = @{}

        $certValues.Id = $certificateResponse.Id
        $certValues.Sans = ""

        foreach($san in $certificateResponse.SubjectAltNameElements)
        {
            if ($san.Type -eq 2) #include only dns sans (type 2)
            {
                if ($cert.Sans)
                {
                    $certValues.Sans = $certValues.Sans + "&" # add ampersand delimiter for additional DNS SANs
                }
                $certValues.Sans = $certValues.Sans + $san.Value
            }
        }

        return $certValues
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

        foreach($storetype in $locationsResponse.Details)
        {
            if ($storetype.StoreType -match "Akamai")
            {
                return $storetype.Locations # array of all akamai locations this cert is in
            }
        }
    }
    catch
    {
        LogWrite "An error occurred looking up the certificate locations in Keyfactor"
        LogWrite $_
        return "LOCATIONS_ERROR"
    }
}

Function GetOrchestratorId($storeId)
{
    try
    {
        $storeURL = $apiUrl + "/certificatestores/" + $storeId
        $storeResponse = Invoke-RestMethod `
        -Method Get `
        -Uri $storeURL `
        -UseDefaultCredentials `
        -ContentType "application/json"

        return $storeResponse.AgentId
    }
    catch
    {
        LogWrite "An error occurred while retrieving the Orchestrator ID of the store with id: " + $storeId
        LogWrite $_
        return "GET_ORCHESTRATORID_ERROR"
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

Function FindInventoryParameters($inventoryList)
{
    try
    {
        $parsedResults = @{}
        # search through the store inventory list for matching cert
        foreach($inventoryItem in $inventoryList)
        {
            if ($inventoryItem.Name -eq $Thumb)
            {
                # get cert subject
                foreach ($cert in $inventoryItem.Certificates)
                {
                    if ($cert.Id -eq $CertId)
                    {
                        $parsedResults.Subject = $cert.IssuedDN
                    }
                }
                $parsedResults.Parameters = $inventoryItem.Parameters
                return $parsedResults
            }
        }
    }
    catch
    {
        LogWrite "An error occurred while parsing the job parameters from the cert store inventory"
        LogWrite $_
        return "PARSE_INVENTORY_ERROR"
    }
}

Function ScheduleReenrollment($storeId, $orchId, $inventoryList, $sans)
{
    try
    {        
        # parse inventory parameters for reenrollment, and get subject
        $reenrollmentParameters = FindInventoryParameters($inventoryList)
        LogWrite "Parsed Subject: " 
        $Subject = $reenrollmentParameters.Subject
        LogWrite $Subject
        LogWrite "Parsed Inventory Parameters"
        $Parameters = $reenrollmentParameters.Parameters

        # add sans, which are not returned on inventory
        $Parameters.Sans = $sans
        LogWrite $Parameters

        # escape backslashes in CA name
        $escapedCA = $CA -replace '\\', '\\'

        # convert Parameters object to Json
        $paramsJSON = $Parameters | ConvertTo-Json

        # get Orchestrator Agent Id for scheduling reenrollment

        $headers = New-Object "System.Collections.Generic.Dictionary[[String],[String]]"
        $headers.Add('content-type', 'application/json')
        $headers.Add("X-Keyfactor-Requested-With", "APIClient")
        $body = @"
{
  "KeystoreId": "$storeId",
  "SubjectName": "$Subject",
  "AgentGuid": "$orchId",
  "Alias": "$Thumb",
  "JobProperties": $paramsJson,
  "CertificateAuthority": "$escapedCA",
  "CertificateTemplate": "$Template"
}
"@
        LogWrite $body
        $reenrollmentURL = $apiUrl + "/certificatestores/reenrollment"
        $reenrollmentResponse = Invoke-RestMethod `
        -Method Post `
        -Uri $reenrollmentURL `
        -Headers $headers `
        -UseDefaultCredentials `
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
    # get CertID and Sans
    $CertValues = GetIdAndSans
    $CertId = $CertValues.Id
    $CertSans = $CertValues.Sans

    LogWrite $CertId
    LogWrite $CertSans

            
    # get cert locations
    $Locations = GetLocations($CertId)
    LogWrite "Retrieved Locations"

    # process for each store location this cert is in
    foreach($Location in $Locations)
    {
        $StoreId = $Location.StoreId

        # get Orchestrator Id for store
        $OrchestratorId = GetOrchestratorId $StoreId

        # get inventory of location
        $InventoryList = GetStoreInventory $StoreId
        LogWrite "Got Store Inventory Parameters"
        LogWrite $InventoryList

        # schedule reenrollment with parameters
        ScheduleReenrollment $StoreId $OrchestratorId $InventoryList $CertSans
        LogWrite "Scheduled Reenrollment"
    }
}
catch
{
    LogWrite (Get-Date).ToUniversalTime().ToString()
    LogWrite "Script Failed Gracefully"
}