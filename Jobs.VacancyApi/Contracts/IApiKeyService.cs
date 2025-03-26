using JobProject.KeyModels;

namespace JobProject.Contracts;

public interface IApiKeyService
{
    //ApiKey GenerateApiKey();
    Task<ApiKey> GenerateApiKeyAsync();
    bool IsValidApiKey(string apiKey);
    bool IsNonceValid(long nonce);

    bool IsValid(string apiKey, long nonce);
}