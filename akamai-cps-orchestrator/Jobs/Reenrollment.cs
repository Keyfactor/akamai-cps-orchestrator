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
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Keyfactor.Logging;
using Keyfactor.Extensions.Utilities.HttpInterface.Exceptions;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Factories;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Services;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Jobs
{
    public class Reenrollment : AkamaiJob, IReenrollmentJobExtension
    {
        public string ExtensionName => "Akamai";

        private readonly ILogger _logger;
        private readonly IAkamaiClientFactory _akamaiClientFactory;
        private readonly ITimerService _timerService;
        private readonly ICertificateChainService _certificateChainService;

        // Default constructor for production use.
        public Reenrollment()
        {
            _logger = LogHandler.GetClassLogger<Reenrollment>();
            _akamaiClientFactory = new AkamaiClientFactory();
            _timerService = new TimerService();
            _certificateChainService = new CertificateChainService();
        }

        // Constructor for dependency injection in unit tests.
        public Reenrollment(ILogger logger, IAkamaiClientFactory akamaiClientFactory,
            ITimerService timerService, ICertificateChainService certificateChainService)
        {
            _logger = logger;
            _akamaiClientFactory = akamaiClientFactory;
            _timerService = timerService;
            _certificateChainService = certificateChainService;
        }

        public JobResult ProcessJob(ReenrollmentJobConfiguration jobConfig, SubmitReenrollmentCSR submitCsr)
        {
            JobHistoryId = jobConfig.JobHistoryId;
            var allJobProps = jobConfig.JobProperties;

            // Create the Akamai API client.
            IAkamaiClient client;
            try
            {
                client = CreateAkamaiClient(jobConfig);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to initialize Akamai client.");
                return Failure(FlattenException(e));
            }

            // Parse the enrollment request (CSR fields, contacts, network) from job properties.
            Enrollment reenrollment;
            string contractId, keyType;
            try
            {
                (reenrollment, contractId, keyType) = BuildEnrollmentRequest(allJobProps);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to build enrollment request from job properties.");
                return Failure(FlattenException(e));
            }

            // Create a new enrollment or update an existing one.
            string enrollmentId;
            bool secureNetworkMismatch;
            CreatedEnrollment createdEnrollment;
            bool hasExistingEnrollment = allJobProps.TryGetValue("EnrollmentId", out var existingEnrollmentIdObj)
                                         && existingEnrollmentIdObj != null;
            try
            {
                if (hasExistingEnrollment)
                {
                    enrollmentId = existingEnrollmentIdObj.ToString();
                    (createdEnrollment, secureNetworkMismatch) = UpdateExistingEnrollment(client, enrollmentId, reenrollment);
                }
                else
                {
                    (createdEnrollment, enrollmentId) = CreateNewEnrollment(client, reenrollment, contractId);
                    secureNetworkMismatch = false;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to create or update enrollment.");
                return Failure(FlattenException(e));
            }

            // Fetch the CSR from Akamai (processed asynchronously; poll until ready).
            string changeId = ExtractLastPathSegment(createdEnrollment.changes[0]);
            string csr;
            try
            {
                csr = FetchCsrWithRetry(client, enrollmentId, changeId, keyType);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected error retrieving CSR for enrollment {enrollmentId}.", enrollmentId);
                return Failure(FlattenException(e));
            }
            if (string.IsNullOrEmpty(csr))
            {
                var errorMessage = $"Maximum retry count reached. CSR was not finished processing for enrollment {enrollmentId}.";
                _logger.LogError(errorMessage);
                return Failure(errorMessage);
            }

            // Submit the CSR to Keyfactor and receive the signed leaf certificate.
            _logger.LogTrace("Submitting CSR to Keyfactor for enrollment {enrollmentId}.", enrollmentId);
            var leafCert = submitCsr.Invoke(csr);
            if (leafCert == null)
            {
                _logger.LogError("No certificate returned from Keyfactor for enrollment {enrollmentId}. CSR:\n{csr}", enrollmentId, csr);
                return Failure("Certificate was not returned from Keyfactor for submitted CSR.");
            }
            _logger.LogDebug("Signed certificate received from Keyfactor for enrollment {enrollmentId}.", enrollmentId);

            // Build the PEM content for the certificate and, if available, the trust chain.
            var (certContent, trustChain, trustChainBuilt) = BuildCertificatePem(leafCert);

            // Upload the certificate to Akamai.
            _logger.LogTrace("Posting certificate to Akamai for enrollment {enrollmentId}, change {changeId}.", enrollmentId, changeId);
            try
            {
                client.PostCertificate(enrollmentId, changeId, certContent, keyType, trustChain);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to upload certificate for enrollment {enrollmentId}, change {changeId}.", enrollmentId, changeId);
                return Failure("Error uploading certificate to Akamai.");
            }
            _logger.LogInformation("Certificate uploaded for enrollment {enrollmentId}.", enrollmentId);

            // Acknowledge deployment warnings to trigger Akamai deployment.
            _logger.LogTrace("Acknowledging deployment warnings for enrollment {enrollmentId}.", enrollmentId);
            bool ack;
            try
            {
                ack = AcknowledgeWarningsWithRetry(client, enrollmentId, changeId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected error acknowledging deployment warnings for enrollment {enrollmentId}.", enrollmentId);
                return Failure(FlattenException(e));
            }
            
            _logger.LogInformation("Deployment warnings acknowledged for enrollment {enrollmentId}.", enrollmentId);

            // Return result — check for known non-fatal conditions before declaring success.
            StringBuilder warnMessageBuilder = new ();
            if (!trustChainBuilt)
            {
                warnMessageBuilder.AppendLine("Enrollment completed but the certificate's trust chain could not be built. Please verify the intermediate and root certificates are part of your system's trust store or have publicly available AIA information.");
            }
            if (secureNetworkMismatch)
            {
                warnMessageBuilder.AppendLine("Certificate was deployed, but the deployment network type could not be updated if it was different from the original enrollment. Enrollment preserved original network type.");
            }
            if (!ack)
            {
                warnMessageBuilder.AppendLine("Maximum retries reached and the deployment warnings could not be acknowledged. Certificate may not be deployed.");
            }
            
            if (warnMessageBuilder.Length > 0)
            {
                var warnMessage = warnMessageBuilder.ToString();
                _logger.LogWarning(warnMessage);
                return Warning(warnMessage);
            }

            _logger.LogInformation("Reenrollment completed successfully for enrollment {enrollmentId}.", enrollmentId);
            return Success();
        }

        /// <summary>
        /// Creates and returns an authenticated Akamai API client from the job's store configuration.
        /// </summary>
        private IAkamaiClient CreateAkamaiClient(ReenrollmentJobConfiguration jobConfig)
        {
            _logger.LogTrace("Deserializing store properties for Akamai authentication.");
            var storeProps = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                jobConfig.CertificateStoreDetails.Properties);

            _logger.LogTrace("Creating Akamai client for machine {clientMachine}, store path {storePath}.",
                jobConfig.CertificateStoreDetails.ClientMachine,
                jobConfig.CertificateStoreDetails.StorePath);

            var client = _akamaiClientFactory.Create(
                _logger, storeProps,
                jobConfig.CertificateStoreDetails.ClientMachine,
                jobConfig.CertificateStoreDetails.StorePath);

            _logger.LogDebug("Akamai client created successfully.");
            return client;
        }

        /// <summary>
        /// Parses the Enrollment object, contract ID, and key type from job properties.
        /// Throws <see cref="ArgumentException"/> if any required field is missing.
        /// </summary>
        private (Enrollment enrollment, string contractId, string keyType) BuildEnrollmentRequest(
            Dictionary<string, object> jobProps)
        {
            _logger.LogTrace("Parsing enrollment request from job properties.");

            string subject = GetRequiredValue(jobProps, "subjectText");
            string keyType = GetRequiredValue(jobProps, "keyType");
            string contractId = GetRequiredValue(jobProps, "ContractId");
            string sans = GetRequiredValue(jobProps, "Sans");

            _logger.LogDebug("Enrollment request: keyType={keyType}, contractId={contractId}, sans={sans}.",
                keyType, contractId, sans);

            var subjectValues = CertificateSubjectInformation.ParseFromSubjectText(subject);
            _logger.LogDebug("Parsed subject: {subject}.", JsonConvert.SerializeObject(subjectValues));

            var enrollment = new Enrollment
            {
                csr = new EnrollmentCSR
                {
                    cn = subjectValues.CommonName,
                    c  = subjectValues.CountryRegion,
                    l  = subjectValues.CityLocality,
                    o  = subjectValues.Organization,
                    ou = subjectValues.OrganizationalUnit,
                    st = subjectValues.StateProvince,
                    sans = sans.Split('&'),
                },
                adminContact = BuildAdminContact(jobProps),
                org          = BuildOrgContact(jobProps),
                techContact  = BuildTechContact(jobProps),
            };

            // Resolve the optional deployment network parameter.
            // Default to "standard-tls" for backward compatibility with job templates that
            // predate this field — omitting it avoids silently downgrading Enhanced TLS enrollments.
            if (jobProps.TryGetValue("deployment-network", out var deploymentNetworkRaw)
                && deploymentNetworkRaw != null
                && !string.IsNullOrWhiteSpace(deploymentNetworkRaw.ToString()))
            {
                enrollment.networkConfiguration.secureNetwork = ParseSecureNetwork(deploymentNetworkRaw.ToString());
            }
            _logger.LogDebug("Deployment network resolved to: {secureNetwork}.",
                enrollment.networkConfiguration.secureNetwork);

            return (enrollment, contractId, keyType);
        }

        /// <summary>
        /// Creates a brand-new enrollment in Akamai CPS and returns the created enrollment
        /// along with the extracted enrollment ID.
        /// Throws if the enrollment already exists (HTTP 409) or on any other API error.
        /// </summary>
        private (CreatedEnrollment createdEnrollment, string enrollmentId) CreateNewEnrollment(
            IAkamaiClient client, Enrollment reenrollment, string contractId)
        {
            _logger.LogDebug("No existing enrollment found. Creating new enrollment for CN={cn}.", reenrollment.csr.cn);

            CreatedEnrollment createdEnrollment;
            try
            {
                createdEnrollment = client.CreateEnrollment(reenrollment, contractId);
            }
            catch (HttpInterfaceException e) when (e.ErrorCode == System.Net.HttpStatusCode.Conflict)
            {
                // Wrap with context so the caller's FlattenException produces a useful failure message.
                throw new InvalidOperationException(
                    $"Enrollment already exists for CN '{reenrollment.csr.cn}'. Cannot create a duplicate.", e);
            }

            var enrollmentId = ExtractLastPathSegment(createdEnrollment.enrollment);
            _logger.LogInformation("Created new enrollment {enrollmentId}.", enrollmentId);
            return (createdEnrollment, enrollmentId);
        }

        /// <summary>
        /// Fetches an existing enrollment from Akamai and updates it with the new CSR data.
        /// Returns the updated enrollment and a flag indicating whether the requested deployment
        /// network differed from the existing one (Akamai does not allow changing network type on update).
        /// Throws on any API error.
        /// </summary>
        private (CreatedEnrollment createdEnrollment, bool secureNetworkMismatch) UpdateExistingEnrollment(
            IAkamaiClient client, string enrollmentId, Enrollment reenrollment)
        {
            _logger.LogDebug("Fetching existing enrollment {enrollmentId}.", enrollmentId);
            var existingEnrollment = client.GetEnrollment(enrollmentId);

            // Apply only the CSR from the reenrollment request; all other fields are preserved from
            // the existing enrollment to avoid inadvertent changes.
            existingEnrollment.csr = reenrollment.csr;

            bool secureNetworkMismatch = existingEnrollment.networkConfiguration.secureNetwork
                                         != reenrollment.networkConfiguration.secureNetwork;
            if (secureNetworkMismatch)
            {
                _logger.LogWarning(
                    "Akamai does not support changing deployment network type on an existing enrollment. " +
                    "Existing: {existing}, requested: {requested}. Preserving existing network type.",
                    existingEnrollment.networkConfiguration.secureNetwork,
                    reenrollment.networkConfiguration.secureNetwork);
            }

            _logger.LogDebug("Updating enrollment {enrollmentId}.", enrollmentId);
            var createdEnrollment = UpdateEnrollmentWithRollback(client, enrollmentId, existingEnrollment);
            _logger.LogInformation("Enrollment {enrollmentId} updated successfully.", enrollmentId);

            return (createdEnrollment, secureNetworkMismatch);
        }

        /// <summary>
        /// Calls UpdateEnrollment and, on a 500 response (indicating the enrollment is mid-deployment),
        /// rolls back the most recent pending change and retries once.
        /// Throws on all other errors or if the rollback itself fails.
        /// </summary>
        private CreatedEnrollment UpdateEnrollmentWithRollback(
            IAkamaiClient client, string enrollmentId, Enrollment existingEnrollment)
        {
            try
            {
                return client.UpdateEnrollment(enrollmentId, existingEnrollment);
            }
            catch (HttpInterfaceException e) when (e.ErrorCode == System.Net.HttpStatusCode.InternalServerError)
            {
                // A 500 is returned when the enrollment is already in a deployment process.
                // Roll back the most recent pending change to unblock, then retry.
                _logger.LogWarning("Update for enrollment {enrollmentId} returned 500; enrollment may be mid-deployment. Attempting rollback.", enrollmentId);

                var mostRecentChangeId = existingEnrollment.pendingChanges.Last().location.Split('/')[^1];
                _logger.LogDebug("Deleting pending change {changeId} for enrollment {enrollmentId}.", mostRecentChangeId, enrollmentId);
                client.DeletePendingChange(enrollmentId, mostRecentChangeId);
                _logger.LogInformation("Pending change {changeId} deleted. Retrying update for enrollment {enrollmentId}.", mostRecentChangeId, enrollmentId);

                // NOTE: This retry does not work when the pending change was staging-only,
                // as the enrollment will no longer exist in that state after deletion.
                return client.UpdateEnrollment(enrollmentId, existingEnrollment);
            }
        }

        /// <summary>
        /// Polls Akamai for the CSR up to 5 times, waiting 30 seconds between attempts.
        /// Returns null if all retries are exhausted without a result.
        /// Non-404 exceptions propagate to the caller immediately.
        /// </summary>
        private string FetchCsrWithRetry(IAkamaiClient client, string enrollmentId, string changeId, string keyType)
        {
            const int maxAttempts = 5;
            _logger.LogDebug("Fetching CSR for enrollment {enrollmentId}, change {changeId} (up to {maxAttempts} attempts).",
                enrollmentId, changeId, maxAttempts);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var csr = client.GetCSR(enrollmentId, changeId, keyType);
                    _logger.LogDebug("CSR retrieved on attempt {attempt}/{maxAttempts} for enrollment {enrollmentId}.",
                        attempt, maxAttempts, enrollmentId);
                    return csr;
                }
                catch (HttpInterfaceException e) when (e.ErrorCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogTrace("CSR not ready yet (attempt {attempt}/{maxAttempts}). Waiting 30 seconds before retrying.",
                        attempt, maxAttempts);
                    _timerService.DelayBySeconds(30);
                }
                // Non-404 exceptions propagate to the caller.
            }

            return null; // All retries exhausted.
        }

        /// <summary>
        /// Builds PEM-encoded strings for the leaf certificate and, if the chain service can resolve it,
        /// the trust chain. Returns whether the trust chain was successfully built.
        /// </summary>
        private (string certContent, string? trustChain, bool trustChainBuilt) BuildCertificatePem(X509Certificate2 leafCert)
        {
            _logger.LogTrace("Building PEM content for certificate {thumbprint}.", leafCert.Thumbprint);

            var x509Cert = ConvertToBouncyCastleX509(leafCert);
            var chain = _certificateChainService.BuildCertificateCollection(x509Cert);

            var sb = new StringBuilder();
            AppendCertificatePEM(sb, chain.EndEntityCert);
            var certContent = sb.ToString().Replace("\r", "");

            if (chain.ChainCerts == null)
            {
                _logger.LogWarning("Trust chain could not be resolved for certificate {thumbprint}. " +
                    "Verify that the issuer chain is in the system trust store or has publicly accessible AIA endpoints.",
                    leafCert.Thumbprint);
                return (certContent, null, false);
            }

            _logger.LogTrace("Building trust chain PEM content ({count} intermediate cert(s)) for certificate {thumbprint}.",
                chain.ChainCerts.Count(), leafCert.Thumbprint);
            sb.Clear();
            foreach (var cert in chain.ChainCerts)
                AppendCertificatePEM(sb, cert);

            _logger.LogDebug("Trust chain built successfully ({count} intermediate cert(s)) for certificate {thumbprint}.",
                chain.ChainCerts.Count(), leafCert.Thumbprint);
            return (certContent, sb.ToString(), true);
        }

        /// <summary>
        /// Acknowledges Akamai deployment warnings up to 4 times, waiting 20 seconds between attempts.
        /// Returns false if all retries are exhausted. Non-404 exceptions propagate to the caller.
        /// </summary>
        private bool AcknowledgeWarningsWithRetry(IAkamaiClient client, string enrollmentId, string changeId)
        {
            const int maxAttempts = 4;
            _logger.LogDebug("Acknowledging deployment warnings for enrollment {enrollmentId}, change {changeId} (up to {maxAttempts} attempts).",
                enrollmentId, changeId, maxAttempts);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    client.AcknowledgeWarnings(enrollmentId, changeId);
                    _logger.LogDebug("Deployment warnings acknowledged on attempt {attempt}/{maxAttempts} for enrollment {enrollmentId}.",
                        attempt, maxAttempts, enrollmentId);
                    return true;
                }
                catch (HttpInterfaceException e) when (e.ErrorCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogTrace("Deployment warnings not ready yet (attempt {attempt}/{maxAttempts}). Waiting 20 seconds before retrying.",
                        attempt, maxAttempts);
                    _timerService.DelayBySeconds(20);
                }
                // Non-404 exceptions propagate to the caller.
            }

            return false; // All retries exhausted.
        }

        // --- Contact builders ---

        private ContactInfo BuildAdminContact(Dictionary<string, object> jobProps) => new ContactInfo
        {
            addressLineOne   = GetRequiredValue(jobProps, "admin-addressLineOne"),
            addressLineTwo   = jobProps["admin-addressLineTwo"]?.ToString(),
            city             = GetRequiredValue(jobProps, "admin-city"),
            country          = GetRequiredValue(jobProps, "admin-country"),
            email            = GetRequiredValue(jobProps, "admin-email"),
            firstName        = GetRequiredValue(jobProps, "admin-firstName"),
            lastName         = GetRequiredValue(jobProps, "admin-lastName"),
            organizationName = GetRequiredValue(jobProps, "admin-organizationName"),
            phone            = GetRequiredValue(jobProps, "admin-phone"),
            postalCode       = GetRequiredValue(jobProps, "admin-postalCode"),
            region           = GetRequiredValue(jobProps, "admin-region"),
            title            = GetRequiredValue(jobProps, "admin-title"),
        };

        private ContactInfo BuildOrgContact(Dictionary<string, object> jobProps) => new ContactInfo
        {
            addressLineOne = GetRequiredValue(jobProps, "org-addressLineOne"),
            addressLineTwo = jobProps["org-addressLineTwo"]?.ToString(),
            city           = GetRequiredValue(jobProps, "org-city"),
            country        = GetRequiredValue(jobProps, "org-country"),
            name           = GetRequiredValue(jobProps, "org-organizationName"),
            phone          = GetRequiredValue(jobProps, "org-phone"),
            postalCode     = GetRequiredValue(jobProps, "org-postalCode"),
            region         = GetRequiredValue(jobProps, "org-region"),
        };

        private ContactInfo BuildTechContact(Dictionary<string, object> jobProps) => new ContactInfo
        {
            addressLineOne   = GetRequiredValue(jobProps, "tech-addressLineOne"),
            addressLineTwo   = jobProps["tech-addressLineTwo"]?.ToString(),
            city             = GetRequiredValue(jobProps, "tech-city"),
            country          = GetRequiredValue(jobProps, "tech-country"),
            email            = GetRequiredValue(jobProps, "tech-email"),
            firstName        = GetRequiredValue(jobProps, "tech-firstName"),
            lastName         = GetRequiredValue(jobProps, "tech-lastName"),
            organizationName = GetRequiredValue(jobProps, "tech-organizationName"),
            phone            = GetRequiredValue(jobProps, "tech-phone"),
            postalCode       = GetRequiredValue(jobProps, "tech-postalCode"),
            region           = GetRequiredValue(jobProps, "tech-region"),
            title            = GetRequiredValue(jobProps, "tech-title"),
        };

        // --- Utilities ---

        private string GetRequiredValue(Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key) || string.IsNullOrWhiteSpace(dict[key].ToString()))
                throw new ArgumentException($"Required field '{key}' was missing a value in the Reenrollment job properties.");
            return dict[key].ToString();
        }

        private X509Certificate ConvertToBouncyCastleX509(X509Certificate2 cert)
        {
            _logger.LogTrace("Converting certificate {thumbprint} to BouncyCastle format.", cert.Thumbprint);
            return new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(cert.RawData);
        }

        private void AppendCertificatePEM(StringBuilder sb, X509Certificate cert)
        {
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            sb.AppendLine(Convert.ToBase64String(cert.GetEncoded()));
            sb.AppendLine("-----END CERTIFICATE-----");
        }

        private static string ExtractLastPathSegment(string url) => url.Split('/')[^1];

        private static string ParseSecureNetwork(string displayValue) => displayValue switch
        {
            "Standard TLS" => "standard-tls",
            "Enhanced TLS" => "enhanced-tls",
            _ => throw new ArgumentException($"Unrecognized deployment network '{displayValue}'. Expected 'Standard TLS' or 'Enhanced TLS'.")
        };
    }
}
