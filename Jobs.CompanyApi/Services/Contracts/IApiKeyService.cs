using Jobs.Common.DataModel;

namespace WebApiCompany.Services.Contracts;

public interface IApiKeyService
{
    //ApiKey GenerateApiKey();
    Task<ApiKey> GenerateApiKeyAsync();
    bool IsValidApiKey(string apiKey);
    bool IsNonceValid(long nonce);
    //bool IsValid(string apiKey, long nonce);
    bool IsValid(string apiKey, long nonce, string realSecretApiKey);
}