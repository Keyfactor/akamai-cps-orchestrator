using System;
using System.Collections.Generic;
using System.Text;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    public static class Constants
    {
        public static class Endpoints
        {
            public static string Enrollments = "/cps/v2/enrollments";

            // str replace {0} with enrollmentId
            public static string Changes = Enrollments + "/{0}/changes";

            // str replace {1} with changeId
            // 'third-party-csr' is a param for getting a Third-Party csr
            public static string GetChange = Changes + "/{1}/input/info/third-party-csr";

            // str replace {1} with changeId
            // 'third-party-cert-and-trust-chain' is a param for uploading a Third-Party cert
            public static string UpdateChange = Changes + "/{1}/input/update/third-party-cert-and-trust-chain";

            // str replace {1} with changeId
            public static string UpdateDeployment = Changes + "/{1}/deployment-schedule";

            // str replace {0} with enrollmentId
            public static string Deployments = Enrollments + "/{0}/deployments";
        }

        public static class StorePaths
        {
            public static string Production = "Production";
            public static string Staging = "Staging";
        }
    }
}
