namespace Marketplace.Warehouse.DTOs;

public record StockResponse(
    Guid Id,
    Guid ProductId,
    int Available,
    int Reserved);