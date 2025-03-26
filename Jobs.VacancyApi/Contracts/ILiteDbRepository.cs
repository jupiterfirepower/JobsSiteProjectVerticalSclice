using JobProject.KeyModels;

namespace JobProject.Contracts;

public interface ILiteDbRepository
{
    bool IsKeyValid(string key);
    void AddApiKey(ApiKey key);
    Task<bool> IsKeyValidAsync(string key);
    Task<bool> AddApiKeyAsync(ApiKey key);
}