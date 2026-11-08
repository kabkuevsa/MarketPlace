using System.Net;
using System.Net.Http.Json;
using Marketplace.Orders.Data;
using Marketplace.Orders.DTOs;
using Marketplace.Orders.Models;
using Marketplace.Shared.Events;
using MassTransit;

namespace Marketplace.Orders.Services;

public class OrderSaga
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OrdersDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OrderSaga> _logger;

    public OrderSaga(
        IHttpClientFactory httpClientFactory,
        OrdersDbContext db,
        IPublishEndpoint publishEndpoint,
        ILogger<OrderSaga> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<(bool Success, string? Error)> ExecuteAsync(Order order, CreateOrderRequest request)
    {
        var warehouseClient = _httpClientFactory.CreateClient("Warehouse");
        Guid? reservationId = null;
        var orderSaved = false;

        try
        {
            // 1. Цены из каталога (404 обрабатывается явно)
            var catalogClient = _httpClientFactory.CreateClient("Catalog");
            var productPrices = new Dictionary<Guid, (string Name, decimal Price)>();

            foreach (var productId in request.Items.Select(i => i.ProductId).Distinct())
            {
                var response = await catalogClient.GetAsync($"/api/products/{productId}");
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return (false, $"Product {productId} not found");
                }
                response.EnsureSuccessStatusCode();

                var product = await response.Content.ReadFromJsonAsync<ProductDto>();
                if (product is null)
                {
                    return (false, $"Product {productId} not found");
                }
                productPrices[productId] = (product.Name, product.Price);
            }

            // 2. Сохраняем заказ
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
            orderSaved = true;

            // 3. Резерв на складе
            order.Status = OrderStatus.ReservingStock;
            await _db.SaveChangesAsync();

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
            reservationId = reservation!.Id;
            order.ReservationId = reservationId;
            order.Status = OrderStatus.StockReserved;
            await _db.SaveChangesAsync();

            // 4. Оплата
            order.Status = OrderStatus.ProcessingPayment;
            await _db.SaveChangesAsync();

            var paymentsClient = _httpClientFactory.CreateClient("Payments");
            var paymentRequest = new { orderId = order.Id, amount = order.Total };
            var paymentResponse = await paymentsClient.PostAsJsonAsync("/api/payments", paymentRequest);

            if (!paymentResponse.IsSuccessStatusCode)
            {
                await CompensateReservationAsync(warehouseClient, reservationId.Value);
                order.Status = OrderStatus.Cancelled;
                await _db.SaveChangesAsync();

                await TryPublishAsync(new OrderCancelledEvent(
                    order.Id, order.ReservationId, null, "Payment failed"));

                var errorContent = await paymentResponse.Content.ReadAsStringAsync();
                return (false, $"Payment failed: {errorContent}");
            }

            var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentDto>();
            order.PaymentId = payment!.Id;
            order.Status = OrderStatus.Completed;
            await _db.SaveChangesAsync();

            // Заказ уже оплачен: сбой публикации не должен превращать его в Failed.
            await TryPublishAsync(new OrderCompletedEvent(order.Id, reservationId.Value));

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order {OrderId} failed", order.Id);

            // Резерв снимаем только если деньги ещё не списаны.
            if (reservationId is not null && order.PaymentId is null)
            {
                await CompensateReservationAsync(warehouseClient, reservationId.Value);
            }

            if (orderSaved)
            {
                try
                {
                    order.Status = OrderStatus.Failed;
                    await _db.SaveChangesAsync();
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Could not mark order {OrderId} as Failed", order.Id);
                }
            }

            return (false, $"Unexpected error: {ex.Message}");
        }
    }

    private async Task CompensateReservationAsync(HttpClient warehouseClient, Guid reservationId)
    {
        try
        {
            await warehouseClient.PostAsync($"/api/reservations/{reservationId}/release", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release reservation {ReservationId}", reservationId);
        }
    }

    private async Task TryPublishAsync<T>(T message) where T : class
    {
        try
        {
            await _publishEndpoint.Publish(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish {Event}", typeof(T).Name);
        }
    }
}

internal record ProductDto(Guid Id, string Name, decimal Price);
internal record ReservationDto(Guid Id);
internal record PaymentDto(Guid Id);
