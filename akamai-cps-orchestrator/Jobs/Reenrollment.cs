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

using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Keyfactor.Logging;
using System.Linq;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Jobs
{
    public class Reenrollment : AkamaiJob, IReenrollmentJobExtension
    {
        public string ExtensionName => "Akamai";

        public JobResult ProcessJob(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
        {
            JobHistoryId = jobConfiguration.JobHistoryId;
            ILogger logger = LogHandler.GetClassLogger<Reenrollment>();
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

            logger.LogTrace("Populating enrollment request information.");
            Enrollment reenrollment;
            string contractId, keyType;
            var allJobProps = jobConfiguration.JobProperties; // contains entry parameters, contact addresses - EnrollmentId, ContractId
            try
            {
                string subject = GetRequiredValue(allJobProps, "subjectText");
                keyType = GetRequiredValue(allJobProps, "keyType");
                contractId = GetRequiredValue(allJobProps, "ContractId");
                string sans = GetRequiredValue(allJobProps, "Sans"); // ampersand split sans

                logger.LogDebug($"Reading passed in reenrollment subject: {subject}");
                string[] subjectParams = subject.Split(',');
                var subjectValues = new Dictionary<string, string>();
                foreach (var subjectParam in subjectParams)
                {
                    string[] subjectPair = subjectParam.Split('=', 2);
                    bool subjectParsedSuccessfully = subjectValues.TryAdd(subjectPair[0].ToUpper(), subjectPair[1]);

                    if (!subjectParsedSuccessfully)
                    {
                        logger.LogInformation($"Subject element '{subjectPair[0]}' with value '{subjectPair[1]}' was not included in the Reenrollment subject sent to Akamai.");
                    }
                }

                reenrollment = new Enrollment()
                {
                    csr = new EnrollmentCSR()
                    {
                        // retrieve subject values by uppercase name
                        cn = subjectValues.GetValueOrDefault("CN"),
                        c = subjectValues.GetValueOrDefault("C"),
                        l = subjectValues.GetValueOrDefault("L"),
                        o = subjectValues.GetValueOrDefault("O"),
                        ou = subjectValues.GetValueOrDefault("OU"),
                        st = subjectValues.GetValueOrDefault("ST"),
                        // split sans entered
                        sans = sans.Split('&')
                    }
                };

                logger.LogTrace("Loading contact info from job properties.");
                reenrollment.adminContact = new ContactInfo()
                {
                    addressLineOne = GetRequiredValue(allJobProps, "admin-addressLineOne"),
                    addressLineTwo = allJobProps["admin-addressLineTwo"]?.ToString(),
                    city = GetRequiredValue(allJobProps, "admin-city"),
                    country = GetRequiredValue(allJobProps, "admin-country"),
                    email = GetRequiredValue(allJobProps, "admin-email"),
                    firstName = GetRequiredValue(allJobProps, "admin-firstName"),
                    lastName = GetRequiredValue(allJobProps, "admin-lastName"),
                    organizationName = GetRequiredValue(allJobProps, "admin-organizationName"),
                    phone = GetRequiredValue(allJobProps, "admin-phone"),
                    postalCode = GetRequiredValue(allJobProps, "admin-postalCode"),
                    region = GetRequiredValue(allJobProps, "admin-region"),
                    title = GetRequiredValue(allJobProps, "admin-title")
                };
                reenrollment.org = new ContactInfo()
                {
                    addressLineOne = GetRequiredValue(allJobProps, "org-addressLineOne"),
                    addressLineTwo = allJobProps["org-addressLineTwo"]?.ToString(),
                    city = GetRequiredValue(allJobProps, "org-city"),
                    country = GetRequiredValue(allJobProps, "org-country"),
                    name = GetRequiredValue(allJobProps, "org-organizationName"),
                    phone = GetRequiredValue(allJobProps, "org-phone"),
                    postalCode = GetRequiredValue(allJobProps, "org-postalCode"),
                    region = GetRequiredValue(allJobProps, "org-region")
                };
                reenrollment.techContact = new ContactInfo()
                {
                    addressLineOne = GetRequiredValue(allJobProps, "tech-addressLineOne"),
                    addressLineTwo = allJobProps["tech-addressLineTwo"]?.ToString(),
                    city = GetRequiredValue(allJobProps, "tech-city"),
                    country = GetRequiredValue(allJobProps, "tech-country"),
                    email = GetRequiredValue(allJobProps, "tech-email"),
                    firstName = GetRequiredValue(allJobProps, "tech-firstName"),
                    lastName = GetRequiredValue(allJobProps, "tech-lastName"),
                    organizationName = GetRequiredValue(allJobProps, "tech-organizationName"),
                    phone = GetRequiredValue(allJobProps, "tech-phone"),
                    postalCode = GetRequiredValue(allJobProps, "tech-postalCode"),
                    region = GetRequiredValue(allJobProps, "tech-region"),
                    title = GetRequiredValue(allJobProps, "tech-title")
                };
            }
            catch (Exception e)
            {
                logger.LogError("Error occurred while creating Enrollment object from job parameters.");
                string errorMessage = FlattenException(e);
                logger.LogError(errorMessage);
                return Failure(errorMessage);
            }
            logger.LogTrace("Enrollment request information finished populating.");

            // if not present as an entry parameter, need to make a new enrollment
            CreatedEnrollment enrollment;
            string enrollmentId;
            bool enrollmentExists = allJobProps.TryGetValue("EnrollmentId", out object existingEnrollmentId);
            if (enrollmentExists && existingEnrollmentId != null)
            {
                enrollmentId = existingEnrollmentId.ToString();
                logger.LogDebug($"Looking for existing enrollment - {enrollmentId}");
                Enrollment existingEnrollment;
                try
                {
                    existingEnrollment = client.GetEnrollment(enrollmentId);
                }
                catch (Exception e)
                {
                    logger.LogError($"Failed to find existing enrollment - {enrollmentId}");
                    string errorMessage = FlattenException(e);
                    logger.LogError(errorMessage);
                    return Failure(errorMessage);
                }

                // use existing enrollment information, with reenrollment CSR data
                existingEnrollment.csr = reenrollment.csr;

                logger.LogDebug($"Found existing enrollment - {enrollmentId}");
                try
                {
                    enrollment = client.UpdateEnrollment(enrollmentId, existingEnrollment);
                    logger.LogInformation($"Updated existing enrollment - {enrollmentId}");
                }
                catch (AkamaiClientException e) when (e.ClientErrorCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    // 500 error thrown when trying to "update" existing enrollment that is in deployment process
                    logger.LogError($"Failed to update existing enrollment - {enrollmentId}");
                    logger.LogInformation($"Attempting to rollback existing change to restart enrollment");
                    try
                    {
                        var mostRecentChangeLocation = existingEnrollment.pendingChanges.Last().location;
                        var mostRecentChangeId = mostRecentChangeLocation.Split('/')[^1]; // last element of the location url is the Change Id
                        // delete change and try to update enrollment again
                        logger.LogDebug($"Deleting pending change {mostRecentChangeId}");
                        // TODO: detect when pending only changes to log warning? or always log warning?
                        client.DeletePendingChange(enrollmentId, mostRecentChangeId);
                        logger.LogInformation($"Deleted pending change, attempting to update enrollment again");
                        enrollment = client.UpdateEnrollment(enrollmentId, existingEnrollment);  // THIS DOES NOT WORK, ENROLLMENT IS GONE FOR STAGING ONLY CHANGES
                        logger.LogInformation($"Updated existing enrollment - {enrollmentId}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Error occurred while trying to rollback existing change and restart enrollment.");
                        string errorMessage = FlattenException(ex);
                        logger.LogError(errorMessage);
                        return Failure(errorMessage);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError($"Failed to update existing enrollment - {enrollmentId}");
                    string errorMessage = FlattenException(e);
                    logger.LogError(errorMessage);
                    return Failure(errorMessage);
                }
            }
            else
            {
                // no existing enrollment, create a new one
                try
                {
                    enrollment = client.CreateEnrollment(reenrollment, contractId);
                }
                catch (AkamaiClientException e) when (e.ClientErrorCode == System.Net.HttpStatusCode.Conflict)
                {
                    logger.LogError($"Enrollment already exists for CN {reenrollment.csr.cn}, cannot create new enrollment.");
                    string errorMessage = FlattenException(e);
                    logger.LogError(errorMessage);
                    return Failure(errorMessage);
                }
                catch (Exception e)
                {
                    logger.LogError($"Failed to create new enrollment");
                    string errorMessage = FlattenException(e);
                    logger.LogError(errorMessage);
                    return Failure(errorMessage);
                }
                enrollmentId = enrollment.enrollment.Split('/')[^1]; // last element of the location url is the Enrollment Id
                logger.LogInformation($"Created new enrollment - {enrollmentId}");
            }

            // update the enrollment and get CSR
            string changeId = enrollment.changes[0].Split('/')[^1]; // last element of the location url is the Change Id
            logger.LogDebug("Retrieving CSR for enrollment change.");

            //
            string csr = null;  
            int retryCount = 0; // track retry count for getting CSR
            // need to delay / wait with a 404 until CSR is processed in Akamai
            while (string.IsNullOrEmpty(csr) && retryCount < 5)
            {
                try
                {
                    csr = client.GetCSR(enrollmentId, changeId, keyType);
                }
                catch (AkamaiClientException e) when (e.ClientErrorCode == System.Net.HttpStatusCode.NotFound)
                {
                    // wait 30 seconds before checking for processed CSR again
                    logger.LogTrace("Akamai CSR not ready yet. Sleeping process to try again.");
                    retryCount++;
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(30));
                }
                catch (Exception e)
                {
                    logger.LogError("Error ocurred when trying to retrieve CSR. Error appears unrelated to CSR not being ready yet.");
                    string errorMessage = FlattenException(e);
                    logger.LogError(errorMessage);
                    return Failure(errorMessage);
                }
            }

            // if no CSR received, fail job
            if (string.IsNullOrEmpty(csr))
            {
                string errorMessage = $"Maximum retry count reached. CSR was not finished processing for new enrollment - {enrollmentId}";
                logger.LogError(errorMessage);
                return Failure(errorMessage);
            }

            logger.LogDebug("Retrieved CSR content for enrollment change.");

            // submit csr
            logger.LogTrace("Submitting CSR to Keyfactor");
            var x509Cert = submitReenrollmentUpdate.Invoke(csr);
            logger.LogDebug("Certificate returned from CSR enrollment.");

            if (x509Cert == null)
            {
                string errorMessage = "Certificate was not returned from Keyfactor for submitted CSR.";
                logger.LogError(errorMessage);
                logger.LogTrace($"Failed CSR: \n{csr}");
                return Failure(errorMessage);
            }

            // submit certificate info to deployment
            // build PEM content
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            sb.AppendLine(Convert.ToBase64String(x509Cert.RawData, Base64FormattingOptions.InsertLineBreaks));
            sb.AppendLine("-----END CERTIFICATE-----");
            var certContent = sb.ToString();

            certContent = certContent.Replace("\r", "");

            logger.LogTrace("Posting certificate to Akamai.");
            try
            {
                client.PostCertificate(enrollmentId, changeId, certContent, keyType);
            }
            catch (Exception e)
            {
                string errorMessage = "Error uploading certificate to Akamai.";
                logger.LogError(errorMessage);
                logger.LogError(FlattenException(e));
                logger.LogError($"Certificate of type {keyType} was not uploaded to enrollment {enrollmentId} for change {changeId}");
                logger.LogTrace($"Failed Certificate content: \n{certContent}");
                return Failure(errorMessage);
            }
            logger.LogInformation($"Certificate uploaded for enrollment - {enrollmentId}");

            // akcnowledge warnings to force deployment
            logger.LogTrace("Acknowledging warnings for finished enrollment.");
            retryCount = 0;
            bool ack = false;
            while (!ack && retryCount < 4)
            {
                try
                {
                    client.AcknowledgeWarnings(enrollmentId, changeId);
                    ack = true;
                }
                catch (AkamaiClientException e) when (e.ClientErrorCode == System.Net.HttpStatusCode.NotFound)
                {
                    // wait 30 seconds before checking for warnings again
                    logger.LogTrace("Akamai deployment warnings are not processed yet. Sleeping process to try again.");
                    retryCount++;
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(20));
                }
                catch (Exception e)
                {
                    string errorMessage = "Error ocurred when trying to acknowledge warnings and deploy certificate.";
                    logger.LogError(errorMessage);
                    logger.LogError(FlattenException(e));
                    return Failure(errorMessage);
                }
            }

            if (!ack)
            {
                // all retries used and not successful
                string warnMessage = "Maximum retries reached and the deployment warnings could not be acknowledged. Certificate may not be deployed.";
                logger.LogWarning(warnMessage);
                return Warning(warnMessage);
            }
            else
            {
                logger.LogInformation($"Warnings acknowleged for enrollment - {enrollmentId}");
                return Success();
            }
        }

        private string GetRequiredValue(Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key)
                || string.IsNullOrWhiteSpace(dict[key].ToString()))
            {
                string error = $"Required field {key} was missing a value in the Reenrollment job properties.";
                throw new ArgumentException(error);
            }
            return dict[key].ToString();
        }
    }
}
