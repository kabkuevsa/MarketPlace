using System.Net;
using System.Net.Http.Json;
using Marketplace.Web.Models;

namespace Marketplace.Web.Services;

public class DeliveriesApi
{
    private readonly HttpClient _http;

    public DeliveriesApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<Delivery?> GetByOrderIdAsync(Guid orderId)
    {
        using var response = await _http.GetAsync(
            $"/api/deliveries/order/{orderId}");

        // Доставка могла ещё не создаться после оформления заказа.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"Не удалось загрузить доставку: {error}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<Delivery>()
            ?? throw new InvalidOperationException(
                "Сервис доставки вернул пустой ответ.");
    }
}