using JobProject.Contracts;
using JobProject.KeyModels;
using LiteDB;
using LiteDB.Async;

namespace JobProject.Repository;

public class LiteDbRepository : ILiteDbRepository
{
    public bool IsKeyValid(string key)
    {
        // Open database (or create if doesn't exist)
        using var db = new LiteDatabase(@"api_keys.db");
        
        var col = db.GetCollection<ApiKey>("keys");

        var result = col.FindOne(x => x.Key == key);
        
        if (result == null)
            return false;

        if (result.Expiration >= DateTime.UtcNow)
        {
            col.DeleteMany(x=> x.Key == key);
            return true;
        }

        return false;
    }
    
    public async Task<bool> IsKeyValidAsync(string key)
    {
        // Open database (or create if doesn't exist)
        using var db = new LiteDatabaseAsync(@"api_keys.db");
        //ar db = new LiteDatabaseAsync("Filename=mydatabase.db;Connection=shared;Password=hunter2");
        
        var col = db.GetCollection<ApiKey>("keys");
        

        var result = await col.FindOneAsync(x => x.Key == key);
        
        if (result == null)
            return false;

        if (result.Expiration >= DateTime.UtcNow)
        {
            await col.DeleteManyAsync(x=> x.Key == key);
            return true;
        }

        return false;
    }
    
    public void AddApiKey(ApiKey key)
    {
        // Open database (or create if doesn't exist)
        using var db = new LiteDatabase(@"api_keys.db");
        
        var col = db.GetCollection<ApiKey>("keys");
        // Insert new customer document (Id will be auto-incremented)
        col.Insert(key);
    }
    
    public async Task<bool> AddApiKeyAsync(ApiKey key)
    {
        // Open database (or create if doesn't exist)
        using var db = new LiteDatabaseAsync(@"api_keys.db");
        
        var col = db.GetCollection<ApiKey>("keys");
        
        // Insert new apikey document (Id will be auto-incremented)
        var upsertResult = await col.UpsertAsync(key);
        return upsertResult;
    }
}