namespace GroupFinance.Api.Authentication;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "GroupFinance";
    public string Audience { get; set; } = "GroupFinance.Client";
    public string SecretKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;
}
