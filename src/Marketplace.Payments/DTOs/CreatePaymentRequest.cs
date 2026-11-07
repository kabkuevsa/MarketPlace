namespace Marketplace.Payments.DTOs;

public record CreatePaymentRequest(
    Guid OrderId,
    decimal Amount);