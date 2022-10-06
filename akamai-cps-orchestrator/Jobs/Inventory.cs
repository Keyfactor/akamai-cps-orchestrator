using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Jobs
{
    public class Inventory : IInventoryJobExtension
    {
        public string ExtensionName => "Akamai";

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
        {
            AkamaiAuth auth = new AkamaiAuth();
            AkamaiClient client = new AkamaiClient(jobConfiguration.CertificateStoreDetails.ClientMachine, auth);

            client.SetDeploymentType(jobConfiguration.CertificateStoreDetails.StorePath);

            // look up all enrollments and get certificates from each enrollment
            Enrollment[] enrollments = client.GetEnrollments();
            var inventory = new List<CurrentInventoryItem>();
            foreach(var enrollment in enrollments)
            {
                CertificateInfo cert = client.GetCertificate(enrollment.id);
                if (cert != null) // some enrollments found will not have a cert of the same deployment type
                {
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
            }

            bool success = submitInventoryUpdate.Invoke(inventory);

            JobResult result = new JobResult()
            {
                JobHistoryId = jobConfiguration.JobHistoryId,
                Result = success ? Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success : Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure
            };

            return result;
        }
    }
}
