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

public class AkamaiEnrollmentBuilder
{
    private string _id = "1";
    private NetworkConfiguration _networkConfiguration = new ();

    public AkamaiEnrollmentBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public AkamaiEnrollmentBuilder WithSecureNetwork(string secureNetwork)
    {
        _networkConfiguration.secureNetwork = secureNetwork;
        return this;
    }

    public Enrollment Build()
    {
        return new Enrollment
        {
            id = _id,
            networkConfiguration = _networkConfiguration,
        };
    }
}
