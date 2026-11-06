using System.Net.Http.Json;
using Marketplace.Orders.Data;
using Marketplace.Orders.DTOs;
using Marketplace.Orders.Models;

namespace Marketplace.Orders.Services;

public class OrderSaga
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OrdersDbContext _db;

    public OrderSaga(IHttpClientFactory httpClientFactory, OrdersDbContext db)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
    }

    public async Task<(bool Success, string? Error)> ExecuteAsync(Order order, CreateOrderRequest request)
    {
        try
        {
            // Шаг 1: Получаем информацию о товарах из Catalog
            var catalogClient = _httpClientFactory.CreateClient("Catalog");
            var productPrices = new Dictionary<Guid, (string Name, decimal Price)>();

            foreach (var item in request.Items)
            {
                var product = await catalogClient.GetFromJsonAsync<ProductDto>($"/api/products/{item.ProductId}");
                if (product is null)
                {
                    return (false, $"Product {item.ProductId} not found");
                }
                productPrices[item.ProductId] = (product.Name, product.Price);
            }

            // Шаг 2: Создаём OrderItems со снимками данных
            decimal total = 0;
            foreach (var item in request.Items)
            {
                var (name, price) = productPrices[item.ProductId];
                order.Items.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    NameSnapshot = name,
                    PriceSnapshot = price,
                    Quantity = item.Quantity
                });
                total += price * item.Quantity;
            }
            order.Total = total;
            order.Status = OrderStatus.Created;
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // Шаг 3: Резервируем товары на складе
            order.Status = OrderStatus.ReservingStock;
            await _db.SaveChangesAsync();

            var warehouseClient = _httpClientFactory.CreateClient("Warehouse");
            var reservationRequest = new
            {
                orderId = order.Id,
                items = request.Items.Select(i => new { productId = i.ProductId, quantity = i.Quantity })
            };

            var reservationResponse = await warehouseClient.PostAsJsonAsync("/api/reservations", reservationRequest);
            if (!reservationResponse.IsSuccessStatusCode)
            {
                order.Status = OrderStatus.Failed;
                await _db.SaveChangesAsync();
                var errorContent = await reservationResponse.Content.ReadAsStringAsync();
                return (false, $"Failed to reserve stock: {errorContent}");
            }

            var reservation = await reservationResponse.Content.ReadFromJsonAsync<ReservationDto>();
            order.ReservationId = reservation!.Id;
            order.Status = OrderStatus.StockReserved;
            await _db.SaveChangesAsync();

            // Шаг 4: Проводим оплату
            order.Status = OrderStatus.ProcessingPayment;
            await _db.SaveChangesAsync();

            var paymentsClient = _httpClientFactory.CreateClient("Payments");
            var paymentRequest = new { orderId = order.Id, amount = order.Total };
            var paymentResponse = await paymentsClient.PostAsJsonAsync("/api/payments", paymentRequest);

            if (!paymentResponse.IsSuccessStatusCode)
            {
                // Компенсация: отменяем резерв
                await CompensateReservationAsync(warehouseClient, order.ReservationId.Value);
                order.Status = OrderStatus.Cancelled;
                await _db.SaveChangesAsync();
                var errorContent = await paymentResponse.Content.ReadAsStringAsync();
                return (false, $"Payment failed: {errorContent}");
            }

            var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentDto>();
            order.PaymentId = payment!.Id;
            order.Status = OrderStatus.Completed;
            await _db.SaveChangesAsync();

            return (true, null);
        }
        catch (Exception ex)
        {
            order.Status = OrderStatus.Failed;
            await _db.SaveChangesAsync();
            return (false, $"Unexpected error: {ex.Message}");
        }
    }

    private async Task CompensateReservationAsync(HttpClient warehouseClient, Guid reservationId)
    {
        try
        {
            await warehouseClient.PostAsync($"/api/reservations/{reservationId}/release", null);
        }
        catch
        {
            // Логируем, но не прерываем
        }
    }
}

internal record ProductDto(Guid Id, string Name, decimal Price);
internal record ReservationDto(Guid Id);
internal record PaymentDto(Guid Id);