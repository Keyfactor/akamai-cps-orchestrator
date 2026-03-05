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

using System;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;

public enum DeploymentType
{
    Production,
    Staging,
}

public static class DeploymentTypeHelpers
{
    public static DeploymentType ParseStorePath(string storePath)
    {
        if (storePath == Constants.StorePaths.Production)
        {
            return DeploymentType.Production;
        }
        else if (storePath == Constants.StorePaths.Staging)
        {
            return DeploymentType.Staging;
        }
        else
        {
            throw new ArgumentException($"Store path {storePath} did not match either '{Constants.StorePaths.Production}' or '{Constants.StorePaths.Staging}'");
        }
    }
}
