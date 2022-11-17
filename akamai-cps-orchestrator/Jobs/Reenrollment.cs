using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using Microsoft.Extensions.Logging;
using Keyfactor.Logging;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Jobs
{
    public class Reenrollment : IReenrollmentJobExtension
    {
        public string ExtensionName => "Akamai";

        public JobResult ProcessJob(ReenrollmentJobConfiguration jobConfiguration, SubmitReenrollmentCSR submitReenrollmentUpdate)
        {
            ILogger logger = LogHandler.GetClassLogger<Reenrollment>();
            AkamaiAuth auth = new AkamaiAuth();
            AkamaiClient client = new AkamaiClient(logger, jobConfiguration.CertificateStoreDetails.ClientMachine, auth);

            string enrollmentType = jobConfiguration.CertificateStoreDetails.StorePath;
            client.SetDeploymentType(enrollmentType);

            logger.LogTrace("Populating enrollment request information.");
            CreatedEnrollment enrollment;
            var allJobProps = jobConfiguration.JobProperties; // contains entry parameters - EnrollmentId, ContractId
            string subject = allJobProps["subjectText"].ToString();
            string keyType = allJobProps["keyType"].ToString();
            string contractId = allJobProps["ContractId"].ToString();
            string sans = allJobProps["Sans"].ToString(); // colon split sans

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
                    st = subjectValues.GetValueOrDefault("ST"),
                    // split sans entered
                    sans = sans.Split(',')
                }
            };

            logger.LogTrace("Loading contact info from file.");
            string extensionDirectory = Path.GetDirectoryName(this.GetType().Assembly.Location);
            string jsonContactInfo = File.ReadAllText($"{extensionDirectory}{Path.DirectorySeparatorChar}config.json");
            JsonConvert.PopulateObject(jsonContactInfo, reenrollment);
            logger.LogTrace("Enrollment request information finished populating.");

            // if not present as an entry parameter, need to make a new enrollment
            string enrollmentId;
            bool enrollmentExists = allJobProps.TryGetValue("EnrollmentId", out object existingEnrollmentId);
            if (enrollmentExists && existingEnrollmentId != null)
            {
                enrollmentId = existingEnrollmentId.ToString();
                logger.LogDebug($"Looking for existing enrollment - {enrollmentId}");
                Enrollment existingEnrollment = client.GetEnrollment(enrollmentId); // TODO: detect when enrollment with this id does not actually exist

                // use existing enrollment information, with reenrollment CSR data
                existingEnrollment.csr = reenrollment.csr;

                logger.LogDebug($"Found existing enrollment - {enrollmentId}");
                enrollment = client.UpdateEnrollment(enrollmentId, existingEnrollment);
                logger.LogInformation($"Updated existing enrollment - {enrollmentId}");
            }
            else
            {
                // no existing enrollment, create a new one
                enrollment = client.CreateEnrollment(reenrollment, contractId); // TODO: handle 409 when CN already exists
                enrollmentId = enrollment.enrollment.Split('/')[^1]; // last element of the location url is the Enrollment Id
                logger.LogInformation($"Created new enrollment - {enrollmentId}");
            }

            // update the enrollment and get CSR
            string changeId = enrollment.changes[0].Split('/')[^1]; // last element of the location url is the Change Id
            logger.LogDebug("Retrieving CSR for enrollment change.");

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
                    logger.LogTrace("Akamai CSR not ready yet. Sleeping process to try again.");
                    retryCount++;
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(30));
                }
            }

            // if no CSR received, fail job
            if (string.IsNullOrEmpty(csr))
            {
                logger.LogError($"Maximum retry count reached. CSR was not finished processing for new enrollment - {enrollmentId}");
                JobResult errorResult = new JobResult()
                {
                    JobHistoryId = jobConfiguration.JobHistoryId,
                    Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure
                };
                return errorResult;
            }

            logger.LogDebug("Retrieved CSR content for enrollment change.");

            // submit csr
            logger.LogTrace("Submitting CSR to Keyfactor");
            var x509Cert = submitReenrollmentUpdate.Invoke(csr);
            logger.LogDebug("Certificate returned from CSR enrollment.");

            // submit certificate info to deployment
            // build PEM content
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            sb.AppendLine(Convert.ToBase64String(x509Cert.RawData, Base64FormattingOptions.InsertLineBreaks));
            sb.AppendLine("-----END CERTIFICATE-----");
            var certContent = sb.ToString();

            certContent = certContent.Replace("\r", "");

            logger.LogTrace("Posting certificate to Akamai.");
            client.PostCertificate(enrollmentId, changeId, certContent, keyType);
            logger.LogInformation($"Certificate uploaded for enrollment - {enrollmentId}");

            // akcnowledge warnings to force deployment
            logger.LogTrace("Acknowledging warnings for finished enrollment.");
            client.AcknowledgeWarnings(enrollmentId, changeId);
            logger.LogDebug($"Warnings acknowleged for enrollment - {enrollmentId}");

            JobResult result = new JobResult()
            {
                JobHistoryId = jobConfiguration.JobHistoryId,
                Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success
            };
            return result;
        }
    }
}
