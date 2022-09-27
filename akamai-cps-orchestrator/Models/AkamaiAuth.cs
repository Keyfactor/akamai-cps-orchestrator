using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Keyfactor.Extensions.Orchestrator.AkamaiCpsOrchestrator.Models
{
    public class AkamaiAuth
    {
        private readonly string _clientSecret;
        private readonly string _clientToken;
        private readonly string _accessToken;

        private string Nonce { get { return new Guid().ToString(); } }
        public readonly string AuthType = "EG1-HMAC-SHA256";

        public AkamaiAuth(string clientSecret, string clientToken, string accessToken)
        {
            _clientSecret = clientSecret; // assumed to be base64 encoded
            _clientToken = clientToken;
            _accessToken = accessToken;
        }

        public AuthenticationHeaderValue GenerateAuthHeader(string requestMethod, string host, string path, string requestBody = null)
        {
            string authFormat = string.Join(' ', "client_token={0};", "access_token={1};", "timestamp={2};", "nonce={3};", "signature={4}");

            DateTime timestamp = DateTime.Now;

            // Auth Header signing key is the signature from signing the timestamp with client secret
            byte[] signingKey = SignData_HMAC_SHA256(timestamp.ToString(), Convert.FromBase64String(_clientSecret));

            byte[] requestBodyHash = SHA256.Create().ComputeHash(Encoding.ASCII.GetBytes(requestBody));

            // FIELDS: request method, request scheme, request host, request path + query or params, headers, hashed request body
            string requestData = string.Join('\t', requestMethod.ToUpper(), "https", host, path, "", Convert.ToBase64String(requestBodyHash));

            byte[] signature = SignData_HMAC_SHA256(requestData, signingKey);

            string authHeaderValue = string.Format(authFormat, _clientToken, _accessToken, timestamp, Nonce, Convert.ToBase64String(signature));
            return new AuthenticationHeaderValue(AuthType, authHeaderValue);
        }

        private byte[] SignData_HMAC_SHA256(string data, byte[] key)
        {
            HMACSHA256 signingAlgorithm = new HMACSHA256(key);
            byte[] signature = signingAlgorithm.ComputeHash(Encoding.ASCII.GetBytes(data));
            return signature;
        }
    }
}
