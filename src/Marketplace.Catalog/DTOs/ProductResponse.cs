namespace Marketplace.Catalog.DTOs;

public record ProductResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string Description,
    decimal Price);