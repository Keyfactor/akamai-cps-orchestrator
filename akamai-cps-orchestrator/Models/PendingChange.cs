using System;
using System.Collections.Generic;
using System.Text;

namespace Keyfactor.Extensions.Orchestrator.AkamaiCpsOrchestrator.Models
{
    // get the CSR generated for an enrollment change
    public class PendingChange
    {
        public PendingCSRs[] csrs;
    }

    public class PendingCSRs
    {
        public string csr;
        public string keyAlgorithm;
    }
}
