// Copyright 2023 Keyfactor
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
using Microsoft.Extensions.Logging;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Jobs
{
    public class Inventory : AkamaiJob, IInventoryJobExtension
    {
        public string ExtensionName => "Akamai";

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
        {
            JobHistoryId = jobConfiguration.JobHistoryId;
            ILogger logger = LogHandler.GetClassLogger<Inventory>();
            AkamaiClient client;
            string enrollmentType;
            try
            {
                logger.LogTrace("Reading store properties for Akamai auth information.");
                var storeProps = JsonConvert.DeserializeObject<Dictionary<string, string>>(jobConfiguration.CertificateStoreDetails.Properties);
                AkamaiAuth auth = new AkamaiAuth(storeProps);

                logger.LogTrace("Creating Akamai Client.");
                client = new AkamaiClient(logger, jobConfiguration.CertificateStoreDetails.ClientMachine, auth);

                enrollmentType = jobConfiguration.CertificateStoreDetails.StorePath;
                logger.LogTrace($"Setting enrollment type as '{enrollmentType}'");
                client.SetDeploymentType(enrollmentType);
            }
            catch (Exception e)
            {
                logger.LogError("Error occurred while setting up Akamai Client.");
                string errorMessage = FlattenException(e);
                logger.LogError(errorMessage);
                return Failure(errorMessage);
            }

            // look up all enrollments
            var inventory = new List<CurrentInventoryItem>();
            Enrollment[] enrollments;
            try
            {
                enrollments = client.GetEnrollments();
                logger.LogDebug($"Found {enrollments.Length} total enrollments.");
            }
            catch (Exception e)
            {
                logger.LogError("Error occurred getting list of enrollments.");
                string errorMessage = FlattenException(e);
                logger.LogError(errorMessage);
                return Failure(errorMessage);
            }

            // get certificates from each enrollment
            try
            {
                foreach(var enrollment in enrollments)
                {
                    logger.LogDebug($"Attempting to retrieve {enrollmentType} certificate from enrollment {enrollment.id}");
                    CertificateInfo cert = client.GetCertificate(enrollment.id);
                    if (cert != null) // some enrollments found will not have a cert of the same deployment type
                    {
                        logger.LogTrace($"Found certificate for enrollment {enrollment.id} of type {enrollmentType}.");
                        var x509Cert = new X509Certificate2(Encoding.UTF8.GetBytes(cert.certificate));
                        inventory.Add(
                            new CurrentInventoryItem()
                            {
                                Certificates = new string[] { Convert.ToBase64String(x509Cert.Export(X509ContentType.Cert)) },
                                ItemStatus = Orchestrators.Common.Enums.OrchestratorInventoryItemStatus.Unknown,
                                PrivateKeyEntry = false,
                                UseChainLevel = false,
                                Alias = x509Cert.Thumbprint,
                                Parameters = new Dictionary<string, object>
                                {
                                    { "EnrollmentId", enrollment.id }
                                }
                            }
                        );
                    }
                    else
                    {
                        logger.LogTrace($"Enrollment {enrollment.id} did not have a certificate of type {enrollmentType}.");
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError("Error occurred while reading certificates from list of Akamai enrollments.");
                string errorMessage = FlattenException(e);
                logger.LogError(errorMessage);
                return Failure(errorMessage);
            }

            logger.LogInformation($"Inventory result: {enrollments.Length} total enrollments found, with {inventory.Count} certificates inventoried for {enrollmentType} type.");
            bool success = submitInventoryUpdate.Invoke(inventory);
            logger.LogTrace("Inventory results submitted to Keyfactor.");

            if (success)
            {
                return Success();
            }
            else
            {
                string errorMessage = "SubmitInventory Invoke did not report a success.";
                logger.LogError(errorMessage);
                return Failure(errorMessage);
            }
        }
    }
}
