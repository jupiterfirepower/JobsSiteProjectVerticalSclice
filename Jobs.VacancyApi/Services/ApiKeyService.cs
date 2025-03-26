using JobProject.Contracts;
using JobProject.KeyModels;
using JobProject.Repository;
using JobsEntities.Models;

namespace JobProject.Services;

public class ApiKeyService(ILiteDbRepository repository, ISecretApiKeyRepository secretKeyRepository, ILogger<ApiKeyService> logger) : IApiKeyService
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
        if (string.IsNullOrEmpty(apiKey) || 
            (!string.IsNullOrEmpty(apiKey) && apiKey.Length > 64) ||
            (!string.IsNullOrEmpty(apiKey) && apiKey.Length < 20))
            return false;
        
        var secretKey = GetSecretApiKey().Result;
        
        if(apiKey == secretKey.Key)
            return true;
        
        return repository.IsKeyValid(apiKey);
    }
    
    public bool IsNonceValid(long nonce)
    {
        var diff = DateTime.UtcNow.Ticks - nonce;
        logger.LogInformation($"Diff : {diff}, diff/TimeSpan.TicksPerSecond : {diff/TimeSpan.TicksPerSecond}");
        
        if(diff/TimeSpan.TicksPerSecond <= 5) // less than 5 seconds.
            return true;
        
        return false;
    }

    public bool IsValid(string apiKey, long nonce) => IsValidApiKey(apiKey) && IsNonceValid(nonce);

}