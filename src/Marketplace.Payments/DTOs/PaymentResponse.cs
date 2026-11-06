namespace Marketplace.Payments.DTOs;

public record PaymentResponse(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    string Status,
    DateTime CreatedAt);