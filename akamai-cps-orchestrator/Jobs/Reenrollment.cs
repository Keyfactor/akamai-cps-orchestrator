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

using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Keyfactor.Logging;
using System.Linq;
using Keyfactor.Extensions.Utilities.HttpInterface.Exceptions;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Factories;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Services;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Jobs
{
    public class Reenrollment : AkamaiJob, IReenrollmentJobExtension
    {
        public string ExtensionName => "Akamai";
        
        private readonly ILogger _logger;
        private readonly IAkamaiClientFactory _akamaiClientFactory;
        private readonly ITimerService _timerService;

        // default constructor for production use
        public Reenrollment()
        {
            _logger = LogHandler.GetClassLogger<Reenrollment>();
            _akamaiClientFactory = new AkamaiClientFactory();
            _timerService = new TimerService();
        }

        // constructor for dependency injection of logger, to allow for better logging in unit tests
        public Reenrollment(ILogger logger, IAkamaiClientFactory akamaiClientFactory, ITimerService timerService)
        {
            _logger = logger;
            _akamaiClientFactory = akamaiClientFactory;
            _timerService = timerService;
        }

        public JobResult ProcessJob(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
        {
            JobHistoryId = jobConfiguration.JobHistoryId;
            IAkamaiClient client;
            
            try
            {
                _logger.LogTrace("Reading store properties for Akamai auth information.");
                var storeProps = JsonConvert.DeserializeObject<Dictionary<string, string>>(jobConfiguration.CertificateStoreDetails.Properties);

                _logger.LogTrace("Creating Akamai Client.");

                var enrollmentType = jobConfiguration.CertificateStoreDetails.StorePath;
                client = _akamaiClientFactory.Create(_logger, storeProps,
                    jobConfiguration.CertificateStoreDetails.ClientMachine, enrollmentType);
            }
            catch (Exception e)
            {
                _logger.LogError("Error occurred while setting up Akamai Client.");
                string errorMessage = FlattenException(e);
                _logger.LogError(errorMessage);
                return Failure(errorMessage);
            }

            _logger.LogTrace("Populating enrollment request information.");
            Enrollment reenrollment;
            string contractId, keyType;
            var allJobProps = jobConfiguration.JobProperties; // contains entry parameters, contact addresses - EnrollmentId, ContractId
            try
            {
                string subject = GetRequiredValue(allJobProps, "subjectText");
                keyType = GetRequiredValue(allJobProps, "keyType");
                contractId = GetRequiredValue(allJobProps, "ContractId");
                string sans = GetRequiredValue(allJobProps, "Sans"); // ampersand split sans
                
                _logger.LogDebug($"keyType: {keyType}, contractId: {contractId}, sans: {sans}");

                _logger.LogDebug($"Reading passed in reenrollment subject: {subject}");
                var subjectValues = CertificateSubjectInformation.ParseFromSubjectText(subject);
                _logger.LogDebug($"Parsed subject values: {JsonConvert.SerializeObject(subjectValues)}");

                reenrollment = new Enrollment()
                {
                    csr = new EnrollmentCSR()
                    {
                        // retrieve subject values by uppercase name
                        cn = subjectValues.CommonName,
                        c = subjectValues.CountryRegion,
                        l = subjectValues.CityLocality,
                        o = subjectValues.Organization,
                        ou = subjectValues.OrganizationalUnit,
                        st = subjectValues.StateProvince,
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
                _logger.LogTrace($"Admin Contact added to re-enrollment request.");
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
                _logger.LogTrace($"Org added to re-enrollment request.");
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
                _logger.LogTrace($"Tech Contact added to re-enrollment request.");
            }
            catch (Exception e)
            {
                _logger.LogError("Error occurred while creating Enrollment object from job parameters.");
                string errorMessage = FlattenException(e);
                _logger.LogError(errorMessage);
                return Failure(errorMessage);
            }
            _logger.LogTrace("Enrollment request information finished populating.");

            // if not present as an entry parameter, need to make a new enrollment
            CreatedEnrollment enrollment;
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
                catch (Exception e)
                {
                    _logger.LogError($"Failed to find existing enrollment - {enrollmentId}");
                    string errorMessage = FlattenException(e);
                    _logger.LogError(errorMessage);
                    return Failure(errorMessage);
                }

                // use existing enrollment information, with reenrollment CSR data
                existingEnrollment.csr = reenrollment.csr;

                _logger.LogDebug($"Found existing enrollment - {enrollmentId}");
                try
                {
                    enrollment = client.UpdateEnrollment(enrollmentId, existingEnrollment);
                    _logger.LogInformation($"Updated existing enrollment - {enrollmentId}");
                }
                catch (HttpInterfaceException e) when (e.ErrorCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    // 500 error thrown when trying to "update" existing enrollment that is in deployment process
                    _logger.LogError($"Failed to update existing enrollment - {enrollmentId}");
                    _logger.LogInformation($"Attempting to rollback existing change to restart enrollment");
                    try
                    {
                        var mostRecentChangeLocation = existingEnrollment.pendingChanges.Last().location;
                        var mostRecentChangeId = mostRecentChangeLocation.Split('/')[^1]; // last element of the location url is the Change Id
                        // delete change and try to update enrollment again
                        _logger.LogDebug($"Deleting pending change {mostRecentChangeId}");
                        // TODO: detect when pending only changes to log warning? or always log warning?
                        client.DeletePendingChange(enrollmentId, mostRecentChangeId);
                        _logger.LogInformation($"Deleted pending change, attempting to update enrollment again");
                        enrollment = client.UpdateEnrollment(enrollmentId, existingEnrollment);  // THIS DOES NOT WORK, ENROLLMENT IS GONE FOR STAGING ONLY CHANGES
                        _logger.LogInformation($"Updated existing enrollment - {enrollmentId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error occurred while trying to rollback existing change and restart enrollment.");
                        string errorMessage = FlattenException(ex);
                        _logger.LogError(errorMessage);
                        return Failure(errorMessage);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to update existing enrollment - {enrollmentId}");
                    string errorMessage = FlattenException(e);
                    _logger.LogError(errorMessage);
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
                catch (HttpInterfaceException e) when (e.ErrorCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogError($"Enrollment already exists for CN {reenrollment.csr.cn}, cannot create new enrollment.");
                    string errorMessage = FlattenException(e);
                    _logger.LogError(errorMessage);
                    return Failure(errorMessage);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to create new enrollment");
                    string errorMessage = FlattenException(e);
                    _logger.LogError(errorMessage);
                    return Failure(errorMessage);
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
                catch (HttpInterfaceException e) when (e.ErrorCode == System.Net.HttpStatusCode.NotFound)
                {
                    // wait 30 seconds before checking for processed CSR again
                    _logger.LogTrace("Akamai CSR not ready yet. Sleeping process to try again.");
                    retryCount++;
                    _timerService.DelayBySeconds(30);
                }
                catch (Exception e)
                {
                    _logger.LogError("Error ocurred when trying to retrieve CSR. Error appears unrelated to CSR not being ready yet.");
                    string errorMessage = FlattenException(e);
                    _logger.LogError(errorMessage);
                    return Failure(errorMessage);
                }
            }

            // if no CSR received, fail job
            if (string.IsNullOrEmpty(csr))
            {
                string errorMessage = $"Maximum retry count reached. CSR was not finished processing for new enrollment - {enrollmentId}";
                _logger.LogError(errorMessage);
                return Failure(errorMessage);
            }

            _logger.LogDebug("Retrieved CSR content for enrollment change.");

            // submit csr
            _logger.LogTrace("Submitting CSR to Keyfactor");
            var x509Cert = submitReenrollmentUpdate.Invoke(csr);
            _logger.LogDebug("Certificate returned from CSR enrollment.");

            if (x509Cert == null)
            {
                string errorMessage = "Certificate was not returned from Keyfactor for submitted CSR.";
                _logger.LogError(errorMessage);
                _logger.LogTrace($"Failed CSR: \n{csr}");
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

            _logger.LogTrace("Posting certificate to Akamai.");
            try
            {
                client.PostCertificate(enrollmentId, changeId, certContent, keyType);
            }
            catch (Exception e)
            {
                string errorMessage = "Error uploading certificate to Akamai.";
                _logger.LogError(errorMessage);
                _logger.LogError(FlattenException(e));
                _logger.LogError($"Certificate of type {keyType} was not uploaded to enrollment {enrollmentId} for change {changeId}");
                _logger.LogTrace($"Failed Certificate content: \n{certContent}");
                return Failure(errorMessage);
            }
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
                catch (HttpInterfaceException e) when (e.ErrorCode == System.Net.HttpStatusCode.NotFound)
                {
                    // wait 30 seconds before checking for warnings again
                    _logger.LogTrace("Akamai deployment warnings are not processed yet. Sleeping process to try again.");
                    retryCount++;
                    _timerService.DelayBySeconds(20);
                }
                catch (Exception e)
                {
                    string errorMessage = "Error ocurred when trying to acknowledge warnings and deploy certificate.";
                    _logger.LogError(errorMessage);
                    _logger.LogError(FlattenException(e));
                    return Failure(errorMessage);
                }
            }

            if (!ack)
            {
                // all retries used and not successful
                string warnMessage = "Maximum retries reached and the deployment warnings could not be acknowledged. Certificate may not be deployed.";
                _logger.LogWarning(warnMessage);
                return Warning(warnMessage);
            }
            else
            {
                _logger.LogInformation($"Warnings acknowleged for enrollment - {enrollmentId}");
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
