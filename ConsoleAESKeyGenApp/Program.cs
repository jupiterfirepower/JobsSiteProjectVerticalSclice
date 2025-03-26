// See https://aka.ms/new-console-template for more information

using System.Security.Cryptography;
using FS.Keycloak.RestApiClient.Api;
using FS.Keycloak.RestApiClient.Authentication.ClientFactory;
using FS.Keycloak.RestApiClient.Authentication.Flow;
using FS.Keycloak.RestApiClient.ClientFactory;
using Jobs.Core.Services;

Console.WriteLine("Creating Aes Encryption 256 bit key");

var workTypes = new List<int>();
var paramWorkTypes = string.Join(",", workTypes);
Console.WriteLine($"paramWorkTypes - {paramWorkTypes}");
using (Aes aesAlgorithm = Aes.Create())
{
    aesAlgorithm.KeySize = 256;
    aesAlgorithm.GenerateKey();
    string keyBase64 = Convert.ToBase64String(aesAlgorithm.Key);
    Console.WriteLine($"Aes Key Size : {aesAlgorithm.KeySize}");
    Console.WriteLine("Here is the Aes key in Base64:");
    Console.WriteLine(keyBase64);
    string ivBase64 = Convert.ToBase64String(aesAlgorithm.IV.ToArray());
    Console.WriteLine(ivBase64);

    var testKey = "123456789123456789123";
    var key = Convert.FromBase64String("QsDJ84U8bbsrtekK8GvGUqo1Burnfy4qe2BU9OtLtJk=");
    var iv = Convert.FromBase64String("ELoct42LnTjZpfft5Y9TAw==");
    var cryptoProvider = new NaiveEncryptionService(key, iv);
    var result = cryptoProvider.Encrypt(testKey);
    Console.WriteLine(result);
    
    var AccountApiSecretKey = "zc32423gfdgbvcxb34wrhyh254t63avdfvfdve2534zxxvasd";
    var accountSecretKeyResult = cryptoProvider.Encrypt(AccountApiSecretKey);
    Console.WriteLine(accountSecretKeyResult);
   

    var credentials = new PasswordGrantFlow
    {
        KeycloakUrl = "http://localhost:9001",
        Realm = "master",
        UserName = "admin",
        Password = "newpwd"
    };

    using var httpClient = AuthenticationHttpClientFactory.Create(credentials);
    using var usersApi = ApiClientFactory.Create<UsersApi>(httpClient);

    var users = await usersApi.GetUsersAsync("mjobs", username:"jupiter");
    Console.WriteLine($"Users: {users.Count}");
    var credentials1 = new ClientCredentialsFlow
    {
        KeycloakUrl = "http://localhost:9001",
        Realm = "mjobs",
        ClientId = "confmjobs",
        ClientSecret = "SRrvfrHZ5dLcBJG8Qq8Ph8lIYrwtKqzj"
    };

    using var httpClient1 = AuthenticationHttpClientFactory.Create(credentials1);
    using var usersApi1 = ApiClientFactory.Create<UsersApi>(httpClient1);

    var users1 = await usersApi1.GetUsersAsync("mjobs");
    //Console.WriteLine($"Users: {users1.Count}");
}
Console.ReadLine();