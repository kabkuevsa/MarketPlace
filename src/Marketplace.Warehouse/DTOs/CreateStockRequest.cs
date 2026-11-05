namespace Marketplace.Warehouse.DTOs;

public record CreateStockRequest(
    Guid ProductId,
    int Quantity);