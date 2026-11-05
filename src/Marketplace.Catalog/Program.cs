using Marketplace.Catalog.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

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