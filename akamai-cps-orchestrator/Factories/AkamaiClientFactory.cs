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

using System.Collections.Generic;
using Keyfactor.Extensions.Utilities.HttpInterface;
using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Factories;

public interface IAkamaiClientFactory
{
    IAkamaiClient Create(ILogger logger, Dictionary<string, string> storeProps, string clientMachine, string enrollmentType);
}

public class AkamaiClientFactory : IAkamaiClientFactory
{
    public IAkamaiClient Create(ILogger logger, Dictionary<string, string> storeProps, string clientMachine, string enrollmentType)
    {
        var auth = new AkamaiAuth(storeProps);
        var deploymentType = DeploymentTypeHelpers.ParseStorePath(enrollmentType);
        return new AkamaiClient(logger, clientMachine, deploymentType, auth);
    }
}
