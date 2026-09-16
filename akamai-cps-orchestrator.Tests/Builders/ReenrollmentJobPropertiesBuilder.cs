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

public class ReenrollmentJobPropertiesBuilder
{
    private readonly Dictionary<string, object> _props = new()
    {
        ["subjectText"] = "CN=test.example.com,O=TestOrg,OU=TestOU,L=TestCity,ST=TestState,C=US",
        ["keyType"] = "RSA",
        ["ContractId"] = "contract-123",
        [Constants.EntryParameters.Sans] = "test.example.com&www.test.example.com",
        // admin contact
        [Constants.EntryParameters.Admin.AddressLineOne] = "123 Main St",
        [Constants.EntryParameters.Admin.AddressLineTwo] = null,
        [Constants.EntryParameters.Admin.City] = "TestCity",
        [Constants.EntryParameters.Admin.Country] = "US",
        [Constants.EntryParameters.Admin.Email] = "admin@test.com",
        [Constants.EntryParameters.Admin.FirstName] = "Admin",
        [Constants.EntryParameters.Admin.LastName] = "User",
        [Constants.EntryParameters.Admin.OrganizationName] = "TestOrg",
        [Constants.EntryParameters.Admin.Phone] = "555-0100",
        [Constants.EntryParameters.Admin.PostalCode] = "12345",
        [Constants.EntryParameters.Admin.Region] = "TestState",
        [Constants.EntryParameters.Admin.Title] = "Admin",
        // org contact
        [Constants.EntryParameters.Org.AddressLineOne] = "123 Main St",
        [Constants.EntryParameters.Org.AddressLineTwo] = null,
        [Constants.EntryParameters.Org.City] = "TestCity",
        [Constants.EntryParameters.Org.Country] = "US",
        [Constants.EntryParameters.Org.OrganizationName] = "TestOrg",
        [Constants.EntryParameters.Org.Phone] = "555-0100",
        [Constants.EntryParameters.Org.PostalCode] = "12345",
        [Constants.EntryParameters.Org.Region] = "TestState",
        // tech contact
        [Constants.EntryParameters.Tech.AddressLineOne] = "123 Main St",
        [Constants.EntryParameters.Tech.AddressLineTwo] = null,
        [Constants.EntryParameters.Tech.City] = "TestCity",
        [Constants.EntryParameters.Tech.Country] = "US",
        [Constants.EntryParameters.Tech.Email] = "tech@test.com",
        [Constants.EntryParameters.Tech.FirstName] = "Tech",
        [Constants.EntryParameters.Tech.LastName] = "User",
        [Constants.EntryParameters.Tech.OrganizationName] = "TestOrg",
        [Constants.EntryParameters.Tech.Phone] = "555-0100",
        [Constants.EntryParameters.Tech.PostalCode] = "12345",
        [Constants.EntryParameters.Tech.Region] = "TestState",
        [Constants.EntryParameters.Tech.Title] = "Tech",
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
        _props[Constants.EntryParameters.DeploymentNetwork] = deploymentNetwork;
        return this;
    }

    public ReenrollmentJobPropertiesBuilder WithEnrollmentId(string? enrollmentId)
    {
        _props[Constants.EntryParameters.EnrollmentId] = enrollmentId;
        return this;
    }

    public ReenrollmentJobPropertiesBuilder WithContractId(string? contractId)
    {
        _props["ContractId"] = contractId;
        return this;
    }

    public Dictionary<string, object> Build() => new(_props);
}
