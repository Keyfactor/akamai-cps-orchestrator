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
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Keyfactor.Extensions.Utilities.HttpInterface.Contexts;

namespace Keyfactor.Extensions.Utilities.HttpInterface
{
    /// <summary>
    /// This class implements the Akamai EdgeGrid protocol for generating the appropriate authentication header for Akamai API requests.
    /// It uses HMAC-SHA256 for signing the request details and client credentials, as specified by Akamai's authentication requirements.
    ///
    /// As of Feb 2026, Akamai does not have a (production-ready) package for generating EdgeGrid authentication headers, so this class was implemented based on the specifications outlined in their documentation: https://github.com/akamai/AkamaiOPEN-edgegrid-C-Sharp/tree/master
    /// </summary>
    public class AkamaiAuth
    {
        private readonly string _clientSecret;
        private readonly string _clientToken;
        private readonly string _accessToken;

        private readonly IAkamaiAuthContext _authContext;
        public readonly string AuthType = "EG1-HMAC-SHA256";

        public AkamaiAuth(Dictionary<string, string> jobProperties, IAkamaiAuthContext authContext = null)
        {
            _clientSecret = jobProperties["client_secret"];
            _clientToken = jobProperties["client_token"];
            _accessToken = jobProperties["access_token"];
            _authContext = authContext ?? new AkamaiAuthContext();
        }

        public AuthenticationHeaderValue GenerateAuthHeader(string requestMethod, string host, string path, string requestBody = null)
        {
            string timestamp = _authContext.GetTime().ToString("yyyyMMddTHH:mm:ss+0000");
            string nonce = _authContext.GetNonce();

            string authFormat = "client_token={0};access_token={1};timestamp={2};nonce={3};";
            string authHeaderValue = string.Format(authFormat, _clientToken, _accessToken, timestamp, nonce);

            // Auth Header signing key is the signature from signing the timestamp with client secret
            string signingKey = Convert.ToBase64String(SignData_HMAC_SHA256(timestamp, Encoding.UTF8.GetBytes(_clientSecret)));

            byte[] requestBodyHash = null;
            if (!string.IsNullOrWhiteSpace(requestBody))
            {
                requestBodyHash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(requestBody));
            }

            // FIELDS: request method, request scheme, request host, request path + query or params, headers, hashed request body, auth header without signature
            string requestData = string.Join('\t',
                requestMethod.ToUpper(),                                                // request method
                "https",                                                                // request scheme
                host,                                                                   // request host
                path,                                                                   // request path
                "",                                                                // accept and content-type headers
                requestBodyHash != null ? Convert.ToBase64String(requestBodyHash) : "", // base 64 of sha256 hash of request body
                $"{AuthType} {authHeaderValue}"                                         // auth header before adding signature
            );

            byte[] signature = SignData_HMAC_SHA256(requestData, Encoding.UTF8.GetBytes(signingKey));
            authHeaderValue += $"signature={Convert.ToBase64String(signature)}";
            return new AuthenticationHeaderValue(AuthType, authHeaderValue);
        }

        private byte[] SignData_HMAC_SHA256(string data, byte[] key)
        {
            HMACSHA256 signingAlgorithm = new HMACSHA256(key);
            byte[] signature = signingAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(data));
            return signature;
        }
    }
}
