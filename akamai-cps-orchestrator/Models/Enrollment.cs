using System;
using System.Collections.Generic;
using System.Text;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    // create an enrollment to generate a change CSR
    public class Enrollment
    {
        public string id;
        public string certificateType = "third-party";
        public bool changeManagement = false;
        public EnrollmentCSR csr;
        public bool enableMultiStackedCertificates;
        public string ra = "third-party";
        public string validationType = "third-party";
    }

    public class EnrollmentCSR
    {
        public string c;
        public string cn;
        public string l;
        public string o;
        public string ou;
        public string preferredTrustChain;
        public string[] sans;
        public string st;
    }

    public class CreatedEnrollment
    {
        public string[] changes;
        public string enrollment;
    }
}
