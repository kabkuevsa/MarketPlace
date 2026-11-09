using System.Net.Http.Json;
using Marketplace.Web.Models;

namespace Marketplace.Web.Services;

public class IdentityApi
{
    private readonly HttpClient _http;

    public IdentityApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<AuthResponse?> LoginAsync(string email, string password)
    {
        var request = new LoginRequest(email, password);
        var response = await _http.PostAsJsonAsync("/api/auth/login", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    public async Task<bool> RegisterAsync(string email, string password)
    {
        var request = new RegisterRequest(email, password);
        var response = await _http.PostAsJsonAsync("/api/auth/register", request);
        return response.IsSuccessStatusCode;
    }
}