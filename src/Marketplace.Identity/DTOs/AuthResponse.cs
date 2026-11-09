namespace Marketplace.Identity.DTOs;

public record AuthResponse(
    string Token,
    string Email,
    Guid UserId,
    DateTime ExpiresAt);