namespace Marketplace.Web.Models;

public record Product(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string Description,
    decimal Price);

public record Category(
    Guid Id,
    string Name);