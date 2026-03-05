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
using Xunit;

namespace akamai_cps_orchestrator.Tests.Models;

public class DeploymentTypeTests
{
    [Theory]
    [InlineData("Production", DeploymentType.Production)]
    [InlineData("Staging", DeploymentType.Staging)]
    public void ParseStorePath_ValidInput_ReturnsExpectedDeploymentType(string storePath, DeploymentType expected)
    {
        var result = DeploymentTypeHelpers.ParseStorePath(storePath);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseStorePath_InvalidInput_ThrowsException()
    {
        var invalidStorePath = "InvalidPath";
        var exception = Assert.Throws<ArgumentException>(() => DeploymentTypeHelpers.ParseStorePath(invalidStorePath));
        Assert.Equal($"Store path {invalidStorePath} did not match either '{Constants.StorePaths.Production}' or '{Constants.StorePaths.Staging}'", exception.Message);
    }
}
