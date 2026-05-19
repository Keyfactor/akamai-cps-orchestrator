// Copyright 2025 Keyfactor
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

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    public class NetworkConfiguration
    {
        public string[] disallowedTlsVersions { get; set; } = new string[0];
        public DnsNameSettings dnsNameSettings { get; set; } = new DnsNameSettings();
        public string geography { get; set; } = "core";
        public string preferredCiphers { get; set; } = "ak-akamai-default";
        public string mustHaveCiphers { get; set; } = "ak-akamai-default";
        public string secureNetwork { get; set; } = "standard-tls";
        public bool sniOnly { get; set; } = true;
        public bool quicEnabled { get; set; } = false;
        public string? ocspStapling { get; set; }
        public ClientMutualAuthentication? clientMutualAuthentication { get; set; }
    }

    public class DnsNameSettings
    {
        public bool cloneDnsNames { get; set; } = true;
        public string[] dnsNames { get; set; } = new string[0];
    }

    public class ClientMutualAuthentication
    {
        public string? setId { get; set; }
        public ClientMutualAuthenticationOptions? authenticationOptions { get; set; }
    }

    public class ClientMutualAuthenticationOptions
    {
        public bool sendCaListToClient { get; set; }
        public ClientMutualAuthenticationOcspOption? ocsp { get; set; }
    }

    public class ClientMutualAuthenticationOcspOption
    {
        public bool enabled { get; set; }
    }
}
