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

using System;
using System.Linq;
using Keyfactor.Extensions.Utilities.HttpInterface;
using Keyfactor.Extensions.Utilities.HttpInterface.Configs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    public interface IAkamaiClient
    {
        CertificateInfo GetCertificate(string enrollmentId);
        Enrollment[] GetEnrollments();
        Enrollment GetEnrollment(string enrollmentId);
        ChangeHistory GetEnrollmentChangeHistory(string enrollmentId);
        CreatedEnrollment CreateEnrollment(Enrollment newEnrollment, string contractId);
        CreatedEnrollment UpdateEnrollment(string enrollmentId, Enrollment enrollment);
        string GetCSR(string enrollmentId, string changeId, string keyType);
        void DeletePendingChange(string enrollmentId, string changeId);
        void PostCertificate(string enrollmentId, string changeId, string certificate, string keyAlgorithm, string trustChain = null);
        void DeployCertificate(string enrollmentId, string changeId);
        void AcknowledgeWarnings(string enrollmentId, string changeId);
        DeploymentType GetDeploymentType();
    }

    public class AkamaiClient : IAkamaiClient
    {
        private ILogger _logger;
        private HttpInterface _http;
        private AkamaiAuth _auth;
        

        public string Username;
        public string ApiKey; // accountSwitchKey

        private readonly bool _isProduction = false;
        private readonly DeploymentType _deploymentType;

        public AkamaiClient(ILogger logger, string clientMachine, DeploymentType deploymentType, AkamaiAuth auth)
        {
            _logger = logger;
            _auth = auth;
            _http = new HttpInterface(_logger, _auth, clientMachine, useSSL: true);
            
            _isProduction = deploymentType == DeploymentType.Production;
            _deploymentType = deploymentType;
            
            logger.LogDebug("Initialized AkamaiClient with hostname {Hostname} for deployment type {DeploymentType}", clientMachine, deploymentType);
        }

        public DeploymentType GetDeploymentType()
        {
            return _deploymentType;
        }

        public CertificateInfo GetCertificate(string enrollmentId)
        {
            _logger.LogTrace("Getting certificate for enrollment ID {EnrollmentId}. IsProduction: {IsProduction}", enrollmentId, _isProduction);
            var path = string.Format(Constants.Endpoints.Deployments, enrollmentId);
            var acceptHeader = "application/vnd.akamai.cps.deployments.v7+json";

            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
            };

            Deployment deployment = _http.Get<Deployment>(config, path);
            
            _logger.LogDebug("Successfully retrieved certificate for enrollment ID {EnrollmentId}", enrollmentId);

            // deployments are returned for in process enrollments, so null coalesce to filter for fully deployed certs
            if (_isProduction)
            {
                _logger.LogTrace("Returning production certificate for enrollment ID {EnrollmentId}", enrollmentId);
                return deployment?.production?.primaryCertificate;
            }
            
            _logger.LogTrace("Returning staging certificate for enrollment ID {EnrollmentId}", enrollmentId);
            
            // staging certificate shows up for completed production deployments
            // to display certs ONLY in staging, need to verify it is not in production
            return deployment?.staging?.primaryCertificate;
        }

        public Enrollment[] GetEnrollments()
        {
            _logger.LogTrace("Getting enrollments from Akamai");
            
            var path = Constants.Endpoints.Enrollments;
            var acceptHeader = "application/vnd.akamai.cps.enrollments.v11+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
            };

            Enrollments enrollmentList = _http.Get<Enrollments>(config, path);
            
            _logger.LogDebug("Successfully retrieved enrollments. Count: {Count}", enrollmentList?.enrollments?.Length ?? 0);
            
            return enrollmentList.enrollments;
        }

        public Enrollment GetEnrollment(string enrollmentId)
        {
            _logger.LogTrace("Getting enrollment with ID {EnrollmentId}", enrollmentId);
            
            var path = $"{Constants.Endpoints.Enrollments}/{enrollmentId}";
            var acceptHeader = "application/vnd.akamai.cps.enrollment.v11+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
            };

            var enrollment = _http.Get<Enrollment>(config, path);
            
            _logger.LogDebug("Successfully retrieved enrollment with ID {EnrollmentId}", enrollmentId);
            
            return enrollment;
        }

        public ChangeHistory GetEnrollmentChangeHistory(string enrollmentId)
        {
            _logger.LogTrace("Getting change history for enrollment with ID {EnrollmentId}", enrollmentId);
            
            var path = $"{Constants.Endpoints.Enrollments}/{enrollmentId}/history/changes";
            var acceptHeader = "application/vnd.akamai.cps.change-history.v5+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
            };

            var history = _http.Get<ChangeHistory>(config, path);

            _logger.LogDebug("Successfully retrieved change history for enrollment with ID {EnrollmentId}. Change count: {ChangeCount}", enrollmentId, history?.changes?.Length ?? 0);;
            
            return history;
        }

        public CreatedEnrollment CreateEnrollment(Enrollment newEnrollment, string contractId)
        {
            _logger.LogTrace("Creating new enrollment with contractId {ContractId}", contractId);
            if (!_isProduction)
            {
                // enable change management if it is a staging enrollment
                // TODO: This logic should probably live at the re-enrollment level -- this is a business logic level concern.
                _logger.LogDebug("Enabling change management for new enrollment because deployment type is staging");
                newEnrollment.changeManagement = true;
            }
            else
            {
                _logger.LogDebug("Not enabling change management for new enrollment because deployment type is production");
                newEnrollment.changeManagement = false;
            }

            var path = $"{Constants.Endpoints.Enrollments}?contractId={contractId}";
            var acceptHeader = "application/vnd.akamai.cps.enrollment-status.v1+json";
            var contentHeader = "application/vnd.akamai.cps.enrollment.v11+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
                ContentType = contentHeader,
            };

            var response = _http.Post<Enrollment, CreatedEnrollment>(config, path, newEnrollment);
            _logger.LogDebug("Successfully created new enrollment for contract ID {ContractId}", contractId);
            return response;
        }

        public CreatedEnrollment UpdateEnrollment(string enrollmentId, Enrollment enrollment)
        {
            _logger.LogTrace("Updating enrollment with ID {EnrollmentId}", enrollmentId);
            
            var path = $"{Constants.Endpoints.Enrollments}/{enrollmentId}?force-renewal=true&allow-cancel-pending-changes=true"; //&allow-staging-bypass={IsProduction.ToString().ToLower()}";
            var acceptHeader = "application/vnd.akamai.cps.enrollment-status.v1+json";
            var contentHeader = "application/vnd.akamai.cps.enrollment.v11+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
                ContentType = contentHeader,
            };

            var response = _http.Put<Enrollment, CreatedEnrollment>(config, path, enrollment);

            _logger.LogDebug("Successfully updated enrollment with ID {EnrollmentId}", enrollmentId);
            
            return response;
        }

        public string GetCSR(string enrollmentId, string changeId, string keyType)
        {
            // get CSR from new pending change
            var path = string.Format(Constants.Endpoints.GetChange, enrollmentId, changeId);
            var acceptHeader = "application/vnd.akamai.cps.csr.v2+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
            };

            PendingChange change = _http.Get<PendingChange>(config, path);

            // get CSR for correct key type of reenrollment template
            PendingCSR csr = change.csrs.Where(csr => string.Equals(csr.keyAlgorithm, keyType, StringComparison.CurrentCultureIgnoreCase)).SingleOrDefault();

            return csr.csr;
        }

        public void DeletePendingChange(string enrollmentId, string changeId)
        {
            _logger.LogTrace("Deleting pending change with ID {ChangeId} for enrollment ID {EnrollmentId}", changeId, enrollmentId);
            var path = string.Format(Constants.Endpoints.Changes, enrollmentId) + $"/{changeId}";
            var acceptHeader = "application/vnd.akamai.cps.change-id.v1+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
            };

            _http.DeleteRaw(config, path);
            _logger.LogDebug("Successfully deleted pending change with ID {ChangeId} for enrollment ID {EnrollmentId}", changeId, enrollmentId);
        }

        public void PostCertificate(string enrollmentId, string changeId, string certificate, string keyAlgorithm, string trustChain = null)
        {
            _logger.LogTrace("Posting certificate for enrollment ID {EnrollmentId} and change ID {ChangeId}", enrollmentId, changeId);
            var path = string.Format(Constants.Endpoints.UpdateChange, enrollmentId, changeId);
            ThirdPartyCertificates cert = new ThirdPartyCertificates()
            {
                certificatesAndTrustChains = new CertificateInfo[]
                {
                    new CertificateInfo()
                    {
                        certificate = certificate,
                        keyAlgorithm = keyAlgorithm,
                        trustChain = trustChain
                    }
                }
            };
            var body = JsonConvert.SerializeObject(cert);
            var acceptHeader = "application/vnd.akamai.cps.change-id.v1+json";
            var contentHeader = "application/vnd.akamai.cps.certificate-and-trust-chain.v2+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
                ContentType = contentHeader,
            };

            _http.PostRaw(config, path, body);
            _logger.LogDebug("Successfully posted certificate for enrollment ID {EnrollmentId} and change ID {ChangeId}", enrollmentId, changeId);
        }

        public void DeployCertificate(string enrollmentId, string changeId)
        {
            _logger.LogTrace("Deploying certificate for enrollment ID {EnrollmentId} and change ID {ChangeId}", enrollmentId, changeId);
            var path = string.Format(Constants.Endpoints.UpdateDeployment, enrollmentId, changeId);
            var acceptHeader = "application/vnd.akamai.cps.change-id.v1+json";
            var contentHeader = "application/vnd.akamai.cps.deployment-schedule.v1+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
                ContentType = contentHeader,
            };

            _http.GetRaw(config, path);
            _logger.LogDebug("Successfully deployed certificate for enrollment ID {EnrollmentId} and change ID {ChangeId}", enrollmentId, changeId);
        }

        public void AcknowledgeWarnings(string enrollmentId, string changeId)
        {
            _logger.LogTrace("Acknowledging warnings for enrollment ID {EnrollmentId} and change ID {ChangeId}", enrollmentId, changeId);
            var path = string.Format(Constants.Endpoints.AcknowledgePostVerification, enrollmentId, changeId);
            var ack = new Acknowledgement();
            var body = JsonConvert.SerializeObject(ack);
            var acceptHeader = "application/vnd.akamai.cps.change-id.v1+json";
            var contentHeader = "application/vnd.akamai.cps.acknowledgement.v1+json";

            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
                ContentType = contentHeader,
            };

            _http.PostRaw(config, path, body);
            
            _logger.LogDebug("Successfully acknowledged warnings for enrollment ID {EnrollmentId} and change ID {ChangeId}", enrollmentId, changeId);
        }
    }
}
