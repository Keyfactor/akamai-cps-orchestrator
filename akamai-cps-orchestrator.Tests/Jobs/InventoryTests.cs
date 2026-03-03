// Copyright 2026 Keyfactor
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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Factories;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Jobs;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace akamai_cps_orchestrator.Tests.Jobs;

public class InventoryTests : BaseJobTest<InventoryTests>
{
    // Generated once per test class to avoid RSA key generation cost per test.
    private static readonly string SelfSignedCertPem = GenerateSelfSignedCertPem();

    private readonly Mock<IAkamaiClientFactory> _mockFactory;
    private readonly Mock<IAkamaiClient> _mockClient;

    public InventoryTests(ITestOutputHelper output): base(output)
    {
        _mockFactory = new Mock<IAkamaiClientFactory>();
        _mockClient = new Mock<IAkamaiClient>();

        // Default: factory successfully returns the mock client for any inputs.
        _mockFactory
            .Setup(f => f.Create(
                It.IsAny<ILogger>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(_mockClient.Object);
    }

    [Fact]
    public void ProcessJob_WhenClientFactoryThrows_ReturnsFailure()
    {
        _mockFactory
            .Setup(f => f.Create(
                It.IsAny<ILogger>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Throws(new Exception("Factory setup failed"));

        var job = new Inventory(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeInventoryConfig(), _ => true);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenGetEnrollmentsThrows_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.GetEnrollments())
            .Throws(new Exception("Akamai API unavailable"));

        var job = new Inventory(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeInventoryConfig(), _ => true);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenGetCertificateThrows_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.GetEnrollments())
            .Returns(new[] { new Enrollment { id = "1" } });
        _mockClient
            .Setup(c => c.GetCertificate("1"))
            .Throws(new Exception("Certificate fetch failed"));

        var job = new Inventory(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeInventoryConfig(), _ => true);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenAllCertificatesNull_SubmitsEmptyInventory()
    {
        _mockClient
            .Setup(c => c.GetEnrollments())
            .Returns(new[] { new Enrollment { id = "1" }, new Enrollment { id = "2" } });
        _mockClient
            .Setup(c => c.GetCertificate(It.IsAny<string>()))
            .Returns((CertificateInfo)null);

        IEnumerable<CurrentInventoryItem> submitted = null;
        var job = new Inventory(Logger, _mockFactory.Object);
        job.ProcessJob(MakeInventoryConfig(), items => { submitted = items; return true; });

        Assert.Empty(submitted);
    }

    [Fact]
    public void ProcessJob_WhenSomeCertificatesNull_SkipsNullEntries()
    {
        _mockClient
            .Setup(c => c.GetEnrollments())
            .Returns(new[]
            {
                new Enrollment { id = "1" },
                new Enrollment { id = "2" },
                new Enrollment { id = "3" },
            });
        _mockClient.Setup(c => c.GetCertificate("1"))
            .Returns(new CertificateInfo { certificate = SelfSignedCertPem });
        _mockClient.Setup(c => c.GetCertificate("2"))
            .Returns((CertificateInfo)null);
        _mockClient.Setup(c => c.GetCertificate("3"))
            .Returns(new CertificateInfo { certificate = SelfSignedCertPem });

        IEnumerable<CurrentInventoryItem> submitted = null;
        var job = new Inventory(Logger, _mockFactory.Object);
        job.ProcessJob(MakeInventoryConfig(), items => { submitted = items; return true; });

        Assert.Equal(2, submitted.Count());
    }

    [Fact]
    public void ProcessJob_WhenCertificatesPresent_SetsEnrollmentIdParameter()
    {
        _mockClient
            .Setup(c => c.GetEnrollments())
            .Returns(new[] { new Enrollment { id = "42" } });
        _mockClient
            .Setup(c => c.GetCertificate("42"))
            .Returns(new CertificateInfo { certificate = SelfSignedCertPem });

        IEnumerable<CurrentInventoryItem> submitted = null;
        var job = new Inventory(Logger, _mockFactory.Object);
        job.ProcessJob(MakeInventoryConfig(), items => { submitted = items; return true; });

        Assert.Single(submitted);
        Assert.Equal("42", submitted.First().Parameters["EnrollmentId"].ToString());
    }

    [Fact]
    public void ProcessJob_WhenSubmitInventoryReturnsFalse_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.GetEnrollments())
            .Returns(Array.Empty<Enrollment>());

        var job = new Inventory(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeInventoryConfig(), _ => false);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_HappyPath_ReturnsSuccess()
    {
        _mockClient
            .Setup(c => c.GetEnrollments())
            .Returns(Array.Empty<Enrollment>());

        var job = new Inventory(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeInventoryConfig(), _ => true);

        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
    }

    // --- Helpers ---

    private static InventoryJobConfiguration MakeInventoryConfig(string storePath = "Production")
    {
        return new InventoryJobConfiguration
        {
            JobHistoryId = 1,
            CertificateStoreDetails = new CertificateStore
            {
                Properties = "{}",
                ClientMachine = "test.akamai.example.com",
                StorePath = storePath,
            }
        };
    }

    private static string GenerateSelfSignedCertPem()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=test.example.com", rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));
        var b64 = Convert.ToBase64String(
            cert.Export(X509ContentType.Cert),
            Base64FormattingOptions.InsertLineBreaks);
        return $"-----BEGIN CERTIFICATE-----\n{b64}\n-----END CERTIFICATE-----";
    }
}
