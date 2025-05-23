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

using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;

namespace Keyfactor.Extensions.Utilities.HttpInterface
{
    public static class HttpUtilities
    {
        /// <summary>
        /// Parses a retry date from the "X-RateLimit-Next" header on an HTTP response, if present, and generate a TimeSpan based off the current time.
        /// If the header value is missing or if parsing fails, it will return a default TimeSpan.
        /// </summary>
        /// <param name="response">An HTTP response message for a failed request</param>
        /// <param name="now">The current timestamp in UTC</param>
        /// <param name="defaultTimeoutSeconds">The default retry time interval if parsing fails</param>
        /// <returns>A TimeSpan indicating how long to wait to retry the request.</returns>
        public static TimeSpan GetRetryAfterDelay(HttpResponseMessage response, DateTimeOffset now, int defaultTimeoutSeconds = 60)
        {
            var defaultDelay = TimeSpan.FromSeconds(defaultTimeoutSeconds);
            if (!response.Headers.Contains("X-RateLimit-Next"))
            {
                return defaultDelay;
            }
            
            var nextHeader = response.Headers.First(p => p.Key == "X-RateLimit-Next");
            if (DateTimeOffset.TryParse(nextHeader.Value.FirstOrDefault(), null, DateTimeStyles.AdjustToUniversal, out var retryUtc))
            {
                // Add a 1-second buffer to the retry to avoid sending requests before server is ready.
                var delay = (retryUtc - now) + TimeSpan.FromSeconds(1);
                return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1);
            }

            return defaultDelay;
        }

        /// <summary>
        /// Returns a hostname from a URL. If paring succeeds, the output string will have the parsed hostname and the method will return true.
        /// If parsing fails, the output string will return the original value and the method will return false.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="parsed"></param>
        /// <returns>A boolean indicating if parsing was successful</returns>
        public static bool TryGetHostname(string input, out string parsed)
        {
            parsed = input;
            
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }
            
            // Ensure the input has a scheme so Uri.TryCreate parses correctly
            if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                input = "http://" + input;
            }

            if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
            {
                parsed = uri.Host;
                return true;
            }
            
            return false;
        }
    }
}
