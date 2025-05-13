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
}
