namespace EventosVivos.API.Configuration;

public class OAuthSettings
{
    public MicrosoftOAuthSettings Microsoft { get; set; } = new();
}

public class MicrosoftOAuthSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TenantId { get; set; } = "common";
    public string TokenEndpoint => $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token";
}
