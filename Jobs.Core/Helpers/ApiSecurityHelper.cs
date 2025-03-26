using Jobs.Common.Constants;
using Jobs.Core.Contracts;
using Microsoft.AspNetCore.Http;

namespace Jobs.Core.Helpers;

public static class ApiSecurityHelper
{
    public static bool IsBadRequest(IHttpContextAccessor httpContextAccessor, 
        IEncryptionService cryptService,
        ISignedNonceService signedNonceService,
        IApiKeyService service,
        string apiKey, 
        string signedNonce,
        string apiSecret)
    {
        if (!UserAgentConstant.AppUserAgent.Equals(httpContextAccessor.HttpContext?.Request.Headers.UserAgent))
        {
            return true;
        }
        
        var (longNonce , resultParse) = signedNonceService.IsSignedNonceValid(signedNonce);

        /*if (builder.Environment.IsDevelopment())
        {
            longNonce = DateTime.Now.Ticks;
        }*/

        if (!resultParse)
        {
            return true;
        }
            
        // apiKey must be in Base64
        var realApiKey = cryptService.Decrypt(apiKey);
        var realApiSecret = cryptService.Decrypt(apiSecret);
            
        if (!service.IsValid(realApiKey, longNonce, realApiSecret))
        {
            return true;
        }

        return false;
    }
}