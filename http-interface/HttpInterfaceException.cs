using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Keyfactor.Extensions.Utilities.HttpInterface
{
    public class HttpInterfaceException : Exception
    {
        public Uri RequestUri;
        public HttpStatusCode ErrorCode;
        public string Reason;

        public HttpInterfaceException(string message, HttpResponseMessage response) : base(message)
        {
            RequestUri = response.RequestMessage.RequestUri;
            ErrorCode = response.StatusCode;
            Reason = response.ReasonPhrase;
        }
    }
}
