using System.Net.Http.Json;
using Marketplace.Web.Models;

namespace Marketplace.Web.Services;

public class CatalogApi
{
    private readonly HttpClient _http;

    public CatalogApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<Product>> GetProductsAsync()
    {
        var response = await _http.GetAsync("/api/products");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Product>>() ?? new();
    }

    public async Task<Product?> GetProductAsync(Guid id)
    {
        var response = await _http.GetAsync($"/api/products/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<Product>();
    }

    public async Task AddToCartAsync(Guid userId, Guid productId, int quantity)
    {
        var request = new { userId, productId, quantity };
        var response = await _http.PostAsJsonAsync("/api/cart/items", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<CartItem>> GetCartAsync(Guid userId)
    {
        var response = await _http.GetAsync($"/api/cart?userId={userId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CartItem>>() ?? new();
    }

    public async Task RemoveFromCartAsync(Guid userId, Guid productId)
    {
        var response = await _http.DeleteAsync($"/api/cart/items/{productId}?userId={userId}");
        response.EnsureSuccessStatusCode();
    }
}