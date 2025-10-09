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

using Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;
using Xunit;

namespace akamai_cps_orchestrator.Tests.Models;

public class CertificateSubjectInformationTests
{
    [Fact]
    public void ParseFromSubjectText_WhenCompleteSubjectTextIsProvided_ParsesCommonName()
    {
        var subjectText = "CN=commonname,E=test@example.com,O=organization,OU=ou,L=city,ST=state,C=CR";
        var result = CertificateSubjectInformation.ParseFromSubjectText(subjectText);
        
        Assert.Equal("commonname", result.CommonName);
    }
    
    [Fact]
    public void ParseFromSubjectText_WhenCompleteSubjectTextIsProvided_ParsesEmail()
    {
        var subjectText = "CN=commonname,E=test@example.com,O=organization,OU=ou,L=city,ST=state,C=CR";
        var result = CertificateSubjectInformation.ParseFromSubjectText(subjectText);
        
        Assert.Equal("test@example.com", result.Email);
    }
    
    [Fact]
    public void ParseFromSubjectText_WhenCompleteSubjectTextIsProvided_ParsesOrganization()
    {
        var subjectText = "CN=commonname,E=test@example.com,O=organization,OU=ou,L=city,ST=state,C=CR";
        var result = CertificateSubjectInformation.ParseFromSubjectText(subjectText);
        
        Assert.Equal("organization", result.Organization);
    }
    
    [Fact]
    public void ParseFromSubjectText_WhenOrganizationIncludesComma_ParsesOrganization()
    {
        var subjectText = "CN=commonname,E=test@example.com,O=Org\\, Inc.,OU=ou,L=city,ST=state,C=CR";
        var result = CertificateSubjectInformation.ParseFromSubjectText(subjectText);
        
        Assert.Equal("Org, Inc.", result.Organization);
    }
    
    [Fact]
    public void ParseFromSubjectText_WhenCompleteSubjectTextIsProvided_ParsesOrganizationalUnit()
    {
        var subjectText = "CN=commonname,E=test@example.com,O=organization,OU=ou,L=city,ST=state,C=CR";
        var result = CertificateSubjectInformation.ParseFromSubjectText(subjectText);
        
        Assert.Equal("ou", result.OrganizationalUnit);
    }
    
    [Fact]
    public void ParseFromSubjectText_WhenCompleteSubjectTextIsProvided_ParsesCityLocality()
    {
        var subjectText = "CN=commonname,E=test@example.com,O=organization,OU=ou,L=city,ST=state,C=CR";
        var result = CertificateSubjectInformation.ParseFromSubjectText(subjectText);
        
        Assert.Equal("city", result.CityLocality);
    }
    
    [Fact]
    public void ParseFromSubjectText_WhenCompleteSubjectTextIsProvided_ParsesCountryRegion()
    {
        var subjectText = "CN=commonname,E=test@example.com,O=organization,OU=ou,L=city,ST=state,C=CR";
        var result = CertificateSubjectInformation.ParseFromSubjectText(subjectText);
        
        Assert.Equal("CR", result.CountryRegion);
    }
    
    [Fact]
    public void ParseFromSubjectText_WhenFieldIsEmpty_SetsFieldValueToNull()
    {
        var subjectText = "CN=commonname,O=organization,OU=ou,L=city,ST=state,C=CR";
        var result = CertificateSubjectInformation.ParseFromSubjectText(subjectText);
        
        Assert.Null(result.Email);
    }
}
