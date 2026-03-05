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
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Services;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using Org.BouncyCastle.X509;
using Xunit;
using Xunit.Abstractions;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace akamai_cps_orchestrator.Tests.Jobs;

public class ReenrollmentTests : BaseJobTest<ReenrollmentTests>
{
    private const string FakeCsr = "-----BEGIN CERTIFICATE REQUEST-----\nfakecsr\n-----END CERTIFICATE REQUEST-----";

    // Generated once per test class to avoid RSA key generation cost per test.
    private static readonly X509Certificate2 TestCert = CreateSelfSignedCert();
    private static readonly X509Certificate TestBcCert = CreateSelfSignedCertBouncyCastle(TestCert);

    private readonly Mock<IAkamaiClientFactory> _mockFactory = new();
    private readonly Mock<IAkamaiClient> _mockClient = new();
    private readonly Mock<ITimerService> _mockTimerService = new();
    private readonly Mock<ICertificateChainService> _mockCertificateChainService = new();

    public ReenrollmentTests(ITestOutputHelper output) : base(output)
    {
        // Default: factory successfully returns the mock client for any inputs.
        _mockFactory
            .Setup(f => f.Create(
                It.IsAny<ILogger>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(_mockClient.Object);

        // Default: chain service returns a valid chain (end entity + one chain cert).
        // Tests that exercise chain-building behavior override this in the test body.
        _mockCertificateChainService
            .Setup(s => s.BuildCertificateCollection(It.IsAny<X509Certificate>()))
            .Returns(new CertificateCollection
            {
                EndEntityCert = TestBcCert,
                ChainCerts = new List<X509Certificate> { TestBcCert },
            });
    }

    #region Errors During Setup

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

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenRequiredJobPropertyMissing_ReturnsFailure()
    {
        var config = MakeReenrollmentConfig();
        config.JobProperties.Remove("subjectText");

        var result = GetReenrollmentClass().ProcessJob(config, _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }
    
    #endregion

    #region Existing Enrollments

    [Fact]
    public void ProcessJob_WhenEnrollmentIdPresent_GetsExistingEnrollment()
    {
        SetupExistingEnrollmentPath("42", "200", MakeExistingEnrollment("42"));

        GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(enrollmentId: "42"), _ => TestCert);

        _mockClient.Verify(c => c.GetEnrollment("42"), Times.Once);
    }

    [Fact]
    public void ProcessJob_WhenGetExistingEnrollmentThrows_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.GetEnrollment(It.IsAny<string>()))
            .Throws(new Exception("Enrollment not found"));

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(enrollmentId: "42"), _ => TestCert);

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
        SetupClientForHappyPath("42", "9002");

        GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(enrollmentId: "42"), _ => TestCert);

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

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(enrollmentId: "42"), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenUpdateEnrollmentThrowsGeneralException_ReturnsFailure()
    {
        _mockClient.Setup(c => c.GetEnrollment("42")).Returns(MakeExistingEnrollment("42"));
        _mockClient
            .Setup(c => c.UpdateEnrollment("42", It.IsAny<Enrollment>()))
            .Throws(new Exception("Unexpected update error"));

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(enrollmentId: "42"), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }
    
    #endregion

    #region New Enrollments

    [Fact]
    public void ProcessJob_WhenNoEnrollmentId_CreatesNewEnrollment()
    {
        SetupNewEnrollmentPath("99", "100");

        GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        _mockClient.Verify(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ProcessJob_WhenCreateEnrollmentThrows409Conflict_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Throws(MakeHttpException(HttpStatusCode.Conflict));

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Contains("Enrollment already exists for CN 'test.example.com'. Cannot create a duplicate.", result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_WhenCreateEnrollmentThrowsGeneralException_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Throws(new Exception("API error"));

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }
    
    #endregion

    #region CSR Retrieval

    [Fact]
    public void ProcessJob_WhenGetCSRThrowsNonNotFoundException_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "99", changeId: "100"));
        _mockClient
            .Setup(c => c.GetCSR("99", "100", It.IsAny<string>()))
            .Throws(new Exception("Unexpected CSR error"));

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenGetCSRReturns404OnceThenSucceeds_DelaysOnceAndContinues()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "99", changeId: "100"));
        _mockClient
            .SetupSequence(c => c.GetCSR("99", "100", It.IsAny<string>()))
            .Throws(MakeHttpException(HttpStatusCode.NotFound))
            .Returns(FakeCsr);

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
        _mockTimerService.Verify(t => t.DelayBySeconds(30), Times.Once);
    }

    [Fact]
    public void ProcessJob_WhenGetCSRAlwaysReturns404_ExhaustsRetriesAndReturnsFailure()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "99", changeId: "100"));
        _mockClient
            .SetupSequence(c => c.GetCSR("99", "100", It.IsAny<string>()))
            .Throws(MakeHttpException(HttpStatusCode.NotFound))
            .Throws(MakeHttpException(HttpStatusCode.NotFound))
            .Throws(MakeHttpException(HttpStatusCode.NotFound))
            .Throws(MakeHttpException(HttpStatusCode.NotFound))
            .Throws(MakeHttpException(HttpStatusCode.NotFound));

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        _mockTimerService.Verify(t => t.DelayBySeconds(30), Times.Exactly(5));
    }
    
    #endregion

    #region Post-CSR Steps

    [Fact]
    public void ProcessJob_WhenSubmitReenrollmentCSRReturnsNull_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "99", changeId: "100"));
        _mockClient
            .Setup(c => c.GetCSR("99", "100", It.IsAny<string>()))
            .Returns(FakeCsr);

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => null);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenPostCertificateThrows_ReturnsFailure()
    {
        SetupNewEnrollmentPath("99", "100");
        _mockClient
            .Setup(c => c.PostCertificate(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>()))
            .Throws(new Exception("Upload failed"));

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }
    
    #endregion

    #region Warning Acknowledgemnet

    [Fact]
    public void ProcessJob_WhenAcknowledgeWarningsThrowsNonNotFoundException_ReturnsFailure()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "99", changeId: "100"));
        _mockClient
            .Setup(c => c.GetCSR("99", "100", It.IsAny<string>()))
            .Returns(FakeCsr);
        _mockClient
            .Setup(c => c.AcknowledgeWarnings("99", "100"))
            .Throws(new Exception("Unexpected acknowledgement error"));

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenAcknowledgeWarningsReturns404OnceThenSucceeds_DelaysOnceAndReturnsSuccess()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "99", changeId: "100"));
        _mockClient
            .Setup(c => c.GetCSR("99", "100", It.IsAny<string>()))
            .Returns(FakeCsr);
        _mockClient
            .SetupSequence(c => c.AcknowledgeWarnings("99", "100"))
            .Throws(MakeHttpException(HttpStatusCode.NotFound))
            .Pass();

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
        _mockTimerService.Verify(t => t.DelayBySeconds(20), Times.Once);
    }

    [Fact]
    public void ProcessJob_WhenAcknowledgeWarningsAlwaysReturns404_ExhaustsRetriesAndReturnsWarning()
    {
        _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()))
            .Returns(MakeCreatedEnrollment(enrollmentId: "99", changeId: "100"));
        _mockClient
            .Setup(c => c.GetCSR("99", "100", It.IsAny<string>()))
            .Returns(FakeCsr);
        _mockClient
            .SetupSequence(c => c.AcknowledgeWarnings("99", "100"))
            .Throws(MakeHttpException(HttpStatusCode.NotFound))
            .Throws(MakeHttpException(HttpStatusCode.NotFound))
            .Throws(MakeHttpException(HttpStatusCode.NotFound))
            .Throws(MakeHttpException(HttpStatusCode.NotFound));

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Warning, result.Result);
        Assert.Contains("Maximum retries reached and the deployment warnings could not be acknowledged. Certificate may not be deployed.", result.FailureMessage);
        _mockTimerService.Verify(t => t.DelayBySeconds(20), Times.Exactly(4));
    }
    
    #endregion

    #region Happy Paths

    [Fact]
    public void ProcessJob_WhenCreatingNewEnrollment_ReturnsSuccess()
    {
        SetupNewEnrollmentPath("99", "100");

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
    }

    [Fact]
    public void ProcessJob_WhenUpdatingExistingEnrollment_ReturnsSuccess()
    {
        SetupExistingEnrollmentPath("42", "200", MakeExistingEnrollment("42"));

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(enrollmentId: "42"), _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
    }
    
    #endregion

    #region Certificate Chain

    [Fact]
    public void ProcessJob_WhenCertificateChainHasNullChain_SendsNullTrustChainAndReturnsWarning()
    {
        SetupNewEnrollmentPath("99", "100");
        _mockCertificateChainService
            .Setup(s => s.BuildCertificateCollection(It.IsAny<X509Certificate>()))
            .Returns(new CertificateCollection { EndEntityCert = TestBcCert, ChainCerts = null });

        var result = GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        _mockClient.Verify(c => c.PostCertificate(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.Is<string?>(s => s == null)), Times.Once);
        Assert.Equal(OrchestratorJobStatusJobResult.Warning, result.Result);
        Assert.Contains(
            "Enrollment completed but the certificate's trust chain could not be built. Please verify the intermediate and root certificates are part of your system's trust store or have publicly available AIA information.",
            result.FailureMessage);
    }

    [Fact]
    public void ProcessJob_WhenCertificateChainHasDefinedTrustChain_SendsTrustChain()
    {
        SetupNewEnrollmentPath("99", "100");
        var expectedBase64 = Convert.ToBase64String(TestBcCert.GetEncoded());
        _mockCertificateChainService
            .Setup(s => s.BuildCertificateCollection(It.IsAny<X509Certificate>()))
            .Returns(new CertificateCollection
            {
                EndEntityCert = TestBcCert,
                ChainCerts = new List<X509Certificate> { TestBcCert },
            });

        GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        _mockClient.Verify(c => c.PostCertificate(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.Is<string?>(s => s != null && s.Contains(expectedBase64))), Times.Once);
    }
    
    [Fact]
    public void ProcessJob_WhenCertificateChainHasNullChain_DeploymentNetworkIsDifferent_ReturnsWarning()
    {
        var existing = MakeExistingEnrollment("42");
        existing.networkConfiguration.secureNetwork = "standard-tls";
        Enrollment? captured = null;
        SetupExistingEnrollmentPath("42", "200", existing, capture: e => captured = e);
        var config = MakeReenrollmentConfig(enrollmentId: "42");
        config.JobProperties["deployment-network"] = "Enhanced TLS";
        
        _mockCertificateChainService
            .Setup(s => s.BuildCertificateCollection(It.IsAny<X509Certificate>()))
            .Returns(new CertificateCollection { EndEntityCert = TestBcCert, ChainCerts = null });

        var result = GetReenrollmentClass().ProcessJob(config, _ => TestCert);


        Assert.Equal(OrchestratorJobStatusJobResult.Warning, result.Result);

        // This tests that warning messages can be combined when both conditions occur.
        Assert.Contains(
            "Enrollment completed but the certificate's trust chain could not be built. Please verify the intermediate and root certificates are part of your system's trust store or have publicly available AIA information.",
            result.FailureMessage);
        Assert.Contains(
            "Certificate was deployed, but the deployment network type could not be updated if it was different from the original enrollment. Enrollment preserved original network type.",
            result.FailureMessage);
    }
    
    #endregion

    #region Deployment Network

    [Fact]
    public void ProcessJob_WhenDeploymentNetworkMissing_DefaultsToStandardTls()
    {
        Enrollment? captured = null;
        SetupNewEnrollmentPath("99", "100", capture: e => captured = e);

        // MakeReenrollmentConfig has no "deployment-network" key — backward compat case.
        GetReenrollmentClass().ProcessJob(MakeReenrollmentConfig(), _ => TestCert);

        Assert.Equal("standard-tls", captured!.networkConfiguration.secureNetwork);
    }

    [Fact]
    public void ProcessJob_WhenDeploymentNetworkIsNull_DefaultsToStandardTls()
    {
        Enrollment? captured = null;
        SetupNewEnrollmentPath("99", "100", capture: e => captured = e);
        var config = MakeReenrollmentConfig();
        config.JobProperties["deployment-network"] = null;

        GetReenrollmentClass().ProcessJob(config, _ => TestCert);

        Assert.Equal("standard-tls", captured!.networkConfiguration.secureNetwork);
    }

    [Theory]
    [InlineData("Standard TLS", "standard-tls")]
    [InlineData("Enhanced TLS", "enhanced-tls")]
    public void ProcessJob_WhenDeploymentNetworkIsSet_MapsToCorrectSecureNetworkValue(
        string displayValue, string expectedSecureNetwork)
    {
        Enrollment? captured = null;
        SetupNewEnrollmentPath("99", "100", capture: e => captured = e);
        var config = MakeReenrollmentConfig();
        config.JobProperties["deployment-network"] = displayValue;

        GetReenrollmentClass().ProcessJob(config, _ => TestCert);

        Assert.Equal(expectedSecureNetwork, captured!.networkConfiguration.secureNetwork);
    }

    [Fact]
    public void ProcessJob_WhenDeploymentNetworkIsInvalidValue_ReturnsFailure()
    {
        var config = MakeReenrollmentConfig();
        config.JobProperties["deployment-network"] = "Ultra TLS";

        var result = GetReenrollmentClass().ProcessJob(config, _ => TestCert);

        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
    }

    [Theory]
    [InlineData("enhanced-tls", "Standard TLS")]
    [InlineData("standard-tls", "Enhanced TLS")]
    public void ProcessJob_WhenUpdatingEnrollment_ReenrollmentNetworkNotEqual_PreservesExistingNetwork_ReturnsWarning(
        string existingNetwork, string reenrollmentNetwork)
    {
        var existing = MakeExistingEnrollment("42");
        existing.networkConfiguration.secureNetwork = existingNetwork;
        Enrollment? captured = null;
        SetupExistingEnrollmentPath("42", "200", existing, capture: e => captured = e);
        var config = MakeReenrollmentConfig(enrollmentId: "42");
        config.JobProperties["deployment-network"] = reenrollmentNetwork;

        var result = GetReenrollmentClass().ProcessJob(config, _ => TestCert);

        Assert.Equal(existingNetwork, captured!.networkConfiguration.secureNetwork);
        Assert.Equal(OrchestratorJobStatusJobResult.Warning, result.Result);
        Assert.Contains(
            "Certificate was deployed, but the deployment network type could not be updated if it was different from the original enrollment. Enrollment preserved original network type.",
            result.FailureMessage);
    }

    [Theory]
    [InlineData("enhanced-tls", "Enhanced TLS")]
    [InlineData("standard-tls", "Standard TLS")]
    public void ProcessJob_WhenUpdatingEnrollment_ReenrollmentNetworkEqual_ReturnsSuccess(
        string existingNetwork, string reenrollmentNetwork)
    {
        var existing = MakeExistingEnrollment("42");
        existing.networkConfiguration.secureNetwork = existingNetwork;
        Enrollment? captured = null;
        SetupExistingEnrollmentPath("42", "200", existing, capture: e => captured = e);
        var config = MakeReenrollmentConfig(enrollmentId: "42");
        config.JobProperties["deployment-network"] = reenrollmentNetwork;

        var result = GetReenrollmentClass().ProcessJob(config, _ => TestCert);

        Assert.Equal(existingNetwork, captured!.networkConfiguration.secureNetwork);
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
    }
    
    #endregion

    #region Helpers

    private Reenrollment GetReenrollmentClass()
        => new(Logger, _mockFactory.Object, _mockTimerService.Object, _mockCertificateChainService.Object);

    /// Sets up the new-enrollment happy path: CreateEnrollment returns a valid enrollment
    /// and GetCSR returns a fake CSR. PostCertificate and AcknowledgeWarnings are void and
    /// default to no-ops. Optionally captures the Enrollment argument passed to CreateEnrollment.
    private void SetupNewEnrollmentPath(string enrollmentId, string changeId,
        Action<Enrollment>? capture = null)
    {
        var setup = _mockClient
            .Setup(c => c.CreateEnrollment(It.IsAny<Enrollment>(), It.IsAny<string>()));
        if (capture != null)
            setup.Callback<Enrollment, string>((e, _) => capture(e));
        setup.Returns(MakeCreatedEnrollment(enrollmentId, changeId));
        SetupClientForHappyPath(enrollmentId, changeId);
    }

    /// Sets up the existing-enrollment happy path: GetEnrollment returns the provided enrollment,
    /// UpdateEnrollment returns a valid enrollment, and GetCSR returns a fake CSR.
    /// Optionally captures the Enrollment argument passed to UpdateEnrollment.
    private void SetupExistingEnrollmentPath(string enrollmentId, string changeId,
        Enrollment existingEnrollment, Action<Enrollment>? capture = null)
    {
        _mockClient.Setup(c => c.GetEnrollment(enrollmentId)).Returns(existingEnrollment);
        var updateSetup = _mockClient
            .Setup(c => c.UpdateEnrollment(enrollmentId, It.IsAny<Enrollment>()));
        if (capture != null)
            updateSetup.Callback<string, Enrollment>((_, e) => capture(e));
        updateSetup.Returns(MakeCreatedEnrollment(enrollmentId, changeId));
        SetupClientForHappyPath(enrollmentId, changeId);
    }

    /// Sets up GetCSR to return a fake CSR for the given IDs.
    /// PostCertificate and AcknowledgeWarnings are void and default to no-ops.
    private void SetupClientForHappyPath(string enrollmentId, string changeId)
    {
        _mockClient
            .Setup(c => c.GetCSR(enrollmentId, changeId, It.IsAny<string>()))
            .Returns(FakeCsr);
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
        var response = new HttpResponseMessage(statusCode)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.akamai.com"),
        };
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

    private static X509Certificate CreateSelfSignedCertBouncyCastle(X509Certificate2 cert)
    {
        return new X509CertificateParser().ReadCertificate(cert.RawData);
    }
    
    #endregion
}
