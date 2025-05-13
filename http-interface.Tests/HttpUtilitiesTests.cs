// Copyright 2025 Keyfactor
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
using Xunit;

namespace http_interface.Tests;

public class HttpUtilitiesTests
{
    [Fact]
    public void GetRetryDate_WhenHeaderIncludesRetryDate_DateInFuture_ReturnsRetryDelayFromHeader()
    {
        var response = new HttpResponseMessage();
        response.Headers.Add("X-RateLimit-Next", "2025-05-13T14:28:19.4732Z");

        var mockNow = DateTimeOffset.Parse("2025-05-13T14:28:17.1234Z");
        
        // A 1-second buffer is added onto the retry timestamp
        var expected = TimeSpan.FromSeconds(3.3498);

        var result = HttpUtilities.GetRetryAfterDelay(response, mockNow);
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void GetRetryDate_WhenHeaderIncludesRetryDate_DateInPast_ReturnsRetryDelayOfOneSecond()
    {
        var response = new HttpResponseMessage();
        response.Headers.Add("X-RateLimit-Next", "2025-05-13T14:28:14.4732Z");

        var mockNow = DateTimeOffset.Parse("2025-05-13T14:28:17.1234Z");
        var expected = TimeSpan.FromSeconds(1);

        var result = HttpUtilities.GetRetryAfterDelay(response, mockNow);
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void GetRetryDate_WhenHeaderIncludesRetryDate_DateInvalid_ReturnsDefaultRetryDelay()
    {
        var response = new HttpResponseMessage();
        response.Headers.Add("X-RateLimit-Next", "invalid");

        var mockNow = DateTimeOffset.Parse("2025-05-13T14:28:17.1234Z");
        var expected = TimeSpan.FromSeconds(60);

        var result = HttpUtilities.GetRetryAfterDelay(response, mockNow);
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void GetRetryDate_WhenHeaderDoesNotIncludeRetryDate_ReturnsDefaultOffsetFromCurrentDate()
    {
        var response = new HttpResponseMessage();

        var mockNow = DateTimeOffset.Parse("2025-05-13T14:28:17.1234Z");
        var expected = TimeSpan.FromSeconds(60);

        var result = HttpUtilities.GetRetryAfterDelay(response, mockNow);
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void GetRetryDate_WhenHeaderDoesNotIncludeRetryDate_DefaultIsOverridden_ReturnsProvidedOffsetFromCurrentDate()
    {
        var response = new HttpResponseMessage();

        var mockNow = DateTimeOffset.Parse("2025-05-13T14:28:17.1234Z");
        var expected = TimeSpan.FromSeconds(15);

        var result = HttpUtilities.GetRetryAfterDelay(response, mockNow, 15);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("hostname.com", "hostname.com")]
    [InlineData("https://hostname.com", "hostname.com")]
    [InlineData("http://hostname.com", "hostname.com")]
    [InlineData("hostname.com/some/path", "hostname.com")]
    [InlineData("api.hostname.com", "api.hostname.com")]
    [InlineData("api.hostname.com/", "api.hostname.com")]
    public void TryGetHostname_WhenInputIsValid_ReturnsExpectedHostname(string input, string expected)
    {
        var ok = HttpUtilities.TryGetHostname(input, out string result);
        Assert.True(ok);
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    [InlineData("some host")]
    public void TryGetHostname_WhenInputIsInvalid_ReturnsFalseResult(string input)
    {
        var ok = HttpUtilities.TryGetHostname(input, out string result);
        Assert.False(ok);
        Assert.Equal(input, result);
    }
}
