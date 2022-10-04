using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Jobs;
using Newtonsoft.Json;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    public class AkamaiClient
    {
        private HttpClient _http;
        private AkamaiAuth _auth;

        public string Username;
        public string ApiKey; // accountSwitchKey

        public string Hostname;
        public bool IsProduction = false;

        public AkamaiClient(string clientMachine, AkamaiAuth auth)
        {
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
            PrepareAuth("GET", $"Accept:{acceptHeader}", path);

            var response = _http.GetAsync(path).Result;
            string json = response.Content.ReadAsStringAsync().Result;
            Deployment deployment = JsonConvert.DeserializeObject<Deployment>(json);

            if (IsProduction)
            {
                return deployment.production.primaryCertificate;
            }
            else
            {
                return deployment.staging.primaryCertificate;
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
            string json = response.Content.ReadAsStringAsync().Result;
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
            string json = response.Content.ReadAsStringAsync().Result;
            return JsonConvert.DeserializeObject<Enrollment>(json);
        }

        public CreatedEnrollment CreateEnrollment()
        {
            var path = Constants.Endpoints.Enrollments;
            Enrollment newEnrollment = new Enrollment()
            {
                //
            };
            var body = JsonConvert.SerializeObject(newEnrollment);
            var acceptHeader = "application/vnd.akamai.cps.enrollment-status.v1+json";
            var contentHeader = "application/vnd.akamai.cps.enrollment.v11+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            _http.DefaultRequestHeaders.Add("Content-Type", contentHeader);
            PrepareAuth("POST", path, $"Accept:{acceptHeader}\tContent-Type:{contentHeader}", body);

            var response = _http.PostAsync(path, new StringContent(body)).Result;
            string json = response.Content.ReadAsStringAsync().Result;
            CreatedEnrollment enrollment = JsonConvert.DeserializeObject<CreatedEnrollment>(json);
            return enrollment;
        }

        public CreatedEnrollment UpdateEnrollment(string enrollmentId, Enrollment enrollment)
        {
            var path = Constants.Endpoints.Enrollments;
            var body = JsonConvert.SerializeObject(enrollment);
            var acceptHeader = "application/vnd.akamai.cps.enrollment-status.v1+json";
            var contentHeader = "application/vnd.akamai.cps.enrollment.v11+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            _http.DefaultRequestHeaders.Add("Content-Type", contentHeader);
            PrepareAuth("PUT", path, $"Accept:{acceptHeader}\tContent-Type:{contentHeader}", body);

            var response = _http.PutAsync(path, new StringContent(body)).Result;
            string json = response.Content.ReadAsStringAsync().Result;
            CreatedEnrollment updatedEnrollment = JsonConvert.DeserializeObject<CreatedEnrollment>(json);
            return updatedEnrollment;
        }

        public string GetCSR(string enrollmentId, string changeId)
        {
            // get CSR from new pending change
            var path = string.Format(Constants.Endpoints.GetChange, enrollmentId, changeId);
            var acceptHeader = "application/vnd.akamai.cps.csr.v2+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            PrepareAuth("GET", $"Accept:{acceptHeader}", path);

            var response = _http.GetAsync(path).Result;
            string json = response.Content.ReadAsStringAsync().Result;
            PendingChange change = JsonConvert.DeserializeObject<PendingChange>(json);

            return change.csrs[0].csr;
        }

        public void PostCertificate(string enrollmentId, string changeId, string certificate, string keyAlgorithm, string trustChain = null)
        {
            var path = string.Format(Constants.Endpoints.UpdateChange, enrollmentId, changeId);
            CertificateInfo[] cert = new CertificateInfo[]
            {
                new CertificateInfo()
                {
                    certificate = certificate,
                    keyAlgorithm = keyAlgorithm,
                    trustChain = trustChain
                }
            };
            var body = JsonConvert.SerializeObject(cert);
            var acceptHeader = "application/vnd.akamai.cps.change-id.v1+json";
            var contentHeader = "application/vnd.akamai.cps.acknowledgement.v1+json";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Accept", acceptHeader);
            _http.DefaultRequestHeaders.Add("Content-Type", contentHeader);
            PrepareAuth("POST", path, $"Accept:{acceptHeader}\tContent-Type:{contentHeader}", body);

            var response = _http.PostAsync(path, new StringContent(body)).Result;
            string json = response.Content.ReadAsStringAsync().Result;
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
            PrepareAuth("PUT", $"Accept:{acceptHeader}\tContent-Type:{contentHeader}", path);

            var response = _http.GetAsync(path).Result;
            string json = response.Content.ReadAsStringAsync().Result;
            return;
        }

        private void PrepareAuth(string method, string path, string headers, string requestBody = null)
        {
            // passing along standard HTTP headers not needed (i.e. accept header)
            var authHeader = _auth.GenerateAuthHeader(method, Hostname, path, headers, requestBody);
            _http.DefaultRequestHeaders.Authorization = authHeader;
        }
    }
}
