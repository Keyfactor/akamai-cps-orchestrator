// Copyright 2023 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
            // 'post-verification-warnings-ack' is a param for acknowleging warnings after uploading a cert
            public static string AcknowledgePostVerification = Changes + "/{1}/input/update/post-verification-warnings-ack";

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
