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

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Keyfactor.Extensions.Utilities.HttpInterface.Exceptions;
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

public class ReenrollmentTests : BaseJobTest<ReenrollmentTests>
{
    // Generated once per test class to avoid RSA key generation cost per test.
    private static readonly X509Certificate2 TestCert = CreateSelfSignedCert();

    private readonly Mock<IAkamaiClientFactory> _mockFactory;
    private readonly Mock<IAkamaiClient> _mockClient;

    public ReenrollmentTests(ITestOutputHelper output) : base(output)
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

    // --- Setup phase ---

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

        var job = new Reenrollment(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenRequiredJobPropertyMissing_ReturnsFailure()
    {
        var config = MakeReenrollmentConfig();
        config.JobProperties.Remove("subjectText"); // required field

        var job = new Reenrollment(Logger, _mockFactory.Object);
        var result = job.ProcessJob(config, _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    // --- Existing enrollment path ---

    [Fact]
    public void ProcessJob_WhenEnrollmentIdPresent_GetsExistingEnrollment()
    {
        _mockClient.Setup(c => c.GetEnrollment("42")).Returns(MakeExistingEnrollment("42"));
        _mockClient
            .Setup(c => c.UpdateEnrollment("42", It.IsAny<Enrollment>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "42", changeId: "200"));
        SetupClientForHappyPath(enrollmentId: "42", changeId: "200");

        var job = new Reenrollment(Logger, _mockFactory.Object);
        job.ProcessJob(MakeReenrollmentConfig(enrollmentId: "42"), _ => TestCert);

        _mockClient.Verify(c => c.GetEnrollment("42"), Times.Once);
    }

    [Fact]
    public void ProcessJob_WhenGetExistingEnrollmentThrows_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.GetEnrollment(It.IsAny<string>()))
            .Throws(new Exception("Enrollment not found"));

        var job = new Reenrollment(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeReenrollmentConfig(enrollmentId: "42"), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenUpdateEnrollmentThrows500_RollsBackAndRetries()
    {
        var existingEnrollment = MakeExistingEnrollment("42", changeLocation: "/cps/v2/enrollments/42/changes/9001");
        _mockClient.Setup(c => c.GetEnrollment("42")).Returns(existingEnrollment);
        _mockClient
            .SetupSequence(c => c.UpdateEnrollment("42", It.IsAny<Enrollment>()))
            .Throws(MakeHttpException(HttpStatusCode.InternalServerError))
            .Returns(MakeCreatedEnrollment(enrollmentId: "42", changeId: "9002"));

        SetupClientForHappyPath(enrollmentId: "42", changeId: "9002");

        var job = new Reenrollment(Logger, _mockFactory.Object);
        job.ProcessJob(MakeReenrollmentConfig(enrollmentId: "42"), _ => TestCert);

        _mockClient.Verify(c => c.DeletePendingChange("42", "9001"), Times.Once);
        _mockClient.Verify(c => c.UpdateEnrollment("42", It.IsAny<Enrollment>()), Times.Exactly(2));
    }

    [Fact]
    public void ProcessJob_WhenUpdateEnrollmentThrows500AndRollbackFails_ReturnsFailure()
    {
        var existingEnrollment = MakeExistingEnrollment("42", changeLocation: "/cps/v2/enrollments/42/changes/9001");
        _mockClient.Setup(c => c.GetEnrollment("42")).Returns(existingEnrollment);
        _mockClient
            .Setup(c => c.UpdateEnrollment("42", It.IsAny<Enrollment>()))
            .Throws(MakeHttpException(HttpStatusCode.InternalServerError));
        _mockClient
            .Setup(c => c.DeletePendingChange("42", "9001"))
            .Throws(new Exception("Delete failed"));

        var job = new Reenrollment(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeReenrollmentConfig(enrollmentId: "42"), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenUpdateEnrollmentThrowsGeneralException_ReturnsFailure()
    {
        _mockClient.Setup(c => c.GetEnrollment("42")).Returns(MakeExistingEnrollment("42"));
        _mockClient
            .Setup(c => c.UpdateEnrollment("42", It.IsAny<Enrollment>()))
            .Throws(new Exception("Unexpected update error"));

        var job = new Reenrollment(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeReenrollmentConfig(enrollmentId: "42"), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    // --- New enrollment path ---

    [Fact]
    public void ProcessJob_WhenNoEnrollmentId_CreatesNewEnrollment()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "99", changeId: "100"));

        SetupClientForHappyPath(enrollmentId: "99", changeId: "100");

        var job = new Reenrollment(Logger, _mockFactory.Object);
        job.ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        _mockClient.Verify(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ProcessJob_WhenCreateEnrollmentThrows409Conflict_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Throws(MakeHttpException(HttpStatusCode.Conflict));

        var job = new Reenrollment(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenCreateEnrollmentThrowsGeneralException_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Throws(new Exception("API error"));

        var job = new Reenrollment(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    // --- CSR retrieval ---

    [Fact]
    public void ProcessJob_WhenGetCSRThrowsNonNotFoundException_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "99", changeId: "100"));
        _mockClient
            .Setup(c => c.GetCSR("99", "100", It.IsAny<string>()))
            .Throws(new Exception("Unexpected CSR error"));

        var job = new Reenrollment(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    // NOTE: Testing GetCSR retry exhaustion (5 × 404 → Failure) requires Thread.Sleep(30s) × 5 = 150s.
    // Extract the delay mechanism to an injectable interface to make this test fast.
    // [Trait("Category", "Slow")]
    // public void ProcessJob_WhenGetCSRConsistentlyReturnsNotFound_ReturnsFailure() { ... }

    // --- Post-CSR steps ---

    [Fact]
    public void ProcessJob_WhenSubmitReenrollmentCSRReturnsNull_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "99", changeId: "100"));
        _mockClient
            .Setup(c => c.GetCSR("99", "100", It.IsAny<string>()))
            .Returns("-----BEGIN CERTIFICATE REQUEST-----\nfakecsr\n-----END CERTIFICATE REQUEST-----");

        var job = new Reenrollment(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeReenrollmentConfig(), _ => null);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenPostCertificateThrows_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "99", changeId: "100"));
        _mockClient
            .Setup(c => c.GetCSR("99", "100", It.IsAny<string>()))
            .Returns("-----BEGIN CERTIFICATE REQUEST-----\nfakecsr\n-----END CERTIFICATE REQUEST-----");
        _mockClient
            .Setup(c => c.PostCertificate(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>()))
            .Throws(new Exception("Upload failed"));

        var job = new Reenrollment(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    // --- Warning acknowledgement ---

    [Fact]
    public void ProcessJob_WhenAcknowledgeWarningsThrowsNonNotFoundException_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "99", changeId: "100"));
        _mockClient
            .Setup(c => c.GetCSR("99", "100", It.IsAny<string>()))
            .Returns("-----BEGIN CERTIFICATE REQUEST-----\nfakecsr\n-----END CERTIFICATE REQUEST-----");
        _mockClient
            .Setup(c => c.AcknowledgeWarnings("99", "100"))
            .Throws(new Exception("Unexpected acknowledgement error"));

        var job = new Reenrollment(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    // NOTE: Testing AcknowledgeWarnings retry exhaustion (4 × 404 → Warning) requires Thread.Sleep(20s) × 4 = 80s.
    // Extract the delay mechanism to an injectable interface to make this test fast.
    // [Trait("Category", "Slow")]
    // public void ProcessJob_WhenAcknowledgeWarningsConsistentlyReturnsNotFound_ReturnsWarning() { ... }

    // --- Happy paths ---

    [Fact]
    public void ProcessJob_WhenCreatingNewEnrollment_ReturnsSuccess()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "99", changeId: "100"));

        SetupClientForHappyPath(enrollmentId: "99", changeId: "100");

        var job = new Reenrollment(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenUpdatingExistingEnrollment_ReturnsSuccess()
    {
        _mockClient.Setup(c => c.GetEnrollment("42")).Returns(MakeExistingEnrollment("42"));
        _mockClient
            .Setup(c => c.UpdateEnrollment("42", It.IsAny<Enrollment>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "42", changeId: "200"));

        SetupClientForHappyPath(enrollmentId: "42", changeId: "200");

        var job = new Reenrollment(Logger, _mockFactory.Object);
        var result = job.ProcessJob(MakeReenrollmentConfig(enrollmentId: "42"), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
    }

    // --- Helpers ---

    /// Sets up the mock client for the post-enrollment steps (GetCSR, PostCertificate,
    /// AcknowledgeWarnings) to succeed, so individual tests can focus on earlier failures.
    private void SetupClientForHappyPath(string enrollmentId, string changeId)
    {
        _mockClient
            .Setup(c => c.GetCSR(enrollmentId, changeId, It.IsAny<string>()))
            .Returns("-----BEGIN CERTIFICATE REQUEST-----\nfakecsr\n-----END CERTIFICATE REQUEST-----");
        // PostCertificate and AcknowledgeWarnings are void — Moq's default is to do nothing,
        // which is the success case for these methods.
    }

    private static ReenrollmentJobConfiguration MakeReenrollmentConfig(string enrollmentId = null)
    {
        var props = new Dictionary<string, object>
        {
            ["subjectText"] = "CN=test.example.com,O=TestOrg,OU=TestOU,L=TestCity,ST=TestState,C=US",
            ["keyType"] = "RSA",
            ["ContractId"] = "contract-123",
            ["Sans"] = "test.example.com&www.test.example.com",
            // admin contact
            ["admin-addressLineOne"] = "123 Main St",
            ["admin-addressLineTwo"] = null,
            ["admin-city"] = "TestCity",
            ["admin-country"] = "US",
            ["admin-email"] = "admin@test.com",
            ["admin-firstName"] = "Admin",
            ["admin-lastName"] = "User",
            ["admin-organizationName"] = "TestOrg",
            ["admin-phone"] = "555-0100",
            ["admin-postalCode"] = "12345",
            ["admin-region"] = "TestState",
            ["admin-title"] = "Admin",
            // org contact
            ["org-addressLineOne"] = "123 Main St",
            ["org-addressLineTwo"] = null,
            ["org-city"] = "TestCity",
            ["org-country"] = "US",
            ["org-organizationName"] = "TestOrg",
            ["org-phone"] = "555-0100",
            ["org-postalCode"] = "12345",
            ["org-region"] = "TestState",
            // tech contact
            ["tech-addressLineOne"] = "123 Main St",
            ["tech-addressLineTwo"] = null,
            ["tech-city"] = "TestCity",
            ["tech-country"] = "US",
            ["tech-email"] = "tech@test.com",
            ["tech-firstName"] = "Tech",
            ["tech-lastName"] = "User",
            ["tech-organizationName"] = "TestOrg",
            ["tech-phone"] = "555-0100",
            ["tech-postalCode"] = "12345",
            ["tech-region"] = "TestState",
            ["tech-title"] = "Tech",
        };

        if (enrollmentId != null)
            props["EnrollmentId"] = enrollmentId;

        return new ReenrollmentJobConfiguration
        {
            JobHistoryId = 1,
            CertificateStoreDetails = new CertificateStore
            {
                Properties = "{}",
                ClientMachine = "test.akamai.example.com",
                StorePath = "Production",
            },
            JobProperties = props,
        };
    }

    private static Enrollment MakeExistingEnrollment(string enrollmentId, string changeLocation = null)
    {
        var changes = changeLocation != null
            ? new[] { new EnrollmentChanges { location = changeLocation } }
            : Array.Empty<EnrollmentChanges>();

        return new Enrollment
        {
            id = enrollmentId,
            csr = new EnrollmentCSR { cn = "existing.example.com" },
            pendingChanges = changes,
        };
    }

    private static CreatedEnrollment MakeCreatedEnrollment(string enrollmentId, string changeId)
    {
        return new CreatedEnrollment
        {
            enrollment = $"/cps/v2/enrollments/{enrollmentId}",
            changes = new[] { $"/cps/v2/enrollments/{enrollmentId}/changes/{changeId}" },
        };
    }

    private static HttpInterfaceException MakeHttpException(HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode);
        response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.akamai.com");
        return new HttpInterfaceException($"HTTP {(int)statusCode}", response);
    }

    private static X509Certificate2 CreateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=test.example.com", rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));
    }
}
