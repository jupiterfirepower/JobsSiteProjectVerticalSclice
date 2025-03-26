using Jobs.Core.Contracts;
using MediatR;

namespace Jobs.ReferenceApi.Helpers;

public static class GuardsHelper
{
    public static void Guards(ISender mediatr, IApiKeyService service,
        IEncryptionService cryptService, ISignedNonceService signedNonceService,
        IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(mediatr, nameof(mediatr));
        ArgumentNullException.ThrowIfNull(service, nameof(service));
        ArgumentNullException.ThrowIfNull(cryptService, nameof(cryptService));
        ArgumentNullException.ThrowIfNull(signedNonceService, nameof(signedNonceService));
        ArgumentNullException.ThrowIfNull(httpContextAccessor, nameof(httpContextAccessor));
    }
}