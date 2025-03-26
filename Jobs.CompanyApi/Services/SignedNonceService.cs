using Jobs.Common.Contracts;
using Jobs.Common.Helpers;

namespace WebApiCompany.Services;

public class SignedNonceService: ISignedNonceService
{
    private readonly NonceParserHelper _helper = new NonceParserHelper();

    public (long, bool) IsSignedNonceValid(string signedNonce) => _helper.IsSignedNonceValid(signedNonce);
}