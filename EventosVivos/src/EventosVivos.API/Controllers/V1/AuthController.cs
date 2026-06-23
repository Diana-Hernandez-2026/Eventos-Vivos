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
    /// Exchanges a Microsoft authorization code for an app access token + refresh token.
    /// </summary>
    [HttpPost("microsoft/exchange")]
    public async Task<IActionResult> MicrosoftExchange([FromBody] CodeExchangeRequest request, CancellationToken ct)
    {
        var userInfo = await ExchangeMicrosoftCodeAsync(request.Code, request.RedirectUri, ct);
        if (userInfo is null)
            return Unauthorized(new { error = "Could not exchange Microsoft authorization code." });

        var accessToken  = GenerateAccessToken(userInfo.Value.Email, userInfo.Value.Name, "microsoft");
        var refreshToken = GenerateRefreshToken(userInfo.Value.Email, userInfo.Value.Name, "microsoft");
        return Ok(new TokenResponse(accessToken, refreshToken, "Bearer", _jwt.ExpiresInMinutes * 60));
    }

    /// <summary>
    /// Issues a new access token + refresh token given a valid, non-expired refresh token.
    /// Implements refresh token rotation: each use invalidates the previous refresh token.
    /// </summary>
    [HttpPost("refresh")]
    public IActionResult Refresh([FromBody] RefreshRequest request)
    {
        var principal = ValidateRefreshToken(request.RefreshToken);
        if (principal is null)
            return Unauthorized(new { error = "Invalid or expired refresh token." });

        var email    = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var name     = principal.FindFirstValue(JwtRegisteredClaimNames.Name) ?? email;
        var provider = principal.FindFirstValue("provider") ?? "microsoft";

        var newAccessToken  = GenerateAccessToken(email, name, provider);
        var newRefreshToken = GenerateRefreshToken(email, name, provider);
        return Ok(new TokenResponse(newAccessToken, newRefreshToken, "Bearer", _jwt.ExpiresInMinutes * 60));
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<(string Email, string Name)?> ExchangeMicrosoftCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var tokenResponse = await client.PostAsync(
                _oauth.Microsoft.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"]          = code,
                    ["client_id"]     = _oauth.Microsoft.ClientId,
                    ["client_secret"] = _oauth.Microsoft.ClientSecret,
                    ["redirect_uri"]  = redirectUri,
                    ["grant_type"]    = "authorization_code",
                    ["scope"]         = "openid email profile User.Read"
                }), ct);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                var err = await tokenResponse.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Microsoft token exchange failed: {Error}", err);
                return null;
            }

            var json = await tokenResponse.Content.ReadAsStringAsync(ct);
            var doc  = JsonDocument.Parse(json);
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

    private string GenerateAccessToken(string email, string name, string provider)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   email),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name,  name),
            new Claim("provider",                    provider),
            new Claim("token_type",                  "access"),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        return BuildJwt(claims, DateTime.UtcNow.AddMinutes(_jwt.ExpiresInMinutes));
    }

    private string GenerateRefreshToken(string email, string name, string provider)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,  email),
            new Claim(JwtRegisteredClaimNames.Name, name),
            new Claim("provider",                   provider),
            new Claim("token_type",                 "refresh"),
            new Claim(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString())
        };

        return BuildJwt(claims, DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiresInDays));
    }

    private string BuildJwt(IEnumerable<Claim> claims, DateTime expires)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:            _jwt.Issuer,
            audience:          _jwt.Audience,
            claims:            claims,
            expires:           expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal? ValidateRefreshToken(string token)
    {
        try
        {
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
            var handler = new JwtSecurityTokenHandler();

            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = key,
                ValidateIssuer           = true,
                ValidIssuer              = _jwt.Issuer,
                ValidateAudience         = true,
                ValidAudience            = _jwt.Audience,
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.Zero
            }, out _);

            // Reject access tokens passed where a refresh token is expected
            if (principal.FindFirstValue("token_type") != "refresh") return null;
            return principal;
        }
        catch { return null; }
    }
}

public record CodeExchangeRequest(string Code, string RedirectUri);
public record RefreshRequest(string RefreshToken);
public record TokenResponse(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn);
