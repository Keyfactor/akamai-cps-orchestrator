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

        public int Timeout { get; set; }

        public HttpInterface(ILogger logger, string baseAddress, bool useSSL)
        {
            _logger = logger;
            _http = new HttpClient();
            // TODO: check if http(s) prefix is already present
            // TODO: parse incoming baseAddress as URI
            _logger.LogDebug($"Base Address - {baseAddress}");
            _logger.LogDebug($"Using SSL - {useSSL}");
            if (useSSL)
            {
                _http.BaseAddress = new Uri($"https://{baseAddress}/");
            }
            else
            {
                _http.BaseAddress = new Uri($"http://{baseAddress}/");
            }

        }

        public void SetRequestHeaders(Dictionary<string, string> headers)
        {
            // TODO: set on httpHandler per request instead of defaults for whole client
            // TODO: log headers set without exposing Auth header / sensitive information
            _http.DefaultRequestHeaders.Clear();
            foreach (var key in headers.Keys)
            {
                _logger.LogDebug($"Adding header - {key}: {headers[key]}");
                _http.DefaultRequestHeaders.Add(key, headers[key]);
            }
    
        }

        public void AddAuthHeader(AuthenticationHeaderValue authHeader)
        {
            _http.DefaultRequestHeaders.Authorization = authHeader;
        }

        public T Get<T>(string path)
        {
            string json = GetRaw(path);
            _logger.LogTrace($"Received GET response. Deserializing into type {typeof(T)}");
            return JsonConvert.DeserializeObject<T>(json);
        }

        public string GetRaw(string path)
        {
            try
            {
                _logger.LogDebug($"Performing GET request to {_http.BaseAddress}/{path}");
                var response = _http.GetAsync(path).Result;
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
                // TODO: check other specific errors, timeout / cancellation
                _logger.LogError($"Unexpected error that was not a GET response to {_http.BaseAddress}/{path}");
                _logger.LogError($"Error info: {e.ToString()}");
                _logger.LogTrace("Returning exception for caller to handle.");
                throw;
            }
        }

        public T Post<T>(string path, StringContent body)
        {
            string json = PostRaw(path, body);
            _logger.LogTrace($"Received POST response. Deserializing into type {typeof(T)}");
            return JsonConvert.DeserializeObject<T>(json);
        }

        public string PostRaw(string path, StringContent body)
        {
            try
            {
                _logger.LogDebug($"Performing POST request to {_http.BaseAddress}/{path}");
                var response = _http.PostAsync(path, body).Result;
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
                // TODO: check other specific errors, timeout / cancellation
                _logger.LogError($"Unexpected error that was not a POST response to {_http.BaseAddress}/{path}");
                _logger.LogError($"Error info: {e.ToString()}");
                _logger.LogTrace("Returning exception for caller to handle.");
                throw;
            }
        }

        public T Put<T>(string path, StringContent body)
        {
            string json = PutRaw(path, body);
            _logger.LogTrace($"Received PUT response. Deserializing into type {typeof(T)}");
            return JsonConvert.DeserializeObject<T>(json);
        }

        public string PutRaw(string path, StringContent body)
        {
            try
            {
                _logger.LogDebug($"Performing PUT request to {_http.BaseAddress}/{path}");
                var response = _http.PutAsync(path, body).Result;
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
                // TODO: check other specific errors, timeout / cancellation
                _logger.LogError($"Unexpected error that was not a PUT response to {_http.BaseAddress}/{path}");
                _logger.LogError($"Error info: {e.ToString()}");
                _logger.LogTrace("Returning exception for caller to handle.");
                throw;
            }
        }

        public T Delete<T>(string path)
        {
            string json = DeleteRaw(path);
            _logger.LogTrace($"Received DELETE response. Deserializing into type {typeof(T)}");
            return JsonConvert.DeserializeObject<T>(json);
        }

        public string DeleteRaw(string path)
        {
            try
            {
                _logger.LogDebug($"Performing DELETE request to {_http.BaseAddress}/{path}");
                var response = _http.DeleteAsync(path).Result;
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
                // TODO: check other specific errors, timeout / cancellation
                _logger.LogError($"Unexpected error that was not a DELETE response to {_http.BaseAddress}/{path}");
                _logger.LogError($"Error info: {e.ToString()}");
                _logger.LogTrace("Returning exception for caller to handle.");
                throw;
            }
        }

        private string ReadHttpResponse(HttpResponseMessage response)
        {
            string responseMessage = response.Content.ReadAsStringAsync().Result;
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
