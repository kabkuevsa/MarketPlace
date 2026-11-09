using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Marketplace.Web.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly TokenStorageService _tokenStorage;
    private readonly HttpClient _httpClient;

    public CustomAuthStateProvider(TokenStorageService tokenStorage, HttpClient httpClient)
    {
        _tokenStorage = tokenStorage;
        _httpClient = httpClient;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStorage.GetTokenAsync();

        if (string.IsNullOrEmpty(token))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var normalizedClaims = jwtToken.Claims.Select(claim =>
        {
            var claimType = claim.Type switch
            {
                "nameid" or "sub" => ClaimTypes.NameIdentifier,
                "email" => ClaimTypes.Email,
                _ => claim.Type
            };

            return new Claim(
                claimType,
                claim.Value,
                claim.ValueType,
                claim.Issuer);
        });

        var identity = new ClaimsIdentity(
            normalizedClaims,
            "jwt",
            ClaimTypes.Email,
            ClaimTypes.Role);
        var user = new ClaimsPrincipal(identity);

        // Добавляем токен в заголовок HttpClient для последующих запросов
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return new AuthenticationState(user);
    }

    public async Task MarkUserAsAuthenticated(string token)
    {
        await _tokenStorage.SetTokenAsync(token);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task MarkUserAsLoggedOut()
    {
        await _tokenStorage.ClearTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}