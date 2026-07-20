using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Accounting.Api.Data;
using Accounting.Api.Models;

namespace Accounting.Api.Workers
{
    public class RoyaltyEventConsumer : BackgroundService
    {
        private readonly ServiceBusClient _serviceBusClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RoyaltyEventConsumer> _logger;
        private ServiceBusProcessor? _processor;
        private const string QueueName = "video-finished-queue";

        public RoyaltyEventConsumer(
            ServiceBusClient serviceBusClient,
            IServiceProvider serviceProvider,
            ILogger<RoyaltyEventConsumer> logger)
        {
            _serviceBusClient = serviceBusClient;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Iniciando Consumidor de Eventos de Regalías para la cola '{QueueName}'...", QueueName);

            // Configurar el procesador del Service Bus
            var options = new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false, // Controlamos manualmente la finalización y el Dead-Lettering
                MaxConcurrentCalls = 1
            };

            _processor = _serviceBusClient.CreateProcessor(QueueName, options);

            _processor.ProcessMessageAsync += MessageHandler;
            _processor.ProcessErrorAsync += ErrorHandler;

            await _processor.StartProcessingAsync(stoppingToken);
            _logger.LogInformation("Procesador de Service Bus iniciado con éxito.");

            // Mantener el BackgroundService activo hasta que se solicite la cancelación
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task MessageHandler(ProcessMessageEventArgs args)
        {
            var body = args.Message.Body.ToString();
            _logger.LogInformation("Mensaje recibido de Service Bus. MessageId: {MessageId}", args.Message.MessageId);

            VideoFinishedEvent? ev = null;
            try
            {
                ev = JsonSerializer.Deserialize<VideoFinishedEvent>(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al deserializar el cuerpo del mensaje. Enviando a DLQ.");
                await args.DeadLetterMessageAsync(args.Message, "SerializationError", ex.Message);
                return;
            }

            if (ev == null)
            {
                _logger.LogWarning("Mensaje vacío. Enviando a DLQ.");
                await args.DeadLetterMessageAsync(args.Message, "NullPayload", "El contenido del mensaje es nulo.");
                return;
            }

            // 1. REQUERIMIENTO TÉCNICO: Validación de Datos de Negocio para Dead Letter Queue (DLQ)
            // Si el VideoId está vacío o la duración es negativa, se debe mover a DLQ de forma inmediata
            if (string.IsNullOrWhiteSpace(ev.VideoId) || ev.DurationSeconds < 0)
            {
                _logger.LogWarning("Mensaje inválido detectado (VideoId vacío o Duración negativa). Enviando a DLQ. VideoId: '{VideoId}', Duration: {Duration}",
                    ev.VideoId, ev.DurationSeconds);
                
                await args.DeadLetterMessageAsync(
                    args.Message, 
                    deadLetterReason: "InvalidBusinessData", 
                    deadLetterErrorDescription: $"El VideoId no puede estar vacío y la duración ({ev.DurationSeconds}) no puede ser negativa."
                );
                return;
            }

            // 2. Procesar datos válidos y actualizar métricas de regalías del creador en la BD
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();

                // Buscar el registro de regalías del creador o crearlo si no existe
                var creatorRoyalty = await dbContext.CreatorRoyalties
                    .FirstOrDefaultAsync(c => c.CreatorId == ev.CreatorId);

                if (creatorRoyalty == null)
                {
                    creatorRoyalty = new CreatorRoyalty
                    {
                        Id = Guid.NewGuid(),
                        CreatorId = ev.CreatorId,
                        TotalViews = 0,
                        EstimatedRevenue = 0m
                    };
                    dbContext.CreatorRoyalties.Add(creatorRoyalty);
                }

                // Incrementar vistas
                creatorRoyalty.TotalViews++;

                // Calcular regalías: p.ej. $0.05 por cada vista del video
                const decimal RoyaltyPerView = 0.05m;
                creatorRoyalty.EstimatedRevenue += RoyaltyPerView;

                // Guardar en la base de datos
                await dbContext.SaveChangesAsync();

                _logger.LogInformation("Métricas de regalías actualizadas para el creador {CreatorId}. Total Vistas: {TotalViews}, Ingreso Estimado: {EstimatedRevenue:C}",
                    creatorRoyalty.CreatorId, creatorRoyalty.TotalViews, creatorRoyalty.EstimatedRevenue);

                // Confirmar que el mensaje se procesó correctamente y remover de la cola principal
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar el negocio de regalías en base de datos. Abandonando mensaje para posterior reintento.");
                // Si la BD falla temporalmente, abandonamos el mensaje para que vuelva a estar disponible en la cola principal
                await args.AbandonMessageAsync(args.Message);
            }
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Error en el receptor del Service Bus: Origen: {ErrorSource}, Namespace: {FullyQualifiedNamespace}", 
                args.ErrorSource, args.FullyQualifiedNamespace);
            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Deteniendo Consumidor de Eventos de Regalías...");

            if (_processor != null)
            {
                await _processor.StopProcessingAsync(cancellationToken);
                await _processor.DisposeAsync();
            }

            await base.StopAsync(cancellationToken);
        }
    }
}
