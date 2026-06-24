using Asp.Versioning;
using EventosVivos.API.Configuration;
using EventosVivos.Application.Common;
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

/// <summary>Handles Microsoft OAuth2 authentication and JWT token lifecycle.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
[Produces("application/json")]
public class AuthController(
    IOptions<JwtSettings> jwtOptions,
    IOptions<OAuthSettings> oauthOptions,
    ILogger<AuthController> logger,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    private readonly JwtSettings _jwt = jwtOptions.Value;
    private readonly OAuthSettings _oauth = oauthOptions.Value;

    /// <summary>
    /// Exchanges a Microsoft OAuth2 authorization code for an access token and a refresh token.
    /// </summary>
    /// <remarks>
    /// The frontend receives the authorization code after Microsoft redirects back to the
    /// callback URL. This endpoint exchanges that code server-side (keeping the client_secret
    /// on the server) and returns a short-lived access token (default: 60 min) and a
    /// long-lived refresh token (default: 7 days).
    /// </remarks>
    /// <response code="200">Token pair emitted successfully.</response>
    /// <response code="401">The authorization code could not be exchanged with Microsoft.</response>
    [HttpPost("microsoft/exchange")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MicrosoftExchange([FromBody] CodeExchangeRequest request, CancellationToken ct)
    {
        var userInfo = await ExchangeMicrosoftCodeAsync(request.Code, request.RedirectUri, ct);
        if (userInfo is null)
            return Unauthorized(new { error = I18n.AuthExchangeFailed });

        var accessToken  = GenerateAccessToken(userInfo.Value.Email, userInfo.Value.Name, "microsoft");
        var refreshToken = GenerateRefreshToken(userInfo.Value.Email, userInfo.Value.Name, "microsoft");
        return Ok(new TokenResponse(accessToken, refreshToken, "Bearer", _jwt.ExpiresInMinutes * 60));
    }

    /// <summary>
    /// Issues a new access token and refresh token from a valid refresh token (rotation).
    /// </summary>
    /// <remarks>
    /// Each successful call invalidates the provided refresh token and returns a brand-new pair.
    /// If the refresh token is expired or tampered, the endpoint returns 401 and the user
    /// must re-authenticate via the OAuth2 flow.
    /// </remarks>
    /// <response code="200">New token pair issued.</response>
    /// <response code="401">Refresh token is invalid, expired, or is an access token.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Refresh([FromBody] RefreshRequest request)
    {
        var principal = ValidateRefreshToken(request.RefreshToken);
        if (principal is null)
            return Unauthorized(new { error = I18n.AuthInvalidRefreshToken });

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
                logger.LogWarning("Microsoft token exchange failed [{Status}]: {Error}", (int)tokenResponse.StatusCode, err);
                logger.LogWarning("Exchange params — client_id: {ClientId}, redirect_uri: {RedirectUri}, tenant: {Tenant}",
                    _oauth.Microsoft.ClientId, redirectUri, _oauth.Microsoft.TenantId);
                return null;
            }

            var json    = await tokenResponse.Content.ReadAsStringAsync(ct);
            var doc     = JsonDocument.Parse(json);
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
            if (principal.FindFirstValue("token_type") != "refresh") return null;
            return principal;
        }
        catch { return null; }
    }
}

/// <summary>Request body for the Microsoft OAuth2 code exchange.</summary>
/// <param name="Code">Authorization code received from Microsoft's redirect.</param>
/// <param name="RedirectUri">Must match the URI registered in Azure AD.</param>
public record CodeExchangeRequest(string Code, string RedirectUri);

/// <summary>Request body for refreshing tokens.</summary>
/// <param name="RefreshToken">A valid, non-expired refresh token.</param>
public record RefreshRequest(string RefreshToken);

/// <summary>JWT token pair returned after successful authentication or refresh.</summary>
/// <param name="AccessToken">Short-lived JWT for API calls (default: 60 min).</param>
/// <param name="RefreshToken">Long-lived token to obtain new access tokens (default: 7 days).</param>
/// <param name="TokenType">Always "Bearer".</param>
/// <param name="ExpiresIn">Access token lifetime in seconds.</param>
public record TokenResponse(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn);
