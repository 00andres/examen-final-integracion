using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Accounting.Api.Data;
using Accounting.Api.Models;
using Accounting.Api.Workers;

var builder = WebApplication.CreateBuilder(args);

// 1. Registrar DbContext de PostgreSQL usando el componente de .NET Aspire
builder.AddNpgsqlDbContext<AccountingDbContext>("AccountingDb");

// 2. Registrar el Cliente de Azure Service Bus usando el componente de .NET Aspire
builder.AddAzureServiceBusClient("messaging");

// 3. Registrar el Background Service (Consumidor)
builder.Services.AddHostedService<RoyaltyEventConsumer>();

// 4. Configurar OpenAPI (Swagger) y endpoints mínimos
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// Inicialización automática de la Base de Datos con reintentos para entornos de desarrollo
try
{
    for (int i = 1; i <= 10; i++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();
            dbContext.Database.EnsureCreated();
            app.Logger.LogInformation("Base de datos de AccountingDb verificada y/o creada correctamente.");
            break;
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning("Intento {Attempt}/10: PostgreSQL no está listo todavía en el arranque. Esperando 2 segundos...", i);
            if (i == 10)
            {
                app.Logger.LogError(ex, "No se pudo conectar a la base de datos de AccountingDb tras 10 intentos.");
            }
            else
            {
                System.Threading.Thread.Sleep(2000);
            }
        }
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Error inesperado durante la inicialización de la base de datos AccountingDb.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// 5. Endpoint GET /api/royalties/{creatorId} - Permite consultar los ingresos acumulados por un creador
app.MapGet("/api/royalties/{creatorId}", async (
    string creatorId,
    [FromServices] AccountingDbContext dbContext,
    [FromServices] ILogger<Program> logger) =>
{
    logger.LogInformation("Consultando regalías para creador: {CreatorId}", creatorId);

    var royalty = await dbContext.CreatorRoyalties
        .FirstOrDefaultAsync(c => c.CreatorId == creatorId);

    if (royalty == null)
    {
        return Results.NotFound(new { Message = $"No se encontraron regalías para el creador con ID '{creatorId}'." });
    }

    return Results.Ok(royalty);
})
.WithName("GetCreatorRoyalty")
.WithOpenApi();

app.Run();
