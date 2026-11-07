namespace Marketplace.Catalog.DTOs;

public record UpdateProductRequest(
    string Name,
    string Description,
    decimal Price);