using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Keyfactor.Logging;
using Keyfactor.PKI.Enums;
using Keyfactor.PKI.X509;
using Microsoft.Extensions.Logging;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Services;

public interface ICertificateChainService
{
    CertificateCollection BuildCertificateCollection(X509Certificate certificate);
}

public class CertificateChainService : ICertificateChainService
{
    private readonly ILogger _logger = LogHandler.GetClassLogger<CertificateChainService>();

    public CertificateCollection? BuildCertificateCollection(X509Certificate certificate)
    {
        try
        {
            _logger.LogTrace("Building certificate chain for certificate with serial number {serialNumber}",
                certificate.SerialNumber);

            using var http = new HttpClient();
            var store = new CertificateAuthorityCertificateCache();
            var builder = new ChainBuilder(store, http);

            _logger.LogTrace("ChainBuilder initialized. Calling BuildChain method on ChainBuilder for certificate with serial number {thumbprint}",
                certificate.SerialNumber);

            var chain = builder.BuildChain(certificate, CertificateCollectionOrder.EndEntityFirst);

            _logger.LogDebug(
                "Successfully built certificate chain for certificate with serial number {thumbprint}. Certificates count: {certificatesCount}, Chain count: {chainLength}",
                certificate.SerialNumber, chain.Certificates.Count(), chain.ChainCertificates.Count());

            for (var i = 0; i < chain.ChainCertificates.Count(); i++)
            {
                var element = chain.ChainCertificates.ElementAt(i);
                _logger.LogTrace(
                    "ChainCertificate element {index} in chain has subject DN {subject} and thumbprint {thumbprint}",
                    i + 1, element.SubjectDN, element.SerialNumber);
            }

            return new CertificateCollection
            {
                EndEntityCert = certificate,
                ChainCerts = chain.ChainCertificates,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("An error occurred while building the certificate chain: {errorMessage}", ex.Message);
            
            // If a chain could not be built, at best return just the end entity cert.
            return new CertificateCollection()
            {
                EndEntityCert = certificate,
                ChainCerts = null
            };
        }
    }
}

public class CertificateCollection
{
    public X509Certificate EndEntityCert { get; set; }
    public IEnumerable<X509Certificate>? ChainCerts { get; set; }
}
