using Jobs.Common.Contracts;
using Jobs.Common.DataModel;
using Jobs.Common.Helpers;
using JobsEntities.Models;
using WebApiCompany.Repositories;
using WebApiCompany.Services.Contracts;

namespace WebApiCompany.Services;

public class ApiKeyService(ILiteDbRepository repository, ISecretApiKeyRepository secretKeyRepository, ISecretApiService secretService) : IApiKeyService
{
    private async Task<SecretApiKey> GetSecretApiKey() => await secretKeyRepository.GetCurrentSecretApiKey();
    
    public async Task<ApiKey> GenerateApiKeyAsync()
    {
        var apiKey = new ApiKey
        {
            Key = ApiKeyGeneratorHelper.GenerateApiKey(),
            Expiration = DateTime.UtcNow.AddMinutes(30)
        };
        
        var result = await repository.AddApiKeyAsync(apiKey);

        return apiKey;
    }

    public bool IsValidApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) ) //|| 
            //(!string.IsNullOrEmpty(apiKey) && apiKey.Length > 64) ||
            //(!string.IsNullOrEmpty(apiKey) && apiKey.Length < 20))
            return false;
        
        var secretKey = GetSecretApiKey().Result;
        
        if(apiKey == secretKey.Key)
            return true;
        
        return repository.IsKeyValid(apiKey);
    }
    
    public bool IsValidApiKeyForToken(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) ) //|| 
            //(!string.IsNullOrEmpty(apiKey) && apiKey.Length > 64) ||
            //(!string.IsNullOrEmpty(apiKey) && apiKey.Length < 20))
            return false;
        
        var secretKey = GetSecretApiKey().Result;
        
        return apiKey == secretKey.Key;
    }
    
    public bool IsNonceValid(long nonce)
    {
        var diff = DateTime.UtcNow.Ticks - nonce;
        
        if(diff/TimeSpan.TicksPerSecond <= 5) // less than 5 seconds.
            return true;
        
        return false;
    }
    
    private bool IsSecretApiKeyValid(string realSecretApiKey) => secretService.SecretApi.Equals(realSecretApiKey);
    
    public bool IsValid(string apiKey, long nonce, string realSecretApiKey) => IsValidApiKey(apiKey) 
        && IsNonceValid(nonce) 
        && IsSecretApiKeyValid(realSecretApiKey);
}