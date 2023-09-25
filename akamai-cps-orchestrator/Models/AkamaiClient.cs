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
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    public class AkamaiClient
    {
        private ILogger _logger;
        private HttpClient _http;
        private AkamaiAuth _auth;
        private JsonSerializerSettings _serializerSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };

        public string Username;
        public string ApiKey; // accountSwitchKey

        public string Hostname;
        public bool IsProduction = false;

        public AkamaiClient(ILogger logger, string clientMachine, AkamaiAuth auth)
        {
            _logger = logger;
            Hostname = clientMachine;

            _auth = auth;
            _http = new HttpClient();
            _http.BaseAddress = new Uri($"https://{Hostname}/");
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
                // invalid input
            }
        }

        public CertificateInfo GetCertificate(string enrollmentId)
        {
            var path = string.Format(Constants.Endpoints.Deployments, enrollmentId);
            var acceptHeader = "application/vnd.akamai.cps.deployments.v7+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            PrepareAuth("GET", path, $"Accept:{acceptHeader}");

            var response = _http.GetAsync(path).Result;
            string json = ReadHttpResponse(response);
            Deployment deployment = JsonConvert.DeserializeObject<Deployment>(json);

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

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            PrepareAuth("GET", path, $"Accept:{acceptHeader}");

            var response = _http.GetAsync(path).Result;
            string json = ReadHttpResponse(response);
            return JsonConvert.DeserializeObject<Enrollments>(json).enrollments;
        }

        public Enrollment GetEnrollment(string enrollmentId)
        {
            var path = $"{Constants.Endpoints.Enrollments}/{enrollmentId}";
            var acceptHeader = "application/vnd.akamai.cps.enrollment.v11+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            PrepareAuth("GET", path, $"Accept:{acceptHeader}");

            var response = _http.GetAsync(path).Result;
            string json = ReadHttpResponse(response);
            return JsonConvert.DeserializeObject<Enrollment>(json);
        }

        public ChangeHistory GetEnrollmentChangeHistory(string enrollmentId)
        {
            var path = $"{Constants.Endpoints.Enrollments}/{enrollmentId}/history/changes";
            var acceptHeader = "application/vnd.akamai.cps.change-history.v5+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            PrepareAuth("GET", path, $"Accept:{acceptHeader}");

            var response = _http.GetAsync(path).Result;
            string json = ReadHttpResponse(response);
            return JsonConvert.DeserializeObject<ChangeHistory>(json);
        }

        public CreatedEnrollment CreateEnrollment(Enrollment newEnrollment, string contractId)
        {
            // enable change management if it is a staging enrollment
            newEnrollment.changeManagement = !IsProduction;

            var path = $"{Constants.Endpoints.Enrollments}?contractId={contractId}";
            var body = JsonConvert.SerializeObject(newEnrollment, _serializerSettings);
            var requestContent = new StringContent(body);
            var acceptHeader = "application/vnd.akamai.cps.enrollment-status.v1+json";
            var contentHeader = "application/vnd.akamai.cps.enrollment.v11+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            requestContent.Headers.ContentType = new MediaTypeHeaderValue(contentHeader);
            PrepareAuth("POST", path, $"Accept:{acceptHeader}\tContent-Type:{contentHeader}", body);

            var response = _http.PostAsync(path, requestContent).Result;
            string json = ReadHttpResponse(response);
            CreatedEnrollment enrollment = JsonConvert.DeserializeObject<CreatedEnrollment>(json);
            return enrollment;
        }

        public CreatedEnrollment UpdateEnrollment(string enrollmentId, Enrollment enrollment)
        {
            var path = $"{Constants.Endpoints.Enrollments}/{enrollmentId}?force-renewal=true&allow-cancel-pending-changes=true"; //&allow-staging-bypass={IsProduction.ToString().ToLower()}";
            var body = JsonConvert.SerializeObject(enrollment, _serializerSettings);
            var requestContent = new StringContent(body);
            var acceptHeader = "application/vnd.akamai.cps.enrollment-status.v1+json";
            var contentHeader = "application/vnd.akamai.cps.enrollment.v11+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            requestContent.Headers.ContentType = new MediaTypeHeaderValue(contentHeader);
            PrepareAuth("PUT", path, $"Accept:{acceptHeader}\tContent-Type:{contentHeader}"); // dont sign PUT body

            var response = _http.PutAsync(path, requestContent).Result;
            string json = ReadHttpResponse(response);
            CreatedEnrollment updatedEnrollment = JsonConvert.DeserializeObject<CreatedEnrollment>(json);
            return updatedEnrollment;
        }

        public string GetCSR(string enrollmentId, string changeId, string keyType)
        {
            // get CSR from new pending change
            var path = string.Format(Constants.Endpoints.GetChange, enrollmentId, changeId);
            var acceptHeader = "application/vnd.akamai.cps.csr.v2+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            PrepareAuth("GET", path, $"Accept:{acceptHeader}");

            var response = _http.GetAsync(path).Result;
            string json = ReadHttpResponse(response);
            PendingChange change = JsonConvert.DeserializeObject<PendingChange>(json);

            // get CSR for correct key type of reenrollment template
            PendingCSR csr = change.csrs.Where(csr => string.Equals(csr.keyAlgorithm, keyType, StringComparison.CurrentCultureIgnoreCase)).SingleOrDefault();

            return csr.csr;
        }

        public void DeletePendingChange(string enrollmentId, string changeId)
        {
            var path = string.Format(Constants.Endpoints.Changes, enrollmentId) + $"/{changeId}";
            var acceptHeader = "application/vnd.akamai.cps.change-id.v1+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            PrepareAuth("DELETE", path, $"Accept:{acceptHeader}");

            var response = _http.DeleteAsync(path).Result;
            string json = ReadHttpResponse(response);
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
            var requestContent = new StringContent(body);
            var acceptHeader = "application/vnd.akamai.cps.change-id.v1+json";
            var contentHeader = "application/vnd.akamai.cps.certificate-and-trust-chain.v2+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            requestContent.Headers.ContentType = new MediaTypeHeaderValue(contentHeader);
            PrepareAuth("POST", path, $"Accept:{acceptHeader}\tContent-Type:{contentHeader}", body);

            var response = _http.PostAsync(path, requestContent).Result;
            string json = ReadHttpResponse(response);
            return;
        }

        public void DeployCertificate(string enrollmentId, string changeId)
        {
            var path = string.Format(Constants.Endpoints.UpdateDeployment, enrollmentId, changeId);
            var acceptHeader = "application/vnd.akamai.cps.change-id.v1+json";
            var contentHeader = "application/vnd.akamai.cps.deployment-schedule.v1+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            _http.DefaultRequestHeaders.Add("Content-Type", contentHeader);
            PrepareAuth("PUT", path, $"Accept:{acceptHeader}\tContent-Type:{contentHeader}");

            var response = _http.GetAsync(path).Result;
            string json = ReadHttpResponse(response);
            return;
        }

        public void AcknowledgeWarnings(string enrollmentId, string changeId)
        {
            var path = string.Format(Constants.Endpoints.AcknowledgePostVerification, enrollmentId, changeId);
            var ack = new Acknowledgement();
            var body = JsonConvert.SerializeObject(ack);
            var requestContent = new StringContent(body);
            var acceptHeader = "application/vnd.akamai.cps.change-id.v1+json";
            var contentHeader = "application/vnd.akamai.cps.acknowledgement.v1+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            requestContent.Headers.ContentType = new MediaTypeHeaderValue(contentHeader);
            PrepareAuth("POST", path, $"Accept:{acceptHeader}\tContent-Type:{contentHeader}", body);

            var response = _http.PostAsync(path, requestContent).Result;
            string json = ReadHttpResponse(response);
            return;
        }

        private string ReadHttpResponse(HttpResponseMessage response)
        {
            string responseMessage = response.Content.ReadAsStringAsync().Result;
            if (response.IsSuccessStatusCode)
            {
                return responseMessage;
            }
            else
            {
                // log Akamai error reason from response
                throw new AkamaiClientException(responseMessage, response.StatusCode);
            }
        }

        private void PrepareAuth(string method, string path, string headers, string requestBody = null)
        {
            var authHeader = _auth.GenerateAuthHeader(method, Hostname, path, headers, requestBody);
            _http.DefaultRequestHeaders.Authorization = authHeader;
        }
    }
}
