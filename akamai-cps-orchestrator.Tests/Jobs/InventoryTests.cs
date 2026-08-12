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
using akamai_cps_orchestrator.Tests.Builders;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Factories;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Jobs;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace akamai_cps_orchestrator.Tests.Jobs;

public class InventoryTests : BaseJobTest<InventoryTests>
{
    // Generated once per test class to avoid RSA key generation cost per test.
    private static readonly string SelfSignedCertPem = GenerateSelfSignedCertPem();

    private static readonly CertificateInfo SelfSignedCertInfo =
        new AkamaiCertificateInfoBuilder().WithCertificate(SelfSignedCertPem).Build();

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
        var enollment1 = new AkamaiEnrollmentBuilder().WithId("1").Build();
        _mockClient
            .Setup(c => c.GetEnrollments())
            .Returns([enollment1]);
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
        var enrollment1 = new AkamaiEnrollmentBuilder().WithId("1").Build();
        var enrollment2 = new AkamaiEnrollmentBuilder().WithId("2").Build();
        
        _mockClient
            .Setup(c => c.GetEnrollments())
            .Returns([enrollment1, enrollment2]);
        
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
        var enrollment1 = new AkamaiEnrollmentBuilder().WithId("1").Build();
        var enrollment2 = new AkamaiEnrollmentBuilder().WithId("2").Build();
        var enrollment3 = new AkamaiEnrollmentBuilder().WithId("3").Build();
        
        _mockClient
            .Setup(c => c.GetEnrollments())
            .Returns([
                enrollment1,
                enrollment2,
                enrollment3
            ]);
        _mockClient.Setup(c => c.GetCertificate(enrollment1.id))
            .Returns(SelfSignedCertInfo);
        _mockClient.Setup(c => c.GetCertificate(enrollment2.id))
            .Returns((CertificateInfo)null);
        _mockClient.Setup(c => c.GetCertificate(enrollment3.id))
            .Returns(SelfSignedCertInfo);

        IEnumerable<CurrentInventoryItem> submitted = null;
        var job = new Inventory(Logger, _mockFactory.Object);
        job.ProcessJob(MakeInventoryConfig(), items => { submitted = items; return true; });

        Assert.Equal(2, submitted.Count());
    }
    
    #region Entry Parameters

    [Fact]
    public void ProcessJob_WhenCertificatesPresent_SetsEnrollmentIdParameter()
    {
        var enrollment = new AkamaiEnrollmentBuilder().WithId("1").Build();
        SetupHappyPath(enrollment, SelfSignedCertInfo);

        AssertEntryParameter(Constants.EntryParameters.EnrollmentId, enrollment.id);
    }
    
    [Theory]
    [InlineData(Constants.DeploymentNetwork.Akamai.StandardTLS, Constants.DeploymentNetwork.Command.StandardTLS)]
    [InlineData(Constants.DeploymentNetwork.Akamai.EnhancedTLS, Constants.DeploymentNetwork.Command.EnhancedTLS)]
    public void ProcessJob_WhenSecureNetworkIsSet_SetsDeploymentNetworkParameter(string secureNetwork, string expectedDeploymentNetwork)
    {
        var enrollment = new AkamaiEnrollmentBuilder().WithSecureNetwork(secureNetwork).Build();
        SetupHappyPath(enrollment, SelfSignedCertInfo);

        AssertEntryParameter(Constants.EntryParameters.DeploymentNetwork, expectedDeploymentNetwork);
    }
    
    [Fact]
    public void ProcessJob_WhenSecureNetworkCannotBeMapped_ThrowsException()
    {
        string secureNetwork = "foobar";
        var enrollment = new AkamaiEnrollmentBuilder().WithId("1").WithSecureNetwork(secureNetwork).Build();
        SetupHappyPath(enrollment, SelfSignedCertInfo);

        var job = new Inventory(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeInventoryConfig(), _ => false);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Contains($"Could not map SecureNetwork value '{secureNetwork}' on enrollment ID {enrollment.id} to a valid deployment-network value",
            result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_WhenCertificatesPresent_SetsSansParameter()
    {
        var enrollment = new AkamaiEnrollmentBuilder()
            .WithSans(new[] { "a.example.com", "b.example.com" })
            .Build();
        SetupHappyPath(enrollment, SelfSignedCertInfo);

        AssertEntryParameter(Constants.EntryParameters.Sans, "a.example.com&b.example.com");
    }

    [Fact]
    public void ProcessJob_WhenCertificatesPresent_SetsAdminContactParameters()
    {
        var adminContact = new ContactInfo
        {
            addressLineOne = "123 Main St",
            addressLineTwo = "Suite 100",
            city = "Atlanta",
            country = "US",
            email = "admin@example.com",
            firstName = "Ada",
            lastName = "Lovelace",
            organizationName = "TestOrg",
            phone = "555-0100",
            postalCode = "30301",
            region = "GA",
            title = "Administrator",
        };
        var enrollment = new AkamaiEnrollmentBuilder().WithAdminContact(adminContact).Build();
        SetupHappyPath(enrollment, SelfSignedCertInfo);

        AssertEntryParameters(new Dictionary<string, string>
        {
            [Constants.EntryParameters.Admin.AddressLineOne] = adminContact.addressLineOne,
            [Constants.EntryParameters.Admin.AddressLineTwo] = adminContact.addressLineTwo,
            [Constants.EntryParameters.Admin.City] = adminContact.city,
            [Constants.EntryParameters.Admin.Country] = adminContact.country,
            [Constants.EntryParameters.Admin.Email] = adminContact.email,
            [Constants.EntryParameters.Admin.FirstName] = adminContact.firstName,
            [Constants.EntryParameters.Admin.LastName] = adminContact.lastName,
            [Constants.EntryParameters.Admin.OrganizationName] = adminContact.organizationName,
            [Constants.EntryParameters.Admin.Phone] = adminContact.phone,
            [Constants.EntryParameters.Admin.PostalCode] = adminContact.postalCode,
            [Constants.EntryParameters.Admin.Region] = adminContact.region,
            [Constants.EntryParameters.Admin.Title] = adminContact.title,
        });
    }

    [Fact]
    public void ProcessJob_WhenCertificatesPresent_SetsOrgContactParameters()
    {
        // Akamai's "org" contact uses a "name" field for the organization name, unlike admin/tech
        // contacts which use "organizationName" - see BuildOrgContact in Reenrollment.cs.
        var orgContact = new ContactInfo
        {
            addressLineOne = "456 Peachtree St",
            addressLineTwo = "Floor 2",
            city = "Atlanta",
            country = "US",
            name = "Test Organization",
            phone = "555-0200",
            postalCode = "30302",
            region = "GA",
        };
        var enrollment = new AkamaiEnrollmentBuilder().WithOrgContact(orgContact).Build();
        SetupHappyPath(enrollment, SelfSignedCertInfo);

        AssertEntryParameters(new Dictionary<string, string>
        {
            [Constants.EntryParameters.Org.AddressLineOne] = orgContact.addressLineOne,
            [Constants.EntryParameters.Org.AddressLineTwo] = orgContact.addressLineTwo,
            [Constants.EntryParameters.Org.City] = orgContact.city,
            [Constants.EntryParameters.Org.Country] = orgContact.country,
            [Constants.EntryParameters.Org.OrganizationName] = orgContact.name,
            [Constants.EntryParameters.Org.Phone] = orgContact.phone,
            [Constants.EntryParameters.Org.PostalCode] = orgContact.postalCode,
            [Constants.EntryParameters.Org.Region] = orgContact.region,
        });
    }

    [Fact]
    public void ProcessJob_WhenCertificatesPresent_SetsTechContactParameters()
    {
        var techContact = new ContactInfo
        {
            addressLineOne = "150 Broadway",
            addressLineTwo = "Suite 400",
            city = "Cambridge",
            country = "US",
            email = "tech@akamai.com",
            firstName = "R2",
            lastName = "D2",
            organizationName = "Akamai",
            phone = "555-0300",
            postalCode = "02142",
            region = "MA",
            title = "Technical Engineer",
        };
        var enrollment = new AkamaiEnrollmentBuilder().WithTechContact(techContact).Build();
        SetupHappyPath(enrollment, SelfSignedCertInfo);

        AssertEntryParameters(new Dictionary<string, string>
        {
            [Constants.EntryParameters.Tech.AddressLineOne] = techContact.addressLineOne,
            [Constants.EntryParameters.Tech.AddressLineTwo] = techContact.addressLineTwo,
            [Constants.EntryParameters.Tech.City] = techContact.city,
            [Constants.EntryParameters.Tech.Country] = techContact.country,
            [Constants.EntryParameters.Tech.Email] = techContact.email,
            [Constants.EntryParameters.Tech.FirstName] = techContact.firstName,
            [Constants.EntryParameters.Tech.LastName] = techContact.lastName,
            [Constants.EntryParameters.Tech.OrganizationName] = techContact.organizationName,
            [Constants.EntryParameters.Tech.Phone] = techContact.phone,
            [Constants.EntryParameters.Tech.PostalCode] = techContact.postalCode,
            [Constants.EntryParameters.Tech.Region] = techContact.region,
            [Constants.EntryParameters.Tech.Title] = techContact.title,
        });
    }

    #endregion

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

    private void SetupHappyPath(Enrollment enrollment, CertificateInfo certInfo)
    {
        _mockClient
            .Setup(c => c.GetEnrollments())
            .Returns([enrollment]);
        _mockClient
            .Setup(c => c.GetCertificate(enrollment.id))
            .Returns(certInfo);
    }

    private void AssertEntryParameter(string entryParameterKey, string expectedValue)
    {
        AssertEntryParameters(new Dictionary<string, string> { [entryParameterKey] = expectedValue });
    }

    private void AssertEntryParameters(Dictionary<string, string> expected)
    {
        IEnumerable<CurrentInventoryItem> submitted = null;
        var job = new Inventory(Logger, _mockFactory.Object);
        job.ProcessJob(MakeInventoryConfig(), items => { submitted = items; return true; });

        var parameters = submitted.First().Parameters;
        foreach (var (key, expectedValue) in expected)
        {
            Assert.Equal(expectedValue, parameters[key]?.ToString());
        }
    }

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
