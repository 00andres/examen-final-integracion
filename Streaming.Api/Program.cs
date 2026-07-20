using System;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Streaming.Api.Data;
using Streaming.Api.Models;
using Streaming.Api.Workers;

var builder = WebApplication.CreateBuilder(args);

// 1. Registrar DbContext de PostgreSQL usando el componente de .NET Aspire
builder.AddNpgsqlDbContext<StreamingDbContext>("StreamingDb");

// 2. Registrar el Cliente de Azure Service Bus usando el componente de .NET Aspire
builder.AddAzureServiceBusClient("messaging");

// 3. Registrar el Background Service (Worker) para procesar el Outbox
builder.Services.AddHostedService<OutboxWorker>();

// 4. Configurar soporte para OpenAPI (Swagger) y Controladores / Endpoints mínimos
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// Inicialización automática de la Base de Datos para entornos de prueba y desarrollo
// Inicialización automática de la Base de Datos con reintentos para entornos de desarrollo
for (int i = 1; i <= 10; i++)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StreamingDbContext>();
        dbContext.Database.EnsureCreated();
        app.Logger.LogInformation("Base de datos de StreamingDb verificada y/o creada correctamente.");
        break;
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("Intento {Attempt}/10: PostgreSQL no está listo todavía en el arranque. Esperando 2 segundos...", i);
        if (i == 10)
        {
            app.Logger.LogError(ex, "No se pudo conectar a la base de datos de StreamingDb tras 10 intentos.");
        }
        else
        {
            System.Threading.Thread.Sleep(2000);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Endpoint GET /api/catalog - Gestiona el catálogo de series/películas disponibles
app.MapGet("/api/catalog", () =>
{
    return Results.Ok(new[]
    {
        new { VideoId = "video-aspire-intro", Title = "Introducción a .NET Aspire", CreatorId = "creator-csharp-master", Genre = "Tecnología" },
        new { VideoId = "video-outbox-pattern", Title = "Patrón Outbox en Microservicios", CreatorId = "creator-csharp-master", Genre = "Diseño de Software" },
        new { VideoId = "video-resilience-dotnet", Title = "Resiliencia Avanzada en .NET 8/10", CreatorId = "creator-architecture", Genre = "Arquitectura" },
        new { VideoId = "video-dlq-servicebus", Title = "Manejo de DLQ en Azure Service Bus", CreatorId = "creator-cloud-architect", Genre = "Cloud" }
    });
})
.WithName("GetCatalog")
.WithOpenApi();

// 5. Endpoint POST /api/views
app.MapPost("/api/views", async (
    [FromBody] CreateViewRequest request,
    [FromServices] StreamingDbContext dbContext,
    [FromServices] ILogger<Program> logger) =>
{
    // Comentamos la validación del VideoId en la API 1 para permitir que pase al Outbox y al Broker,
    // de modo que se pueda probar el desvío a DLQ en la API 2.
    /*
    if (string.IsNullOrWhiteSpace(request.VideoId))
    {
        return Results.BadRequest(new { Error = "El VideoId no puede estar vacío." });
    }
    */

    if (string.IsNullOrWhiteSpace(request.UserId))
    {
        return Results.BadRequest(new { Error = "El UserId no puede estar vacío." });
    }

    if (string.IsNullOrWhiteSpace(request.CreatorId))
    {
        return Results.BadRequest(new { Error = "El CreatorId no puede estar vacío." });
    }

    logger.LogInformation("Recibida solicitud de vista. Video: {VideoId}, Usuario: {UserId}, Creador: {CreatorId}",
        request.VideoId, request.UserId, request.CreatorId);

    try
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        UserView? userView = null;

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync();

            userView = new UserView
            {
                Id = Guid.NewGuid(),
                VideoId = request.VideoId,
                UserId = request.UserId,
                CreatorId = request.CreatorId,
                DurationSeconds = request.DurationSeconds,
                WatchedAt = DateTime.UtcNow
            };

            // Guardar el registro de negocio de la vista
            dbContext.UserViews.Add(userView);

            // Crear el evento de negocio
            var videoFinishedEvent = new VideoFinishedEvent
            {
                ViewId = userView.Id,
                VideoId = userView.VideoId,
                UserId = userView.UserId,
                CreatorId = userView.CreatorId,
                DurationSeconds = userView.DurationSeconds,
                WatchedAt = userView.WatchedAt
            };

            // Serializar el evento
            var payload = JsonSerializer.Serialize(videoFinishedEvent);

            // Guardar el mensaje en el Outbox
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(VideoFinishedEvent),
                Payload = payload,
                CreatedAt = DateTime.UtcNow,
                ProcessedDate = null
            };

            dbContext.OutboxMessages.Add(outboxMessage);

            // Guardar cambios dentro de la transacción
            await dbContext.SaveChangesAsync();

            // Confirmar transacción
            await transaction.CommitAsync();
        });

        logger.LogInformation("Vista guardada y evento encolado en Outbox en transacción para vista {ViewId}.", userView?.Id);

        return Results.Created($"/api/views/{userView?.Id}", userView);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error al procesar la transacción de la vista del usuario con la estrategia de ejecución.");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
})
.WithName("CreateUserView")
.WithOpenApi();

app.Run();

// DTO para la petición
public record CreateViewRequest(string VideoId, string UserId, string CreatorId, int DurationSeconds);
