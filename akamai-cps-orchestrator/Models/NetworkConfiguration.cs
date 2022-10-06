using System;
using System.Collections.Generic;
using System.Text;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models
{
    public class NetworkConfiguration
    {
        // not all fields are here currently, only required ones
        public string[] disallowedTlsVersions = new string[0];
        public string geography = "core";
        public string preferredCiphers = "ak-akamai-default-2017q3";
        public string mustHaveCiphers = "ak-akamai-default-2017q3";
        public string secureNetwork = "standard-tls";
        public string sni = "on"; // may need to make sure this is serialized even though it is null
        //public bool sniOnly = false;
    }
}
