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
