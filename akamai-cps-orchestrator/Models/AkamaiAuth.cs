﻿using System;
using System.Collections.Generic;
using System.IO;
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

        public AkamaiAuth()
        {
            string extensionDirectory = Path.GetDirectoryName(this.GetType().Assembly.Location);
            string[] edgeGridInfo = File.ReadAllLines($"{extensionDirectory}{Path.DirectorySeparatorChar}.edgerc");

            // handling only 1 entry in the .edgerc file
            // expected structure:
            // client_secret = xxxx
            // host = xxxx
            // access_token = xxxx
            // client_token = xxxx

            _clientSecret = edgeGridInfo[0].Split('=', 2)[1].Trim();
            _clientToken = edgeGridInfo[3].Split('=', 2)[1].Trim();
            _accessToken = edgeGridInfo[2].Split('=', 2)[1].Trim();
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
                "",                                                                     // other headers
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
