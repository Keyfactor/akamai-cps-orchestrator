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
// ReSharper disable InconsistentNaming

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    public static class Constants
    {
        public static class Endpoints
        {
            public const string Enrollments = "/cps/v2/enrollments";

            // str replace {0} with enrollmentId
            public const string Changes = Enrollments + "/{0}/changes";

            // str replace {1} with changeId
            // 'third-party-csr' is a param for getting a Third-Party csr
            public const string GetChange = Changes + "/{1}/input/info/third-party-csr";

            // str replace {1} with changeId
            // 'third-party-cert-and-trust-chain' is a param for uploading a Third-Party cert
            public const string UpdateChange = Changes + "/{1}/input/update/third-party-cert-and-trust-chain";
            // 'post-verification-warnings-ack' is a param for acknowleging warnings after uploading a cert
            public const string AcknowledgePostVerification = Changes + "/{1}/input/update/post-verification-warnings-ack";

            // str replace {1} with changeId
            public const string UpdateDeployment = Changes + "/{1}/deployment-schedule";

            // str replace {0} with enrollmentId
            public const string Deployments = Enrollments + "/{0}/deployments";
        }

        public static class StorePaths
        {
            public const string Production = "Production";
            public const string Staging = "Staging";
        }

        public static class EntryParameters
        {
            public const string EnrollmentId = "EnrollmentId";
            public const string DeploymentNetwork = "deployment-network";
            public const string Sans = "Sans";

            public static class Admin
            {
                public const string AddressLineOne = "admin-addressLineOne";
                public const string AddressLineTwo = "admin-addressLineTwo";
                public const string City = "admin-city";
                public const string Country = "admin-country";
                public const string Email = "admin-email";
                public const string FirstName = "admin-firstName";
                public const string LastName = "admin-lastName";
                public const string OrganizationName = "admin-organizationName";
                public const string Phone = "admin-phone";
                public const string PostalCode = "admin-postalCode";
                public const string Region = "admin-region";
                public const string Title = "admin-title";
            }

            public static class Org
            {
                public const string AddressLineOne = "org-addressLineOne";
                public const string AddressLineTwo = "org-addressLineTwo";
                public const string City = "org-city";
                public const string Country = "org-country";
                public const string OrganizationName = "org-organizationName";
                public const string Phone = "org-phone";
                public const string PostalCode = "org-postalCode";
                public const string Region = "org-region";
            }

            public static class Tech
            {
                public const string AddressLineOne = "tech-addressLineOne";
                public const string AddressLineTwo = "tech-addressLineTwo";
                public const string City = "tech-city";
                public const string Country = "tech-country";
                public const string Email = "tech-email";
                public const string FirstName = "tech-firstName";
                public const string LastName = "tech-lastName";
                public const string OrganizationName = "tech-organizationName";
                public const string Phone = "tech-phone";
                public const string PostalCode = "tech-postalCode";
                public const string Region = "tech-region";
                public const string Title = "tech-title";
            }
        }

        public static class DeploymentNetwork
        {
            public static class Akamai
            {
                public const string StandardTLS = "standard-tls";
                public const string EnhancedTLS = "enhanced-tls";
            }

            public static class Command
            {
                public const string StandardTLS = "Standard TLS";
                public const string EnhancedTLS = "Enhanced TLS";
            }
        }
    }
}
