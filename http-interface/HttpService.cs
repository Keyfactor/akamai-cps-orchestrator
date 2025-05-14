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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Keyfactor.Extensions.Utilities.HttpInterface
{
    public class HttpService
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        public HttpService(ILogger logger, HttpClient httpClient, int maxRetries = 3)
        {
            _logger = logger;
            _httpClient = httpClient;

            // Retry API requests that fail due to an HTTP 429 (rate limiting)
            // See https://techdocs.akamai.com/cps/reference/rate-limiting
            _retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => r.StatusCode == (HttpStatusCode)429)
                .WaitAndRetryAsync<HttpResponseMessage>(
                    retryCount: maxRetries, 
                    sleepDurationProvider: (retryAttempt, response, context) =>
                        {
                            var retryAfter = HttpUtilities.GetRetryAfterDelay(response.Result, DateTimeOffset.UtcNow);
                            _logger.LogInformation($"Retrying in {retryAfter.TotalSeconds} seconds (Attempt {retryAttempt})");
                            return retryAfter;
                        },
                    onRetryAsync: (outcome, delay, retryAttempt, context) =>
                    {
                        _logger.LogDebug($"Retry {retryAttempt} scheduled after {delay.TotalSeconds} seconds due to {outcome.Result.StatusCode}");
                        return Task.CompletedTask;
                    });
        }
        
        public async Task<HttpResponseMessage> GetAsync(HttpServiceConfig config, string url)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using HttpRequestMessage request = await BuildHttpRequestMessage(config, HttpMethod.Get, url);
                    
                    return await _httpClient.SendAsync(request).ConfigureAwait(false);
                })
                .ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> PostAsync(HttpServiceConfig config, string url, HttpContent content)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using HttpRequestMessage request = await BuildHttpRequestMessage(config, HttpMethod.Post, url);
                    AddContentToRequest(config, request, content);
                    return await _httpClient.SendAsync(request);
                })
                .ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> PutAsync(HttpServiceConfig config, string url, HttpContent content)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using HttpRequestMessage request = await BuildHttpRequestMessage(config, HttpMethod.Put, url);
                    AddContentToRequest(config, request, content);
                    return await _httpClient.SendAsync(request);
                })
                .ConfigureAwait(false);
        }
        
        public async Task<HttpResponseMessage> DeleteAsync(HttpServiceConfig config, string url)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using HttpRequestMessage request = await BuildHttpRequestMessage(config, HttpMethod.Delete, url);
                    
                    return await _httpClient.SendAsync(request).ConfigureAwait(false);
                })
                .ConfigureAwait(false);
        }
        
        private async Task<HttpRequestMessage> BuildHttpRequestMessage(HttpServiceConfig config, HttpMethod method, string url)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, url);
            
            if (config.Accept != null)
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(config.Accept));
            }
                    
            if (config.AuthorizationDelegate != null)
            {
                request.Headers.Authorization = await config.AuthorizationDelegate().ConfigureAwait(false);
            }

            return request;
        }

        private void AddContentToRequest(HttpServiceConfig config, HttpRequestMessage request, HttpContent content)
        {
            if (config.ContentType != null)
            {
                content.Headers.ContentType = new MediaTypeHeaderValue(config.ContentType);
            }

            request.Content = content;
        }
    }
}
