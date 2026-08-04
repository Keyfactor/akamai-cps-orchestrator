namespace akamai_cps_orchestrator.Tests.Builders;

public class ReenrollmentJobPropertiesBuilder
{
    private readonly Dictionary<string, object> _props = new()
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

    public ReenrollmentJobPropertiesBuilder WithKeyType(string? keyType)
    {
        _props["keyType"] = keyType;
        return this;
    }

    // deployment-network is absent by default (backward-compat case); calling this,
    // even with null, adds the key so callers can distinguish "absent" from "present but null".
    public ReenrollmentJobPropertiesBuilder WithDeploymentNetwork(string? deploymentNetwork)
    {
        _props["deployment-network"] = deploymentNetwork;
        return this;
    }

    public ReenrollmentJobPropertiesBuilder WithEnrollmentId(string? enrollmentId)
    {
        _props["EnrollmentId"] = enrollmentId;
        return this;
    }

    public ReenrollmentJobPropertiesBuilder WithContractId(string? contractId)
    {
        _props["ContractId"] = contractId;
        return this;
    }

    public Dictionary<string, object> Build() => new(_props);
}
