namespace Jobs.Common.Constants;

public static class HttpHeaderKeys
{
    public const string XApiHeaderKey = "x-api-key";
    public const string SNonceHeaderKey = "s-nonce";
    public const string XApiSecretHeaderKey = "x-api-secret";
    public const string AppJsonMediaTypeValue = "application/json";
    public const int XApiHeaderKeyMaxLength = 100;
    public const int XApiHeaderKeyMinLength = 15;
    public const int SNonceHeaderKeyMaxLength = 64;
    public const int SNonceHeaderKeyMinLength = 10;
    public const int XApiSecretHeaderKeyMaxLength = 64;
    public const int XApiSecretHeaderKeyMinLength = 10;
}