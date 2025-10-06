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

using System.Linq;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;

public class CertificateSubjectInformation
{
    /// <summary>
    /// The CN field of the Certificate Subject Information
    /// </summary>
    public string CommonName { get; init; }
    
    /// <summary>
    /// The O field of the Certificate Subject Information
    /// </summary>
    public string? Organization { get; init; }
    
    /// <summary>
    /// The OU field of the Certificate Subject Information
    /// </summary>
    public string? OrganizationalUnit { get; init; }
    
    /// <summary>
    /// The L field of the Certificate Subject Information
    /// </summary>
    public string? CityLocality { get; init; }
    
    /// <summary>
    /// The ST field of the Certificate Subject Information
    /// </summary>
    public string? StateProvince { get; init; }
    
    /// <summary>
    /// The C field of the Certificate Subject Information
    /// </summary>
    public string? CountryRegion { get; init; }
    
    /// <summary>
    /// The E field of the Certificate Subject Information
    /// </summary>
    public string? Email { get; init; }
    
    /// <summary>
    /// The original subject text
    /// </summary>
    public string SubjectText { get; init; }

    public static CertificateSubjectInformation ParseFromSubjectText(string subjectText)
    {
        var x509Name = new X509Name(subjectText);

        var result = new CertificateSubjectInformation()
        {
            SubjectText = subjectText,
            CommonName = GetLastStringValue(x509Name, X509Name.CN),
            CityLocality = GetLastStringValue(x509Name, X509Name.L),
            CountryRegion = GetLastStringValue(x509Name, X509Name.C),
            Email = GetLastStringValue(x509Name, X509Name.E),
            Organization = GetLastStringValue(x509Name, X509Name.O),
            OrganizationalUnit = GetLastStringValue(x509Name, X509Name.OU),
            StateProvince = GetLastStringValue(x509Name, X509Name.ST),
        };
        
        return result;
    }

    private static string GetLastStringValue(X509Name x509Name, DerObjectIdentifier identifier)
    {
        return x509Name.GetValueList(identifier).OfType<string>().LastOrDefault();
    }
}