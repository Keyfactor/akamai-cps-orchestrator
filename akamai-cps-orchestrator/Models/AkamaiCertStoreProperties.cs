using Newtonsoft.Json;

namespace Keyfactor.Orchestrator.Extensions.AkamaiCpsOrchestrator.Models;

public class AkamaiCertStoreProperties
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; }
    
    [JsonProperty("client_token")]
    public string ClientToken { get; set; }
    
    [JsonProperty("client_secret")]
    public string ClientSecret { get; set; }
    
    [JsonProperty("ContractId")]
    public string? ContractId { get; set; }
}
