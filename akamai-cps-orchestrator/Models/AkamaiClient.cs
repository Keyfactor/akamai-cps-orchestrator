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
        public string EnrollmentId;
        public string ChangeId;
        public bool IsProduction = false;

        public AkamaiClient(string clientMachine, AkamaiAuth auth)
        {
            //string[] clientInput = clientMachine.Split('#'); // if > 2 parts, invalid input
            //Hostname = clientInput[0];
            //EnrollmentId = clientInput[1];

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

        public CertificateInfo GetCertificate()
        {
            var path = string.Format(Constants.Endpoints.Deployments, EnrollmentId);
            var authHeader = _auth.GenerateAuthHeader("GET", Hostname, path);
            _http.DefaultRequestHeaders.Authorization = authHeader;

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

        public Enrollment GetEnrollment(string enrollmentId)
        {
            EnrollmentId = enrollmentId;
            var path = $"{Constants.Endpoints.Enrollments}/{EnrollmentId}";
            var authHeader = _auth.GenerateAuthHeader("GET", Hostname, path);
            _http.DefaultRequestHeaders.Authorization = authHeader;

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
            var authHeader = _auth.GenerateAuthHeader("POST", Hostname, path, body);
            _http.DefaultRequestHeaders.Authorization = authHeader;

            var response = _http.PostAsync(path, new StringContent(body)).Result;
            string json = response.Content.ReadAsStringAsync().Result;
            CreatedEnrollment enrollment = JsonConvert.DeserializeObject<CreatedEnrollment>(json);
            return enrollment;
        }

        public CreatedEnrollment UpdateEnrollment(string enrollmentId, Enrollment enrollment)
        {
            var path = Constants.Endpoints.Enrollments;
            var body = JsonConvert.SerializeObject(enrollment);
            var authHeader = _auth.GenerateAuthHeader("PUT", Hostname, path, body);
            _http.DefaultRequestHeaders.Authorization = authHeader;

            var response = _http.PutAsync(path, new StringContent(body)).Result;
            string json = response.Content.ReadAsStringAsync().Result;
            CreatedEnrollment updatedEnrollment = JsonConvert.DeserializeObject<CreatedEnrollment>(json);
            return updatedEnrollment;
        }

        public string GetCSR(string changeId)
        {
            // get CSR from new pending change
            var path = string.Format(Constants.Endpoints.GetChange, EnrollmentId, changeId);
            var authHeader = _auth.GenerateAuthHeader("GET", Hostname, path);
            _http.DefaultRequestHeaders.Authorization = authHeader;

            var response = _http.GetAsync(path).Result;
            string json = response.Content.ReadAsStringAsync().Result;
            PendingChange change = JsonConvert.DeserializeObject<PendingChange>(json);

            return change.csrs[0].csr;
        }

        public void PostCertificate(string certificate, string keyAlgorithm, string trustChain = null)
        {
            var path = string.Format(Constants.Endpoints.UpdateChange, EnrollmentId, ChangeId);
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
            var authHeader = _auth.GenerateAuthHeader("POST", Hostname, path, body);
            _http.DefaultRequestHeaders.Authorization = authHeader;

            var response = _http.PostAsync(path, new StringContent(body)).Result;
            string json = response.Content.ReadAsStringAsync().Result;
            return;
        }

        public void DeployCertificate()
        {
            var path = string.Format(Constants.Endpoints.UpdateDeployment, EnrollmentId, ChangeId);
            var authHeader = _auth.GenerateAuthHeader("GET", Hostname, path);
            _http.DefaultRequestHeaders.Authorization = authHeader;

            var response = _http.GetAsync(path).Result;
            string json = response.Content.ReadAsStringAsync().Result;
            return;
        }
    }
}
