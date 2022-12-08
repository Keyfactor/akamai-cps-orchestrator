using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Jobs
{
    public class Inventory : IInventoryJobExtension
    {
        public string ExtensionName => "Akamai";

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
        {
            ILogger logger = LogHandler.GetClassLogger<Inventory>();
            AkamaiAuth auth = new AkamaiAuth(jobConfiguration.JobProperties);
            AkamaiClient client = new AkamaiClient(logger, jobConfiguration.CertificateStoreDetails.ClientMachine, auth);

            string enrollmentType = jobConfiguration.CertificateStoreDetails.StorePath;
            client.SetDeploymentType(enrollmentType);

            // look up all enrollments and get certificates from each enrollment
            Enrollment[] enrollments = client.GetEnrollments();
            var inventory = new List<CurrentInventoryItem>();
            logger.LogDebug($"Found {enrollments.Length} total enrollments.");
            foreach(var enrollment in enrollments)
            {
                logger.LogTrace($"Attempting to retrieve {enrollmentType} certifcate from enrollment {enrollment.id}");
                CertificateInfo cert = client.GetCertificate(enrollment.id);
                if (cert != null) // some enrollments found will not have a cert of the same deployment type
                {
                    logger.LogTrace($"Found certificate for enrollment {enrollment.id} of type {enrollmentType}.");
                    var x509Cert = new X509Certificate2(Encoding.UTF8.GetBytes(cert.certificate));
                    inventory.Add(
                        new CurrentInventoryItem()
                        {
                            Certificates = new string[] { Convert.ToBase64String(x509Cert.Export(X509ContentType.Cert)) },
                            ItemStatus = Orchestrators.Common.Enums.OrchestratorInventoryItemStatus.Unknown,
                            PrivateKeyEntry = false,
                            UseChainLevel = false,
                            Alias = x509Cert.Thumbprint,
                            Parameters = new Dictionary<string, object>
                            {
                                { "EnrollmentId", enrollment.id }
                            }
                        }
                    );
                }
                else
                {
                    logger.LogTrace($"Enrollment {enrollment.id} did not have a certificate of type {enrollmentType}.");
                }
            }

            logger.LogInformation($"Inventory result: {enrollments.Length} total enrollments found, with {inventory.Count} certificates inventoried for {enrollmentType} type.");
            bool success = submitInventoryUpdate.Invoke(inventory);
            logger.LogTrace("Inventory resulst submitted to Keyfactor.");

            JobResult result = new JobResult()
            {
                JobHistoryId = jobConfiguration.JobHistoryId,
                Result = success ? Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success : Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure
            };
            return result;
        }
    }
}
