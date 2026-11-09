using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Marketplace.Web.Services;

public class TokenStorageService
{
    private readonly ProtectedSessionStorage _sessionStorage;
    private const string TokenKey = "jwt_token";

    public TokenStorageService(ProtectedSessionStorage sessionStorage)
    {
        _sessionStorage = sessionStorage;
    }

    public async Task SetTokenAsync(string token)
    {
        await _sessionStorage.SetAsync(TokenKey, token);
    }

    public async Task<string?> GetTokenAsync()
    {
        var result = await _sessionStorage.GetAsync<string>(TokenKey);
        return result.Success ? result.Value : null;
    }

    public async Task ClearTokenAsync()
    {
        await _sessionStorage.DeleteAsync(TokenKey);
    }
}