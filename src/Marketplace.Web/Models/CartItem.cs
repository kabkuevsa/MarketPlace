namespace Marketplace.Web.Models;

public record CartItem(
    Guid Id,
    Guid UserId,
    Guid ProductId,
    string ProductName,
    decimal ProductPrice,
    int Quantity,
    decimal TotalPrice);