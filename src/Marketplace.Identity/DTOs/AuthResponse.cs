namespace Marketplace.Identity.DTOs;

public record AuthResponse(string Token, DateTime ExpiresAt);