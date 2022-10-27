using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.IO;

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
            var allJobProps = jobConfiguration.JobProperties; // contains entry parameters - EnrollmentId, ContractId
            string subject = allJobProps["subjectText"].ToString();
            string keyType = allJobProps["keyType"].ToString();
            string contractId = allJobProps["ContractId"].ToString();
            // TODO: add entry parameters for SANs on reenrollment?

            string[] subjectParams = subject.Split(',');
            var subjectValues = new Dictionary<string, string>();
            foreach (var subjectParam in subjectParams)
            {
                string[] subjectPair = subjectParam.Split('=', 2);
                subjectValues.Add(subjectPair[0].ToUpper(), subjectPair[1]);
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
                    st = subjectValues.GetValueOrDefault("ST")
                }
            };

            string extensionDirectory = Path.GetDirectoryName(this.GetType().Assembly.Location);
            string jsonContactInfo = File.ReadAllText($"{extensionDirectory}{Path.DirectorySeparatorChar}config.json");
            JsonConvert.PopulateObject(jsonContactInfo, reenrollment);


            // if not present as an entry parameter, need to make a new enrollment
            string enrollmentId;
            bool enrollmentExists = allJobProps.TryGetValue("EnrollmentId", out object existingEnrollmentId);
            if (enrollmentExists && existingEnrollmentId != null)
            {
                enrollmentId = existingEnrollmentId.ToString();
                Enrollment existingEnrollment = client.GetEnrollment(enrollmentId); // TODO: detect when enrollment with this id does not actually exist
                // make needed enrollment changes, merge ?
                enrollment = client.UpdateEnrollment(enrollmentId, reenrollment);
            }
            else
            {
                // no existing enrollment, create a new one
                enrollment = client.CreateEnrollment(reenrollment, contractId); // TODO: handle 409 when CN already exists
                enrollmentId = enrollment.enrollment.Split('/')[^1]; // last element of the location url is the Enrollment Id
            }

            // update the enrollment and get CSR
            string changeId = enrollment.changes[0].Split('/')[^1]; // last element of the location url is the Change Id
            string csr = client.GetCSR(enrollmentId, changeId, keyType); // need to delay / wait with a 404 present is processed in AKamai

            // submit csr
            var x509Cert = submitReenrollmentUpdate.Invoke(csr);

            // submit certificate info to deployment
            // build PEM content
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            sb.AppendLine(Convert.ToBase64String(x509Cert.RawData, Base64FormattingOptions.InsertLineBreaks));
            sb.AppendLine("-----END CERTIFICATE-----");
            var certContent = sb.ToString();

            certContent = certContent.Replace("\r", "");

            client.PostCertificate(enrollmentId, changeId, certContent, keyType);

            JobResult result = new JobResult()
            {
                JobHistoryId = jobConfiguration.JobHistoryId,
                Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success
            };
            return result;
        }
    }
}
