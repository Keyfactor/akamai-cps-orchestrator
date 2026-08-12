// Copyright 2026 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Factories;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Jobs
{
    public class Inventory : AkamaiJob, IInventoryJobExtension
    {
        public string ExtensionName => "Akamai";
        
        private readonly ILogger _logger;
        private readonly IAkamaiClientFactory _akamaiClientFactory;

        // default constructor for production use
        public Inventory()
        {
            _logger = LogHandler.GetClassLogger<Inventory>();
            _akamaiClientFactory = new AkamaiClientFactory();
        }

        // constructor for dependency injection of logger, to allow for better logging in unit tests
        public Inventory(ILogger logger, IAkamaiClientFactory akamaiClientFactory)
        {
            _logger = logger;
            _akamaiClientFactory = akamaiClientFactory;
        }

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
        {
            JobHistoryId = jobConfiguration.JobHistoryId;
            IAkamaiClient client;
            string enrollmentType;
            try
            {
                _logger.LogTrace("Reading store properties for Akamai auth information.");
                var storeProps = JsonConvert.DeserializeObject<Dictionary<string, string>>(jobConfiguration.CertificateStoreDetails.Properties);

                _logger.LogTrace("Creating Akamai Client.");

                enrollmentType = jobConfiguration.CertificateStoreDetails.StorePath;
                client = _akamaiClientFactory.Create(_logger, storeProps, jobConfiguration.CertificateStoreDetails.ClientMachine, enrollmentType);
                
                _logger.LogDebug("Successfully created Akamai client");
            }
            catch (Exception e)
            {
                _logger.LogError("Error occurred while setting up Akamai Client.");
                string errorMessage = FlattenException(e);
                _logger.LogError(errorMessage);
                return Failure(errorMessage);
            }

            // look up all enrollments
            var inventory = new List<CurrentInventoryItem>();
            Enrollment[] enrollments;
            try
            {
                enrollments = client.GetEnrollments();
                _logger.LogDebug($"Found {enrollments.Length} total enrollments.");
            }
            catch (Exception e)
            {
                _logger.LogError("Error occurred getting list of enrollments.");
                string errorMessage = FlattenException(e);
                _logger.LogError(errorMessage);
                return Failure(errorMessage);
            }

            // get certificates from each enrollment
            try
            {
                foreach(var enrollment in enrollments)
                {
                    _logger.LogDebug($"Attempting to retrieve {enrollmentType} certificate from enrollment {enrollment.id}");
                    CertificateInfo cert = client.GetCertificate(enrollment.id);
                    if (cert != null) // some enrollments found will not have a cert of the same deployment type
                    {
                        _logger.LogTrace($"Found certificate for enrollment {enrollment.id} of type {enrollmentType}.");
                        var x509Cert = new X509Certificate2(Encoding.UTF8.GetBytes(cert.certificate));
                        
                        _logger.LogTrace($"Built x509 certificate for enrollment {enrollment.id}. Building entry parameters from enrollment data");
                        
                        var entryParameters = BuildEntryParameters(enrollment);

                        var alias = x509Cert.Thumbprint;
                        
                        _logger.LogDebug($"Adding enrollment {enrollment.id} to inventory with alias {alias} and {entryParameters.Count} entry parameters.");
                        
                        inventory.Add(
                            new CurrentInventoryItem()
                            {
                                Certificates = new string[] { Convert.ToBase64String(x509Cert.Export(X509ContentType.Cert)) },
                                ItemStatus = Orchestrators.Common.Enums.OrchestratorInventoryItemStatus.Unknown,
                                PrivateKeyEntry = false,
                                UseChainLevel = false,
                                Alias = x509Cert.Thumbprint,
                                Parameters = entryParameters,
                            }
                        );
                    }
                    else
                    {
                        _logger.LogTrace($"Enrollment {enrollment.id} did not have a certificate of type {enrollmentType}.");
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error occurred while reading certificates from list of Akamai enrollments.");
                string errorMessage = FlattenException(e);
                _logger.LogError(errorMessage);
                return Failure(errorMessage);
            }

            _logger.LogInformation($"Inventory result: {enrollments.Length} total enrollments found, with {inventory.Count} certificates inventoried for {enrollmentType} type.");
            bool success = submitInventoryUpdate.Invoke(inventory);
            _logger.LogTrace("Inventory results submitted to Keyfactor.");

            if (success)
            {
                return Success();
            }
            else
            {
                string errorMessage = "SubmitInventory Invoke did not report a success.";
                _logger.LogError(errorMessage);
                return Failure(errorMessage);
            }
        }

        private string MapSecureNetworkToCommandValue(Enrollment enrollment)
        {
            string secureNetwork = enrollment.networkConfiguration.secureNetwork;
            switch (secureNetwork)
            {
                case Constants.DeploymentNetwork.Akamai.StandardTLS:
                    return Constants.DeploymentNetwork.Command.StandardTLS;
                case Constants.DeploymentNetwork.Akamai.EnhancedTLS:
                    return Constants.DeploymentNetwork.Command.EnhancedTLS;
                default:
                    _logger.LogError($"SecureNetwork value {secureNetwork} on enrollment ID {enrollment.id} not recognized.");
                    throw new ArgumentException(
                        $"Could not map SecureNetwork value '{secureNetwork}' on enrollment ID {enrollment.id} to a valid deployment-network value");
            }
        }

        private Dictionary<string, object> BuildEntryParameters(Enrollment enrollment)
        {
            _logger.LogTrace($"Building entry parameters for enrollment {enrollment.id}");
            
            var parameters = new Dictionary<string, object>
            {
                { Constants.EntryParameters.EnrollmentId, enrollment.id },
                { Constants.EntryParameters.DeploymentNetwork, MapSecureNetworkToCommandValue(enrollment) },
                { Constants.EntryParameters.Sans, string.Join("&", enrollment.csr.sans) },
            };

            AddAdminContactParameters(parameters, enrollment.adminContact);
            AddOrgContactParameters(parameters, enrollment.org);
            AddTechContactParameters(parameters, enrollment.techContact);
            
            _logger.LogDebug($"Successfully built entry parameters for {enrollment.id}.");

            return parameters;
        }

        // --- Contact parameter mappings (mirror BuildAdminContact/BuildOrgContact/BuildTechContact in Reenrollment.cs) ---

        private void AddAdminContactParameters(Dictionary<string, object> parameters, ContactInfo contact)
        {
            parameters.Add(Constants.EntryParameters.Admin.AddressLineOne, contact.addressLineOne);
            parameters.Add(Constants.EntryParameters.Admin.AddressLineTwo, contact.addressLineTwo);
            parameters.Add(Constants.EntryParameters.Admin.City, contact.city);
            parameters.Add(Constants.EntryParameters.Admin.Country, contact.country);
            parameters.Add(Constants.EntryParameters.Admin.Email, contact.email);
            parameters.Add(Constants.EntryParameters.Admin.FirstName, contact.firstName);
            parameters.Add(Constants.EntryParameters.Admin.LastName, contact.lastName);
            parameters.Add(Constants.EntryParameters.Admin.OrganizationName, contact.organizationName);
            parameters.Add(Constants.EntryParameters.Admin.Phone, contact.phone);
            parameters.Add(Constants.EntryParameters.Admin.PostalCode, contact.postalCode);
            parameters.Add(Constants.EntryParameters.Admin.Region, contact.region);
            parameters.Add(Constants.EntryParameters.Admin.Title, contact.title);
        }

        private void AddOrgContactParameters(Dictionary<string, object> parameters, ContactInfo contact)
        {
            parameters.Add(Constants.EntryParameters.Org.AddressLineOne, contact.addressLineOne);
            parameters.Add(Constants.EntryParameters.Org.AddressLineTwo, contact.addressLineTwo);
            parameters.Add(Constants.EntryParameters.Org.City, contact.city);
            parameters.Add(Constants.EntryParameters.Org.Country, contact.country);
            parameters.Add(Constants.EntryParameters.Org.OrganizationName, contact.name);
            parameters.Add(Constants.EntryParameters.Org.Phone, contact.phone);
            parameters.Add(Constants.EntryParameters.Org.PostalCode, contact.postalCode);
            parameters.Add(Constants.EntryParameters.Org.Region, contact.region);
        }

        private void AddTechContactParameters(Dictionary<string, object> parameters, ContactInfo contact)
        {
            parameters.Add(Constants.EntryParameters.Tech.AddressLineOne, contact.addressLineOne);
            parameters.Add(Constants.EntryParameters.Tech.AddressLineTwo, contact.addressLineTwo);
            parameters.Add(Constants.EntryParameters.Tech.City, contact.city);
            parameters.Add(Constants.EntryParameters.Tech.Country, contact.country);
            parameters.Add(Constants.EntryParameters.Tech.Email, contact.email);
            parameters.Add(Constants.EntryParameters.Tech.FirstName, contact.firstName);
            parameters.Add(Constants.EntryParameters.Tech.LastName, contact.lastName);
            parameters.Add(Constants.EntryParameters.Tech.OrganizationName, contact.organizationName);
            parameters.Add(Constants.EntryParameters.Tech.Phone, contact.phone);
            parameters.Add(Constants.EntryParameters.Tech.PostalCode, contact.postalCode);
            parameters.Add(Constants.EntryParameters.Tech.Region, contact.region);
            parameters.Add(Constants.EntryParameters.Tech.Title, contact.title);
        }
    }
}
