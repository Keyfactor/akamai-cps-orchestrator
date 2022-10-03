using System;
using System.Collections.Generic;
using System.Text;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    // used to update a change with the enrolled certificate
    public class CertificateInfo
    {
        public string certificate;
        public string keyAlgorithm;
        public string trustChain;
    }
}
