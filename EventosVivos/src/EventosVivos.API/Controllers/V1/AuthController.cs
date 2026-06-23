using Asp.Versioning;
using EventosVivos.API.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace EventosVivos.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public class AuthController(
    IOptions<JwtSettings> jwtOptions,
    IOptions<OAuthSettings> oauthOptions,
    ILogger<AuthController> logger,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    private readonly JwtSettings _jwt = jwtOptions.Value;
    private readonly OAuthSettings _oauth = oauthOptions.Value;

    /// <summary>
    /// Exchanges a Microsoft authorization code (from OAuth2 redirect) for an app JWT.
    /// The frontend sends the code it received from login.microsoftonline.com.
    /// </summary>
    [HttpPost("microsoft/exchange")]
    public async Task<IActionResult> MicrosoftExchange([FromBody] CodeExchangeRequest request, CancellationToken ct)
    {
        var userInfo = await ExchangeMicrosoftCodeAsync(request.Code, request.RedirectUri, ct);
        if (userInfo is null)
            return Unauthorized(new { error = "Could not exchange Microsoft authorization code." });

        var token = GenerateJwt(userInfo.Value.Email, userInfo.Value.Name, "microsoft");
        return Ok(new TokenResponse(token, "Bearer", _jwt.ExpiresInMinutes * 60));
    }

    private async Task<(string Email, string Name)?> ExchangeMicrosoftCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var tokenResponse = await client.PostAsync(
                _oauth.Microsoft.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = _oauth.Microsoft.ClientId,
                    ["client_secret"] = _oauth.Microsoft.ClientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code",
                    ["scope"] = "openid email profile User.Read"
                }), ct);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                var err = await tokenResponse.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Microsoft token exchange failed: {Error}", err);
                return null;
            }

            var json = await tokenResponse.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            var idToken = doc.RootElement.GetProperty("id_token").GetString()!;
            return ExtractClaimsFromJwt(idToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Microsoft code exchange threw an exception");
            return null;
        }
    }

    private static (string Email, string Name)? ExtractClaimsFromJwt(string idToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(idToken)) return null;

            var jwt = handler.ReadJwtToken(idToken);

            var email = jwt.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value
                     ?? jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value
                     ?? jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;

            var name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value
                    ?? jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.GivenName)?.Value
                    ?? email;

            if (string.IsNullOrEmpty(email)) return null;
            return (email, name ?? email);
        }
        catch { return null; }
    }

    private string GenerateJwt(string email, string name, string provider)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, email),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name, name),
            new Claim("provider", provider),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.ExpiresInMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record CodeExchangeRequest(string Code, string RedirectUri);
public record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);
