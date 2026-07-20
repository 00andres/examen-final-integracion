#:sdk Aspire.AppHost.Sdk@13.4.6+87fe259e4fc244c599019a7b1304c85a1488f248
#:package Aspire.Hosting.PostgreSQL@13.4.6
#:package Aspire.Hosting.Azure.ServiceBus@13.4.6
#:package AJP.Aspire.Hosting.AsbEmulatorUi@1.0.24
#:project Streaming.Api/Streaming.Api.csproj
#:project Accounting.Api/Accounting.Api.csproj
#:project Streaming.Client/Streaming.Client.csproj
#:project Accounting.Client/Accounting.Client.csproj

using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// 1. Base de Datos - Contenedor PostgreSQL
var postgres = builder.AddPostgres("postgres")
                      .WithPgAdmin();

// Base de datos independiente para cada Microservicio (Principio de Aislamiento)
var streamingDb = postgres.AddDatabase("StreamingDb");
var accountingDb = postgres.AddDatabase("AccountingDb");

// 2. Broker de Mensajería - Emulador de Azure Service Bus con la cola requerida
var messaging = builder.AddAzureServiceBus("messaging")
                       .RunAsEmulator(c => c.WithLifetime(ContainerLifetime.Persistent));

var videoFinishedQueue = messaging.AddServiceBusQueue("video-finished-queue");

// 2.1 Agregar la interfaz gráfica del emulador (UI)
builder.AddAsbEmulatorUi("asb-ui", messaging);

// 3. Proyectos de la API
var streamingApi = builder.AddProject<Projects.Streaming_Api>("streaming-api")
                          .WithReference(streamingDb)
                          .WithReference(messaging)
                          .WithReference(videoFinishedQueue);

var accountingApi = builder.AddProject<Projects.Accounting_Api>("accounting-api")
                           .WithReference(accountingDb)
                           .WithReference(messaging)
                           .WithReference(videoFinishedQueue);

// 4. Clientes (Mockeados o Frontend) con Service Discovery para las APIs
builder.AddProject<Projects.Streaming_Client>("streaming-client")
       .WithReference(streamingApi);

builder.AddProject<Projects.Accounting_Client>("accounting-client")
       .WithReference(accountingApi);

builder.Build().Run();