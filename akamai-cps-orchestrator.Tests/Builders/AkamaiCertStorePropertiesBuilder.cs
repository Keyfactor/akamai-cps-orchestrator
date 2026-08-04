using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;

namespace akamai_cps_orchestrator.Tests.Builders;

public class AkamaiCertStorePropertiesBuilder
{
    private string? _accessToken = "foo";
    private string? _clientToken = "bar";
    private string? _clientSecret = "baz";
    private string? _contractId = null;

    public AkamaiCertStorePropertiesBuilder WithAccessToken(string? accessToken)
    {
        _accessToken = accessToken;
        return this;
    }

    public AkamaiCertStorePropertiesBuilder WithClientToken(string? clientToken)
    {
        _clientToken = clientToken;
        return this;
    }

    public AkamaiCertStorePropertiesBuilder WithClientSecret(string? clientSecret)
    {
        _clientSecret = clientSecret;
        return this;
    }

    public AkamaiCertStorePropertiesBuilder WithContractId(string? contractId)
    {
        _contractId = contractId;
        return this;
    }

    public AkamaiCertStoreProperties Build()
    {
        return new AkamaiCertStoreProperties()
        {
            AccessToken = _accessToken,
            ClientToken = _clientToken,
            ClientSecret = _clientSecret,
            ContractId = _contractId,
        };
    }
}
