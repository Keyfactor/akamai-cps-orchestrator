using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    public class AkamaiClientException : Exception
    {
        public HttpStatusCode ClientErrorCode;

        public AkamaiClientException(string message, HttpStatusCode statusCode) : base(message)
        {
            ClientErrorCode = statusCode;
        }
    }
}
