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

using Keyfactor.Extensions.Utilities.HttpInterface;
using Keyfactor.Extensions.Utilities.HttpInterface.Contexts;
using Xunit;

namespace http_interface.Tests;

public class AkamaiAuthTests
{
    private readonly IAkamaiAuthContext _authContext = new FakeAkamaiAuthContext();

    [Fact]
    public void GetHeader_WithoutBody_ReturnsExpectedHeaderValue()
    {
        var properties = new Dictionary<string, string>()
        {
            { "client_secret", "foo" },
            { "client_token", "bar" },
            { "access_token", "baz" }
        };

        var requestMethod = "GET";
        var host = "host";
        var path = "path";

        var expectedAuthHeader =
            "EG1-HMAC-SHA256 client_token=bar;access_token=baz;timestamp=20250513T14:28:17+0000;nonce=test-nonce;signature=B2M8OBYTcTDfvZQ2hMKMIZXdO9unVaRHQqAyo/a3YGA=";
        
        var auth = new AkamaiAuth(properties, _authContext);
        var result = auth.GenerateAuthHeader(requestMethod, host, path);
        Assert.Equal(expectedAuthHeader, result.ToString());
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GetHeader_WithNullOrWhitespaceBody_ReturnsExpectedHeaderValue(string requestBody)
    {
        var properties = new Dictionary<string, string>()
        {
            { "client_secret", "foo" },
            { "client_token", "bar" },
            { "access_token", "baz" }
        };

        var requestMethod = "GET";
        var host = "host";
        var path = "path";

        var expectedAuthHeader =
            "EG1-HMAC-SHA256 client_token=bar;access_token=baz;timestamp=20250513T14:28:17+0000;nonce=test-nonce;signature=B2M8OBYTcTDfvZQ2hMKMIZXdO9unVaRHQqAyo/a3YGA=";
        
        var auth = new AkamaiAuth(properties, _authContext);
        var result = auth.GenerateAuthHeader(requestMethod, host, path, requestBody);
        Assert.Equal(expectedAuthHeader, result.ToString());
    }
    
    [Fact]
    public void GetHeader_WithBody_ReturnsExpectedHeaderValue()
    {
        var properties = new Dictionary<string, string>()
        {
            { "client_secret", "foo" },
            { "client_token", "bar" },
            { "access_token", "baz" }
        };

        const string requestMethod = "GET";
        const string host = "host";
        const string path = "path";

        const string requestBody = "fizzbuzz";

        const string expectedAuthHeader = "EG1-HMAC-SHA256 client_token=bar;access_token=baz;timestamp=20250513T14:28:17+0000;nonce=test-nonce;signature=XnbdZjMdsQVm5TkZEJ4enhdmFvFb0b98iFUMVwyi7LM=";
        
        var auth = new AkamaiAuth(properties, _authContext);
        var result = auth.GenerateAuthHeader(requestMethod, host, path, requestBody);
        Assert.Equal(expectedAuthHeader, result.ToString());
    }
}

public class FakeAkamaiAuthContext : IAkamaiAuthContext
{
    public DateTime GetTime() => DateTime.Parse("2025-05-13T14:28:17.1234Z").ToUniversalTime();
    public string GetNonce() => "test-nonce";
}
