using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Extensions.Orchestrator.AkamaiCpsOrchestrator.Models;
using System;

namespace Keyfactor.Extensions.Orchestrator.AkamaiCpsOrchestrator.Jobs
{
    public class Inventory : IInventoryJobExtension
    {
        public string ExtensionName => "Akamai";

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration, SubmitInventoryUpdate submitInventoryUpdate)
        {
            AkamaiAuth auth = new AkamaiAuth();
            AkamaiClient client = new AkamaiClient(jobConfiguration.CertificateStoreDetails.ClientMachine, auth)
            {
                Username = jobConfiguration.ServerUsername,
                ApiKey = jobConfiguration.ServerPassword
            };

            client.SetDeploymentType(jobConfiguration.CertificateStoreDetails.StorePath);

            CertificateInfo cert = client.GetCertificate();

            CurrentInventoryItem[] inventory = new CurrentInventoryItem[]
            {
                new CurrentInventoryItem()
                {
                    Certificates = new string[] { cert.certificate },
                    ItemStatus = Orchestrators.Common.Enums.OrchestratorInventoryItemStatus.New
                }
            };
            bool success = submitInventoryUpdate.Invoke(inventory);

            JobResult result = new JobResult()
            {
                Result = Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success
            };
            return result;
        }
    }
}
