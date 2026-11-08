
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

    public async Task<Order> CreateOrderAsync(List<OrderItemRequest> items)
    {
        if (items is null || items.Count == 0)
            throw new ArgumentException("Order must contain at least one item.");

        if (items.Any(i => i.ProductId == Guid.Empty || i.Quantity <= 0))
            throw new ArgumentException("Order contains invalid items.");

        var request = new { items };

        using var response = await _http.PostAsJsonAsync(
            "/api/orders",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"Не удалось создать заказ. HTTP {(int)response.StatusCode}. {error}",
                null,
                response.StatusCode);
        }

        var order = await response.Content.ReadFromJsonAsync<Order>();

        return order ?? throw new InvalidOperationException(
            "Сервер создал заказ, но вернул пустой ответ.");
    }

    public async Task<List<Order>> GetUserOrdersAsync()
    {
        using var response = await _http.GetAsync("/api/orders");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"Не удалось загрузить заказы. HTTP {(int)response.StatusCode}. {error}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<List<Order>>()
               ?? new List<Order>();
    }
}

public record OrderItemRequest(Guid ProductId, int Quantity);
