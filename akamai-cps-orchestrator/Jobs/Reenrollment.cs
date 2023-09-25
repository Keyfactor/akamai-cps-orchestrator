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
    public class Reenrollment : IReenrollmentJobExtension
    {
        public string ExtensionName => "Akamai";

        private ILogger _logger;

        public JobResult ProcessJob(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
        {
            _logger = LogHandler.GetClassLogger<Reenrollment>();
            var storeProps = JsonConvert.DeserializeObject<Dictionary<string, string>>(jobConfiguration.CertificateStoreDetails.Properties);
            AkamaiAuth auth = new AkamaiAuth(storeProps);
            AkamaiClient client = new AkamaiClient(_logger, jobConfiguration.CertificateStoreDetails.ClientMachine, auth);

            string enrollmentType = jobConfiguration.CertificateStoreDetails.StorePath;
            client.SetDeploymentType(enrollmentType);

            _logger.LogTrace("Populating enrollment request information.");
            CreatedEnrollment enrollment;
            var allJobProps = jobConfiguration.JobProperties; // contains entry parameters, contact addresses - EnrollmentId, ContractId
            string subject = GetRequiredValue(allJobProps, "subjectText");
            string keyType = GetRequiredValue(allJobProps, "keyType");
            string contractId = GetRequiredValue(allJobProps, "ContractId");
            string sans = GetRequiredValue(allJobProps, "Sans"); // ampersand split sans

            _logger.LogDebug($"Reading passed in reenrollment subject: {subject}");
            string[] subjectParams = subject.Split(',');
            var subjectValues = new Dictionary<string, string>();
            foreach (var subjectParam in subjectParams)
            {
                string[] subjectPair = subjectParam.Split('=', 2);
                bool subjectParsedSuccessfully = subjectValues.TryAdd(subjectPair[0].ToUpper(), subjectPair[1]);

                if (!subjectParsedSuccessfully)
                {
                    _logger.LogInformation($"Subject element '{subjectPair[0]}' with value '{subjectPair[1]}' was not included in the Reenrollment subject sent to Akamai.");
                }
            }

            var reenrollment = new Enrollment()
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

            _logger.LogTrace("Loading contact info from job properties.");
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

            _logger.LogTrace("Enrollment request information finished populating.");

            // if not present as an entry parameter, need to make a new enrollment
            string enrollmentId;
            bool enrollmentExists = allJobProps.TryGetValue("EnrollmentId", out object existingEnrollmentId);
            if (enrollmentExists && existingEnrollmentId != null)
            {
                enrollmentId = existingEnrollmentId.ToString();
                _logger.LogDebug($"Looking for existing enrollment - {enrollmentId}");
                Enrollment existingEnrollment;
                try
                {
                    existingEnrollment = client.GetEnrollment(enrollmentId);
                }
                catch
                {
                    _logger.LogError($"Failed to find existing enrollment - {enrollmentId}");
                    throw;
                }

                // use existing enrollment information, with reenrollment CSR data
                existingEnrollment.csr = reenrollment.csr;

                _logger.LogDebug($"Found existing enrollment - {enrollmentId}");
                try
                {
                    enrollment = client.UpdateEnrollment(enrollmentId, existingEnrollment);
                    _logger.LogInformation($"Updated existing enrollment - {enrollmentId}");
                }
                catch (AkamaiClientException e) when (e.ClientErrorCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    // 500 error thrown when trying to "update" existing enrollment that is in deployment process
                    _logger.LogError($"Failed to update existing enrollment - {enrollmentId}");
                    _logger.LogInformation($"Attempting to rollback existing change to restart enrollment");
                    var mostRecentChangeLocation = existingEnrollment.pendingChanges.Last().location;
                    var mostRecentChangeId = mostRecentChangeLocation.Split('/')[^1]; // last element of the location url is the Change Id
                    // delete change and try to update enrollment again
                    _logger.LogDebug($"Deleting pending change {mostRecentChangeId}");
                    client.DeletePendingChange(enrollmentId, mostRecentChangeId);
                    _logger.LogInformation($"Deleted pending change, attempting to update enrollment again");
                    enrollment = client.UpdateEnrollment(enrollmentId, existingEnrollment);
                    _logger.LogInformation($"Updated existing enrollment - {enrollmentId}");
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
                    _logger.LogError($"Enrollment already exists for CN {reenrollment.csr.cn}, cannot create new enrollment.");
                    throw;
                }
                catch
                {
                    _logger.LogError($"Failed to create new enrollment");
                    throw;
                }
                enrollmentId = enrollment.enrollment.Split('/')[^1]; // last element of the location url is the Enrollment Id
                _logger.LogInformation($"Created new enrollment - {enrollmentId}");
            }

            // update the enrollment and get CSR
            string changeId = enrollment.changes[0].Split('/')[^1]; // last element of the location url is the Change Id
            _logger.LogDebug("Retrieving CSR for enrollment change.");

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
                    _logger.LogTrace("Akamai CSR not ready yet. Sleeping process to try again.");
                    retryCount++;
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(30));
                }
            }

            // if no CSR received, fail job
            if (string.IsNullOrEmpty(csr))
            {
                _logger.LogError($"Maximum retry count reached. CSR was not finished processing for new enrollment - {enrollmentId}");
                JobResult errorResult = new JobResult()
                {
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure
                };
                return errorResult;
            }

            _logger.LogDebug("Retrieved CSR content for enrollment change.");

            // submit csr
            _logger.LogTrace("Submitting CSR to Keyfactor");
            var x509Cert = submitReenrollmentUpdate.Invoke(csr);
            _logger.LogDebug("Certificate returned from CSR enrollment.");

            // submit certificate info to deployment
            // build PEM content
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            sb.AppendLine(Convert.ToBase64String(x509Cert.RawData, Base64FormattingOptions.InsertLineBreaks));
            sb.AppendLine("-----END CERTIFICATE-----");
            var certContent = sb.ToString();

            certContent = certContent.Replace("\r", "");

            _logger.LogTrace("Posting certificate to Akamai.");
            client.PostCertificate(enrollmentId, changeId, certContent, keyType);
            _logger.LogInformation($"Certificate uploaded for enrollment - {enrollmentId}");

            // akcnowledge warnings to force deployment
            _logger.LogTrace("Acknowledging warnings for finished enrollment.");
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
                    _logger.LogTrace("Akamai deployment warnings are not processed yet. Sleeping process to try again.");
                    retryCount++;
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(20));
                }
            }
            _logger.LogDebug($"Warnings acknowleged for enrollment - {enrollmentId}");

            JobResult result = new JobResult()
            {
                JobHistoryId = jobConfiguration.JobHistoryId,
                Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success
            };
            return result;
        }

        private string GetRequiredValue(Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key)
                || string.IsNullOrWhiteSpace(dict[key].ToString()))
            {
                string error = $"Requried field {key} was missing a value in the Reenrollment job properties.";
                _logger.LogError(error);
                throw new ArgumentException(error);
            }
            return dict[key].ToString();
        }
    }
}
