using System.Net.Http.Json;
using Marketplace.Web.Models;

namespace Marketplace.Web.Services;

public class OrdersApi
{
    private readonly HttpClient _http;

    public OrdersApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<Order?> CreateOrderAsync(List<OrderItemRequest> items)
    {
        var request = new { items };
        var response = await _http.PostAsJsonAsync("/api/orders", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<Order>();
    }

    public async Task<List<Order>> GetUserOrdersAsync()
    {
        var response = await _http.GetAsync("/api/orders");
        if (!response.IsSuccessStatusCode) return new();
        return await response.Content.ReadFromJsonAsync<List<Order>>() ?? new();
    }
}

public record OrderItemRequest(Guid ProductId, int Quantity);