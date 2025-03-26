using System.Net.Http.Headers;
using Microsoft.Net.Http.Headers;

namespace Jobs.Common.Helpers;

public static class HttClientHeaderHelper
{
    public static HttpClient CreateHttpClientWithSecurityHeaders(string apiKey, string apiSecret, string token = null)
    {
        var httpClient = new HttpClient();
        SetHttpClientSecurityHeaders(httpClient, apiKey, apiSecret, token);
        return httpClient;
    }

    public static void SetHttpClientSecurityHeaders(HttpClient httpClient, string apiKey, string apiSecret, string token = null)
    {
        httpClient.DefaultRequestHeaders.Clear();
        
        httpClient.DefaultRequestHeaders.Add(HeaderNames.UserAgent, "JobsSiteSpecialClientAgent");
        
        
        var ticks = DateTime.UtcNow.Ticks;
        var sign = ticks * Math.PI * Math.E;
        var rounded = (long)Math.Ceiling(sign);
        var reverseNonce = new string(ticks.ToString().Reverse().ToArray());
        
        var signFirst = ticks * Math.PI;
        var roundedSignFirst = (long)Math.Ceiling(signFirst);
        var signSecond = ticks * Math.E;
        var roundedSignSecond = (long)Math.Ceiling(signSecond);
        
        var roundedSum = roundedSignFirst + roundedSignSecond;
        
        /*int[] intArray = ticks.ToString()
            .ToArray() 
            .Select(x=>x.ToString()) 
            .Select(int.Parse) 
            .ToArray(); */
        
        var nonceValue = $"{reverseNonce}-{rounded}-{roundedSum}";
        
        Console.WriteLine($"s-nonce : {nonceValue}");
        
        if (!string.IsNullOrWhiteSpace(token))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        
        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        httpClient.DefaultRequestHeaders.Add("s-nonce", nonceValue);
        httpClient.DefaultRequestHeaders.Add("x-api-secret", apiSecret);
        httpClient.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/json");
        //httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }
}