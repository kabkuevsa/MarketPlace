using Marketplace.Catalog.Data;
using Marketplace.Catalog.DTOs;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Marketplace.Catalog.Models;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Catalog service");   // <-- имя сервиса

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddHealthChecks();

    builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));


    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.MapHealthChecks("/health");

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        db.Database.Migrate();
    }


    var productsApi = app.MapGroup("/api/products").WithTags("Products");

    productsApi.MapGet("/", async (CatalogDbContext db) =>
    {
        var products = await db.Products
            .Include(p => p.Category)
            .Select(p => new ProductResponse(p.Id, p.CategoryId, p.Category.Name, p.Name, p.Description, p.Price))
            .ToListAsync();
        return Results.Ok(products);
    });

    productsApi.MapGet("/{id:guid}", async (Guid id, CatalogDbContext db) =>
    {
        var product = await db.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

        return product is null
            ? Results.NotFound()
            : Results.Ok(new ProductResponse(product.Id, product.CategoryId, product.Category.Name,
                product.Name, product.Description, product.Price));
    });

    productsApi.MapPost("/", async (CreateProductRequest request, CatalogDbContext db) =>
    {
        var product = new Product
        {
            CategoryId = request.CategoryId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return Results.Created($"/api/products/{product.Id}", product.Id);
    });

    productsApi.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request, CatalogDbContext db) =>
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return Results.NotFound();

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        await db.SaveChangesAsync();
        return Results.NoContent();
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Catalog service terminated unexpectedly");   // <-- имя сервиса
}
finally
{
    Log.CloseAndFlush();
}