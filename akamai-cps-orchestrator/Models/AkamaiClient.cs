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

using System;
using System.Linq;
using Keyfactor.Extensions.Utilities.HttpInterface;
using Keyfactor.Extensions.Utilities.HttpInterface.Configs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    public class AkamaiClient
    {
        private ILogger _logger;
        private HttpInterface _http;
        private AkamaiAuth _auth;
        

        public string Username;
        public string ApiKey; // accountSwitchKey

        public string Hostname;
        public bool IsProduction = false;

        public AkamaiClient(ILogger logger, string clientMachine, AkamaiAuth auth)
        {
            _logger = logger;
            Hostname = clientMachine;

            _auth = auth;
            _http = new HttpInterface(_logger, _auth, Hostname, useSSL: true);
        }

        public void SetDeploymentType(string storePath)
        {
            if (storePath == Constants.StorePaths.Production)
            {
                IsProduction = true;
            }
            else if (storePath == Constants.StorePaths.Staging)
            {
                IsProduction = false;
            }
            else
            {
                throw new ArgumentException($"Store path {storePath} did not match either '{Constants.StorePaths.Production}' or '{Constants.StorePaths.Staging}'");
            }
        }

        public CertificateInfo GetCertificate(string enrollmentId)
        {
            var path = string.Format(Constants.Endpoints.Deployments, enrollmentId);
            var acceptHeader = "application/vnd.akamai.cps.deployments.v7+json";

            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
            };

            Deployment deployment = _http.Get<Deployment>(config, path);

            // deployments are returned for in process enrollments, so null coalesce to filter for fully deployed certs
            if (IsProduction)
            {
                return deployment?.production?.primaryCertificate;
            }
            else
            {
                // staging certificate shows up for completed production deployments
                // to display certs ONLY in staging, need to verify it is not in production
                return deployment?.staging?.primaryCertificate;
            }
        }

        public Enrollment[] GetEnrollments()
        {
            var path = Constants.Endpoints.Enrollments;
            var acceptHeader = "application/vnd.akamai.cps.enrollments.v11+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
            };

            Enrollments enrollmentList = _http.Get<Enrollments>(config, path);
            return enrollmentList.enrollments;
        }

        public Enrollment GetEnrollment(string enrollmentId)
        {
            var path = $"{Constants.Endpoints.Enrollments}/{enrollmentId}";
            var acceptHeader = "application/vnd.akamai.cps.enrollment.v11+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
            };

            return _http.Get<Enrollment>(config, path);
        }

        public ChangeHistory GetEnrollmentChangeHistory(string enrollmentId)
        {
            var path = $"{Constants.Endpoints.Enrollments}/{enrollmentId}/history/changes";
            var acceptHeader = "application/vnd.akamai.cps.change-history.v5+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
            };

            return _http.Get<ChangeHistory>(config, path);
        }

        public CreatedEnrollment CreateEnrollment(Enrollment newEnrollment, string contractId)
        {
            // enable change management if it is a staging enrollment
            newEnrollment.changeManagement = !IsProduction;

            var path = $"{Constants.Endpoints.Enrollments}?contractId={contractId}";
            var acceptHeader = "application/vnd.akamai.cps.enrollment-status.v1+json";
            var contentHeader = "application/vnd.akamai.cps.enrollment.v11+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
                ContentType = contentHeader,
            };

            return _http.Post<Enrollment, CreatedEnrollment>(config, path, newEnrollment);
        }

        public CreatedEnrollment UpdateEnrollment(string enrollmentId, Enrollment enrollment)
        {
            var path = $"{Constants.Endpoints.Enrollments}/{enrollmentId}?force-renewal=true&allow-cancel-pending-changes=true"; //&allow-staging-bypass={IsProduction.ToString().ToLower()}";
            var acceptHeader = "application/vnd.akamai.cps.enrollment-status.v1+json";
            var contentHeader = "application/vnd.akamai.cps.enrollment.v11+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
                ContentType = contentHeader,
            };

            return _http.Put<Enrollment, CreatedEnrollment>(config, path, enrollment);
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
            var path = string.Format(Constants.Endpoints.Changes, enrollmentId) + $"/{changeId}";
            var acceptHeader = "application/vnd.akamai.cps.change-id.v1+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
            };

            var response = _http.DeleteRaw(config, path);
            return;
        }

        public void PostCertificate(string enrollmentId, string changeId, string certificate, string keyAlgorithm, string trustChain = null)
        {
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

            var response = _http.PostRaw(config, path, body);
            return;
        }

        public void DeployCertificate(string enrollmentId, string changeId)
        {
            var path = string.Format(Constants.Endpoints.UpdateDeployment, enrollmentId, changeId);
            var acceptHeader = "application/vnd.akamai.cps.change-id.v1+json";
            var contentHeader = "application/vnd.akamai.cps.deployment-schedule.v1+json";
            
            var config = new HttpRequestConfig()
            {
                Accept = acceptHeader,
                ContentType = contentHeader,
            };

            var response = _http.GetRaw(config, path);
            return;
        }

        public void AcknowledgeWarnings(string enrollmentId, string changeId)
        {
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

            var response = _http.PostRaw(config, path, body);
            return;
        }
    }
}
