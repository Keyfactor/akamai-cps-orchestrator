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

namespace Keyfactor.Extensions.Utilities.HttpInterface.Contexts;

public interface IAkamaiAuthContext
{
    /// <summary>
    /// Returns the current time to be used in the Akamai authentication header. This is used to ensure that the generated auth header is valid for a specific time window, as Akamai will reject requests with timestamps that are too far in the past or future.
    /// </summary>
    /// <returns></returns>
    DateTime GetTime();
    
    /// <summary>
    /// Returns a random nonce value to be used in the Akamai authentication header. This is used to ensure that each generated auth header is unique, even if generated at the same timestamp.
    /// </summary>
    /// <returns></returns>
    string GetNonce();
}

public sealed class AkamaiAuthContext : IAkamaiAuthContext
{
    public DateTime GetTime() => DateTime.UtcNow;
    public string GetNonce() => Guid.NewGuid().ToString();
}
