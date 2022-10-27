using System;
using System.Collections.Generic;
using System.Text;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    // get the CSR generated for an enrollment change
    public class PendingChange
    {
        public PendingCSR[] csrs;
    }

    public class PendingCSR
    {
        public string csr;
        public string keyAlgorithm;
    }
}
