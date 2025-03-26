using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobs.AccountApi.Contracts;
using Jobs.Common.Responses;
using Jobs.Common.SerializationSettings;
using Jobs.Entities.DataModel;
using Jobs.Entities.Responses;
using Keycloak.Client.Models;
using Serilog;

namespace Jobs.AccountApi.Services;

public class KeycloakAccountService(IHttpClientFactory httpClientFactory, 
    string baseUrl = "http://localhost:9001", 
    string realm = "mjobs", 
    string adminUserName = "admin", 
    string adminPassword = "newpwd"): IKeycloakAccountService
{
    public async Task<KeycloakTokenResponse> LoginAsync(string username, string password)
    {
        if (string.IsNullOrEmpty(username))
            throw new ArgumentNullException(nameof(username));
        
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentNullException(nameof(password));

        using var httpClient = httpClientFactory.CreateClient();
        
        // Form data is typically sent as key-value pairs
        var adminData = new List<KeyValuePair<string, string>>
        {
            new ("grant_type", "password"),
            new ("username", username),
            new ("password", password),
            new ("client_id", "admin-cli")
        };

        // Encodes the key-value pairs for the ContentType 'application/x-www-form-urlencoded'
        using var adminContent = new FormUrlEncodedContent(adminData);
        
        var adminResponse = await httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/realms/{realm}/protocol/openid-connect/token", adminContent).ConfigureAwait(false);
        adminResponse.EnsureSuccessStatusCode();
        var result = await adminResponse.Content.ReadAsStringAsync();
        
        return JsonSerializer.Deserialize<KeycloakTokenResponse>(result)!;
    }

    private async Task<KeycloakRespone?> GetKeycloakAccessToken()
    {
        using var httpClient = httpClientFactory.CreateClient();

        // Form data is typically sent as key-value pairs
        var adminData = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "password"),
            new("client_id", "admin-cli"),
            new("username", adminUserName),  //"admin"
            new("password", adminPassword), //"newpwd"
        };

        // Encodes the key-value pairs for the ContentType 'application/x-www-form-urlencoded'
        using var adminContent = new FormUrlEncodedContent(adminData);

        var adminResponse =
            await httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/realms/master/protocol/openid-connect/token",
                adminContent);
        adminResponse.EnsureSuccessStatusCode();

        var result = await adminResponse.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<KeycloakRespone>(result);
    }

    public async Task<RegisterUserResponse> RegisterUser(User user)
    {
        var dataToken = await GetKeycloakAccessToken();

        Log.Information($"Admin AccessToken: {dataToken?.AccessToken}");
        
        var userInfo = await GetUserAsync(dataToken?.AccessToken!, user.Email);

        if (userInfo == null) // user not found(not registered yet)
        {
            Console.WriteLine($"User not registered : {user.Email}, Password: {user.Password}");
            //var refreshedToken = await RefreshTokenAsync(dataToken.AccessToken);
            
            using var client = httpClientFactory.CreateClient();

            client.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));

            client.DefaultRequestHeaders.Authorization =
               //new AuthenticationHeaderValue("Bearer", refreshedToken?.AccessToken);
               new AuthenticationHeaderValue("Bearer", dataToken?.AccessToken);

            var userData = new UserContext(user)
            {
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                UserName = user.Email,
                Credentials = [new Credentials { Type = "password", Value = user.Password, Temporary = false }]
            };

            var requestJson = JsonSerializer.Serialize(userData);
            using var userContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

            Log.Information($"Request Json: {requestJson}");

            var response = await client.PostAsync($"{baseUrl.TrimEnd('/')}/admin/realms/{realm}/users", userContent);
            response.EnsureSuccessStatusCode();

            Log.Information("User Created!");
            
            return new RegisterUserResponse { Result = true, IsAdded = true };
        }

        return new RegisterUserResponse { Result = false, IsAdded = false };
    }

    public async Task<KeycloakRespone?> RefreshTokenAsync(string refreshToken)
    {
        using var httpClient = httpClientFactory.CreateClient();

        // Form data is typically sent as key-value pairs
        var adminData = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "refresh_token"),
            new("client_id", "admin-cli"),
            new("refresh_token", refreshToken)
        };

        // Encodes the key-value pairs for the ContentType 'application/x-www-form-urlencoded'
        using var adminContent = new FormUrlEncodedContent(adminData);

        var adminResponse =
            await httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/realms/master/protocol/openid-connect/token",
                adminContent);
        adminResponse.EnsureSuccessStatusCode();

        var result = await adminResponse.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<KeycloakRespone>(result);
        return data;
    }

    public async Task<bool> LogoutAsync(LogoutUser user)
    {
        var dataToken = await GetKeycloakAccessToken();
        
        Log.Information($"Admin AccessToken: {dataToken?.AccessToken}");

        var userInfo = await GetUserAsync(dataToken?.AccessToken!, user.Username);

        if (userInfo != null)
        {
            using var client = httpClientFactory.CreateClient();
        
            client.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", dataToken?.AccessToken);
            
            var url = $"{baseUrl.TrimEnd('/')}/admin/realms/{realm}/users/{userInfo.Id}/logout";
        
            Log.Information($"Logout URL - {url}");

            var logoutResponse= await client.PostAsync(url, null).ConfigureAwait(false);
            logoutResponse.EnsureSuccessStatusCode();

            if (logoutResponse.StatusCode == HttpStatusCode.NoContent)
            {
                Log.Information($"User - {userInfo.Username} logout ok.");
                return true;
            }
        }

        return false;
    }

    private async Task<UserRepresentation?> GetUserAsync(string accessToken, string userName)
    {
        using var client = httpClientFactory.CreateClient();
        
        client.DefaultRequestHeaders
            .Accept
            .Add(new MediaTypeWithQualityHeaderValue("application/json"));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer",  accessToken);
      
        var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/admin/realms/{realm}/users?username={userName}").ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var resultUser = await response.Content.ReadAsStringAsync();
        
        Log.Information($"User - {resultUser}");
        var userId = resultUser.Length > 2 ? resultUser.Substring(8, 36) : string.Empty;
        Log.Information($"UserId - {userId}");
        
        var userInfo = JsonSerializer.Deserialize<List<UserRepresentation>>(resultUser, JsonSerializerSetting.JsonSerializerOptions);
        Log.Information($"userInfo - {userInfo != null}, count - {userInfo?.Count}");
        
        return userInfo?.FirstOrDefault();
    }
}