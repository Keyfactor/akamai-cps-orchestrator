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
