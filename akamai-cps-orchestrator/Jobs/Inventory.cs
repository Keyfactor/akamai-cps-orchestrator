using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;

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
                inventory.Add(
                    new CurrentInventoryItem()
                    {
                        Certificates = new string[] { cert.certificate },
                        ItemStatus = Orchestrators.Common.Enums.OrchestratorInventoryItemStatus.New
                    }
                );
            }

            bool success = submitInventoryUpdate.Invoke(inventory);

            JobResult result = new JobResult()
            {
                Result = success ? Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success : Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure
            };
            return result;
        }
    }
}
