
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

    public async Task<Order> CreateOrderAsync(
        List<OrderItemRequest> items,
        string deliveryAddress)
    {
        if (items is null || items.Count == 0)
            throw new ArgumentException(
                "Order must contain at least one item.");

        if (string.IsNullOrWhiteSpace(deliveryAddress))
            throw new ArgumentException(
                "Delivery address is required.");

        var request = new
        {
            items,
            deliveryAddress = deliveryAddress.Trim()
        };

        using var response = await _http.PostAsJsonAsync(
            "/api/orders",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"Не удалось создать заказ: {error}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<Order>()
            ?? throw new InvalidOperationException(
                "Сервер вернул пустой ответ.");
    }

    public async Task<List<Order>> GetUserOrdersAsync()
    {
        using var response =
            await _http.GetAsync("/api/orders");

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"Не удалось загрузить заказы: {error}",
                null,
                response.StatusCode);
        }

        return await response.Content
            .ReadFromJsonAsync<List<Order>>()
            ?? new List<Order>();
    }
}

public record OrderItemRequest(
    Guid ProductId,
    int Quantity);
