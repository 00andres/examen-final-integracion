using System;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Streaming.Api.Data;
using Streaming.Api.Models;

namespace Streaming.Api.Workers
{
    public class OutboxWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxWorker> _logger;
        private readonly ServiceBusClient _serviceBusClient;
        private const string QueueName = "video-finished-queue";

        public OutboxWorker(
            IServiceProvider serviceProvider,
            ILogger<OutboxWorker> logger,
            ServiceBusClient serviceBusClient)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _serviceBusClient = serviceBusClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxWorker iniciado. Escaneando outbox cada 5 segundos.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessagesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error general en el procesamiento de Outbox. Reintentando en 5 segundos.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("OutboxWorker detenido.");
        }

        private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<StreamingDbContext>();

            // Obtener mensajes de outbox no procesados ordenados por fecha de creación (FIFO)
            var messages = await dbContext.OutboxMessages
                .Where(m => m.ProcessedDate == null)
                .OrderBy(m => m.CreatedAt)
                .Take(50) // Lote de procesamiento para optimizar rendimiento
                .ToListAsync(stoppingToken);

            if (messages.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Se encontraron {Count} mensajes en el outbox sin procesar.", messages.Count);

            // Crear el Service Bus Sender
            var sender = _serviceBusClient.CreateSender(QueueName);

            foreach (var message in messages)
            {
                try
                {
                    var sbMessage = new ServiceBusMessage(message.Payload)
                    {
                        MessageId = message.Id.ToString(),
                        ContentType = "application/json",
                        Subject = message.Type
                    };

                    _logger.LogInformation("Enviando evento de outbox '{Type}' ({MessageId}) a la cola '{Queue}'...", 
                        message.Type, message.Id, QueueName);

                    // Publicar al Azure Service Bus
                    await sender.SendMessageAsync(sbMessage, stoppingToken);

                    // Si tiene éxito, marcar como procesado
                    message.ProcessedDate = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    // En caso de error (ej: ASB apagado/inaccesible), se registra la excepción y se sale del bucle actual
                    // para reintentar en el próximo ciclo sin perder el orden original de procesamiento.
                    _logger.LogError(ex, "Falla al transmitir el mensaje de Outbox '{Id}'. Se reintentará en el próximo ciclo.", message.Id);
                    break;
                }
            }

            // Guardar los cambios correspondientes a los mensajes marcados como procesados en este lote
            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }
}
