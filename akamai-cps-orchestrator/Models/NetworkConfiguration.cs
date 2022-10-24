using System;
using System.Collections.Generic;
using System.Text;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    public class NetworkConfiguration
    {
        public string[] disallowedTlsVersions = new string[0];
        public DnsNameSettings dnsNameSettings = new DnsNameSettings();
        public string geography = "core";
        public string preferredCiphers = "ak-akamai-default";
        public string mustHaveCiphers = "ak-akamai-default";
        public string secureNetwork = "standard-tls";
        public bool sniOnly = true;
        public bool quicEnabled = false;
    }

    public class DnsNameSettings
    {
        public bool cloneDnsNames = true;
    }
}
