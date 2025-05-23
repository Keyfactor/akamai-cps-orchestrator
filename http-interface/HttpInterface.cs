// Copyright 2023 Keyfactor
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
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Utilities.HttpInterface
{
    public class HttpInterface
    {
        private ILogger _logger;
        private HttpClient _http;
        private SocketsHttpHandler _httpHandler; // TODO: use to make requests with headers
        private HttpService _httpService;
        private readonly AkamaiAuth _auth;
        private readonly string _hostname;
        private readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };

        public int Timeout { get; set; }

        public HttpInterface(ILogger logger, AkamaiAuth auth, string hostname, bool useSSL)
        {
            _logger = logger;
            _http = new HttpClient();

            _logger.LogDebug($"Base Address - {hostname}");
            _logger.LogDebug($"Using SSL - {useSSL}");
            
            _auth = auth;

            // TODO: Configure the max retries via a parameter?
            _httpService = new HttpService(_logger, _http, 3);
            
            HttpUtilities.TryGetHostname(hostname, out _hostname);
            
            if (useSSL)
            {
                _http.BaseAddress = new Uri($"https://{_hostname}/");
            }
            else
            {
                _http.BaseAddress = new Uri($"http://{_hostname}/");
            }

        }
        
        public T Get<T>(HttpRequestConfig req, string path)
        {
            string json = GetRaw(req, path);
            _logger.LogTrace($"Received GET response. Deserializing into type {typeof(T)}");
            return JsonConvert.DeserializeObject<T>(json);
        }
        
        public string GetRaw(HttpRequestConfig req, string path)
        {
            try
            {
                _logger.LogDebug($"Performing GET request to {_http.BaseAddress}/{path}");
                
                var config = new HttpServiceConfig()
                {
                    Accept = req.Accept,
                    ContentType = req.ContentType,
                    AuthorizationDelegate = async () => _auth.GenerateAuthHeader("GET", _hostname, path)
                };
                
                var response = _httpService
                    .GetAsync(config, path)
                    .GetAwaiter()
                    .GetResult();
                
                _logger.LogTrace($"Completed GET request. Reading response");
                return ReadHttpResponse(response);
            }
            catch (HttpInterfaceException e)
            {
                _logger.LogError($"Error in GET response from {e.RequestUri}");
                _logger.LogError($"Code: {e.ErrorCode} - ReasonPhrase: {e.Reason}");
                _logger.LogTrace("Returning exception for caller to handle.");
                throw;
            }
            catch (AggregateException e) when (e.GetBaseException() is TaskCanceledException)
            {
                // timeout occurred
                _logger.LogError($"Timeout occurred for GET request to {_http.BaseAddress}/{path}");
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError($"Unexpected error that was not a GET response to {_http.BaseAddress}/{path}");
                _logger.LogError($"Error info: {e.ToString()}");
                _logger.LogTrace("Returning exception for caller to handle.");
                throw;
            }
        }
        
        public T2 Post<T1, T2>(HttpRequestConfig req, string path, T1 body)
        {
            string content = JsonConvert.SerializeObject(body, _serializerSettings);
            string json = PostRaw(req, path, content);
            _logger.LogTrace($"Received POST response. Deserializing into type {typeof(T2)}");
            return JsonConvert.DeserializeObject<T2>(json);
        }
        
        public string PostRaw(HttpRequestConfig req, string path, string body)
        {
            try
            {
                _logger.LogDebug($"Performing POST request to {_http.BaseAddress}/{path}");
                
                var config = new HttpServiceConfig()
                {
                    Accept = req.Accept,
                    ContentType = req.ContentType,
                    AuthorizationDelegate = async () => _auth.GenerateAuthHeader("POST", _hostname, path, body)
                };

                var content = new StringContent(body);
                
                var response = _httpService.PostAsync(config, path, content).GetAwaiter().GetResult();
                _logger.LogTrace($"Completed POST request. Reading response");
                return ReadHttpResponse(response);
            }
            catch (HttpInterfaceException e)
            {
                _logger.LogError($"Error in POST response from {e.RequestUri}");
                _logger.LogError($"Code: {e.ErrorCode} - ReasonPhrase: {e.Reason}");
                _logger.LogTrace("Returning exception for caller to handle.");
                throw;
            }
            catch (AggregateException e) when (e.GetBaseException() is TaskCanceledException)
            {
                // timeout occurred
                _logger.LogError($"Timeout occurred for POST request to {_http.BaseAddress}/{path}");
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError($"Unexpected error that was not a POST response to {_http.BaseAddress}/{path}");
                _logger.LogError($"Error info: {e.ToString()}");
                _logger.LogTrace("Returning exception for caller to handle.");
                throw;
            }
        }
        
        public T2 Put<T1, T2>(HttpRequestConfig req, string path, T1 content)
        {
            string body = JsonConvert.SerializeObject(content, _serializerSettings);
            string json = PutRaw(req, path, body);
            _logger.LogTrace($"Received PUT response. Deserializing into type {typeof(T2)}");
            return JsonConvert.DeserializeObject<T2>(json);
        }
        
        public string PutRaw(HttpRequestConfig req, string path, string body)
        {
            try
            {
                _logger.LogDebug($"Performing PUT request to {_http.BaseAddress}/{path}");
                
                var config = new HttpServiceConfig()
                {
                    Accept = req.Accept,
                    ContentType = req.ContentType,
                    AuthorizationDelegate = async () => _auth.GenerateAuthHeader("PUT", _hostname, path)
                };
                
                var content = new StringContent(body);
                var response = _httpService.PutAsync(config, path, content).GetAwaiter().GetResult();
                _logger.LogTrace($"Completed PUT request. Reading response");
                return ReadHttpResponse(response);
            }
            catch (HttpInterfaceException e)
            {
                _logger.LogError($"Error in PUT response from {e.RequestUri}");
                _logger.LogError($"Code: {e.ErrorCode} - ReasonPhrase: {e.Reason}");
                _logger.LogTrace("Returning exception for caller to handle.");
                throw;
            }
            catch (AggregateException e) when (e.GetBaseException() is TaskCanceledException)
            {
                // timeout occurred
                _logger.LogError($"Timeout occurred for PUT request to {_http.BaseAddress}/{path}");
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError($"Unexpected error that was not a PUT response to {_http.BaseAddress}/{path}");
                _logger.LogError($"Error info: {e.ToString()}");
                _logger.LogTrace("Returning exception for caller to handle.");
                throw;
            }
        }
        
        public T Delete<T>(HttpRequestConfig req, string path)
        {
            string json = DeleteRaw(req, path);
            _logger.LogTrace($"Received DELETE response. Deserializing into type {typeof(T)}");
            return JsonConvert.DeserializeObject<T>(json);
        }
        
        public string DeleteRaw(HttpRequestConfig req, string path)
        {
            try
            {
                _logger.LogDebug($"Performing DELETE request to {_http.BaseAddress}/{path}");
                
                var config = new HttpServiceConfig()
                {
                    Accept = req.Accept,
                    ContentType = req.ContentType,
                    AuthorizationDelegate = async () => _auth.GenerateAuthHeader("DELETE", _hostname, path)
                };
                
                var response = _httpService.DeleteAsync(config, path).GetAwaiter().GetResult();
                _logger.LogTrace($"Completed DELETE request. Reading response");
                return ReadHttpResponse(response);
            }
            catch (HttpInterfaceException e)
            {
                _logger.LogError($"Error in DELETE response from {e.RequestUri}");
                _logger.LogError($"Code: {e.ErrorCode} - ReasonPhrase: {e.Reason}");
                _logger.LogTrace("Returning exception for caller to handle.");
                throw;
            }
            catch (AggregateException e) when (e.GetBaseException() is TaskCanceledException)
            {
                // timeout occurred
                _logger.LogError($"Timeout occurred for DELETE request to {_http.BaseAddress}/{path}");
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError($"Unexpected error that was not a DELETE response to {_http.BaseAddress}/{path}");
                _logger.LogError($"Error info: {e.ToString()}");
                _logger.LogTrace("Returning exception for caller to handle.");
                throw;
            }
        }

        private string ReadHttpResponse(HttpResponseMessage response)
        {
            string responseMessage = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                return responseMessage;
            }
            else
            {
                throw new HttpInterfaceException(responseMessage, response);
            }
        }
    }
}
