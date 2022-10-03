using System;
using System.Collections.Generic;
using System.Text;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    class Deployment
    {
        public CertificateStack production;
        public CertificateStack staging;
    }

    class CertificateStack
    {
        public CertificateInfo primaryCertificate;
        public CertificateInfo[] multiStackedCertificates;
    }
}
