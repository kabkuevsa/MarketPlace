namespace Marketplace.Catalog.DTOs;

public record CreateProductRequest(
    Guid CategoryId,
    string Name,
    string Description,
    decimal Price);