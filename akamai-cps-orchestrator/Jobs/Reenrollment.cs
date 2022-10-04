using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Jobs
{
    public class Reenrollment : IReenrollmentJobExtension
    {
        public string ExtensionName => "Akamai";

        public JobResult ProcessJob(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
        {
            AkamaiAuth auth = new AkamaiAuth();
            AkamaiClient client = new AkamaiClient(jobConfiguration.CertificateStoreDetails.ClientMachine, auth);

            client.SetDeploymentType(jobConfiguration.CertificateStoreDetails.StorePath);

            // store deployment info ??

            // get enrollment
            CreatedEnrollment enrollment;
            bool newEnrollment = false;
            var allJobProps = jobConfiguration.JobProperties; // contains entry parameters

            // make this a safe check for enrollment id. if not present as an entry parameter, need to make a new enrollment
            string enrollmentId = allJobProps["EnrollmentId"].ToString();
            try
            {
                Enrollment existingEnrollment = client.GetEnrollment(enrollmentId);
                // make needed enrollment changes
                enrollment = client.UpdateEnrollment(enrollmentId, existingEnrollment);
            }
            catch
            {
                // no existing enrollment, create a new one
                newEnrollment = true;
                enrollment = client.CreateEnrollment();
            }

            // update the enrollment and get CSR
            string csr = client.GetCSR(enrollment.changes[0]);

            // submit csr
            var x509Cert = submitReenrollmentUpdate.Invoke(csr);

            // submit certificate info to deployment
            // build PEM content
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            sb.AppendLine(Convert.ToBase64String(x509Cert.RawData, Base64FormattingOptions.InsertLineBreaks));
            sb.AppendLine("-----END CERTIFICATE-----");
            var certContent = sb.ToString();

            client.PostCertificate(certContent, x509Cert.GetKeyAlgorithm());

            JobResult result = new JobResult()
            {
                Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success
            };
            return result;
        }
    }
}
