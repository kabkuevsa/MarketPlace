namespace Marketplace.Web.Models;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password);
public record AuthResponse(string Token, string Email, Guid UserId);