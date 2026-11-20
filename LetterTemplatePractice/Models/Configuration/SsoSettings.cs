namespace LetterTemplatePractice.Models.Configuration;

public class SsoSettings
{
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ResponseType { get; set; } = "code";
    public string ResponseMode { get; set; } = "query";
    public bool SaveTokens { get; set; } = true;
    public bool GetClaimsFromUserInfoEndpoint { get; set; } = true;
    public string Scopes { get; set; } = "openid email profile roles";
    public string CallbackPath { get; set; } = "/signin-oidc";
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";
    public string RemoteSignOutPath { get; set; } = "/signout-oidc";
}
