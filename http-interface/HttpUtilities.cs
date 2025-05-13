using System;
using System.Linq;
using System.Net.Http;

namespace Keyfactor.Extensions.Utilities.HttpInterface
{
    public static class HttpUtilities
    {
        public static TimeSpan GetRetryAfterDelay(HttpResponseMessage response, DateTimeOffset now, int defaultTimeoutSeconds = 60)
        {
            var defaultDelay = TimeSpan.FromSeconds(defaultTimeoutSeconds);
            if (!response.Headers.Contains("X-RateLimit-Next"))
            {
                return defaultDelay;
            }
            
            var nextHeader = response.Headers.First(p => p.Key == "X-RateLimit-Next");
            if (DateTimeOffset.TryParse(nextHeader.Value.FirstOrDefault(), out var retryUtc))
            {
                // Add a 1-second buffer to the retry to avoid sending requests before server is ready.
                var delay = (retryUtc - now) + TimeSpan.FromSeconds(1);
                return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1);
            }

            return defaultDelay;
        }
    }
}
