﻿// Copyright 2023 Keyfactor
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

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    public class AkamaiAuth
    {
        private readonly string _clientSecret;
        private readonly string _clientToken;
        private readonly string _accessToken;

        private string Nonce { get { return Guid.NewGuid().ToString(); } }
        public readonly string AuthType = "EG1-HMAC-SHA256";

        public AkamaiAuth(Dictionary<string, string> jobProperties)
        {
            _clientSecret = jobProperties["client_secret"];
            _clientToken = jobProperties["client_token"];
            _accessToken = jobProperties["access_token"];
        }

        public AuthenticationHeaderValue GenerateAuthHeader(string requestMethod, string host, string path, string requestBody = null)
        {
            DateTime time = DateTime.UtcNow;
            string timestamp = time.ToString("yyyyMMddTHH:mm:ss+0000");

            string authFormat = "client_token={0};access_token={1};timestamp={2};nonce={3};";
            string authHeaderValue = string.Format(authFormat, _clientToken, _accessToken, timestamp, Nonce);

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
