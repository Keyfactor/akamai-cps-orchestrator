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
            var props = JsonConvert.DeserializeObject<Dictionary<string, string>>(jobConfiguration.CertificateStoreDetails.Properties);
            AkamaiAuth auth = new AkamaiAuth(props["ClientSecret"], props["ClientToken"], props["AccessToken"]);
            AkamaiClient client = new AkamaiClient(jobConfiguration.CertificateStoreDetails.ClientMachine, auth)
            {
                //Username = jobConfiguration.ServerUsername,
                //ApiKey = jobConfiguration.ServerPassword
            };

            var allJobProps = jobConfiguration.JobProperties;
            var allStoreProps = jobConfiguration.CertificateStoreDetails.Properties;

            //string enrollmentId = allJobProps["EnrollmentId"].ToString();

            client.SetDeploymentType(jobConfiguration.CertificateStoreDetails.StorePath);

            // look up all enrollments instead and get certificates - don't have a specific enrollment id to lookup for the entire cert store
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
