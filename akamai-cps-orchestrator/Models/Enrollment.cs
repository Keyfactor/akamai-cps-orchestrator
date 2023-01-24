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

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    // create an enrollment to generate a change CSR
    public class Enrollment
    {
        public string id;
        public ContactInfo adminContact;
        //public string certificateChainType = "default"; // not in v4 object
        public string certificateType = "third-party";
        public bool changeManagement = false;
        public EnrollmentCSR csr;
        public bool enableMultiStackedCertificates = false;
        public NetworkConfiguration networkConfiguration = new NetworkConfiguration();
        public ContactInfo org;
        public string ra = "third-party";
        public string signatureAlgorithm = "SHA-256";
        public ContactInfo techContact;
        public ThirdParty thirdParty = new ThirdParty();
        public string validationType = "third-party";
    }

    public class Enrollments
    {
        public Enrollment[] enrollments;
    }

    public class EnrollmentCSR
    {
        public string c;
        public string cn;
        public string l;
        public string o;
        public string ou;
        public string preferredTrustChain = null;
        public string[] sans = new string[0];
        public string st;
    }

    public class CreatedEnrollment
    {
        public string[] changes;
        public string enrollment;
    }

    public class ThirdParty
    {
        public bool excludeSans = false;
    }

    public class ContactInfo
    {
        public string addressLineOne;
        public string addressLineTwo;
        public string city;
        public string country;
        public string email;
        public string name;
        public string firstName;
        public string lastName;
        public string organizationName;
        public string phone;
        public string postalCode;
        public string region;
        public string title;
    }
}
