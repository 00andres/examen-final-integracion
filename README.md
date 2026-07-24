# 🎬 Streaming Royalty Platform — Examen Final de Integración

[![.NET Aspire](https://img.shields.io/badge/.NET%20Aspire-13.4.6-512BD4?style=for-the-badge&logo=dotnet)](https://learn.microsoft.com/en-us/dotnet/aspire/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Containerized-4169E1?style=for-the-badge&logo=postgresql)](https://www.postgresql.org/)
[![Azure Service Bus](https://img.shields.io/badge/Azure%20Service%20Bus-Emulator-0078D4?style=for-the-badge&logo=microsoftazure)](https://learn.microsoft.com/en-us/azure/service-bus-messaging/)
[![Architecture](https://img.shields.io/badge/Architecture-Event--Driven%20%26%20Outbox-success?style=for-the-badge)](#-patrones-de-diseño-e-integración)

Sistema distribuido de microservicios para la **gestión de reproducciones de video en streaming y el cálculo automatizado de regalías para creadores**, orquestado con **.NET Aspire**.

---

## 📌 Tabla de Contenido

- [Visión General](#-visión-general)
- [Arquitectura del Sistema](#-arquitectura-del-sistema)
- [Componentes y Microservicios](#-componentes-y-microservicios)
- [Modelo de Datos y Esquema BD](#-modelo-de-datos-y-esquema-bd)
- [Patrones de Diseño e Integración](#-patrones-de-diseño-e-integración)
- [Estructura del Proyecto](#-estructura-del-proyecto)
- [Requisitos Previos](#-requisitos-previos)
- [Guía de Instalación y Ejecución](#-guía-de-instalación-y-ejecución)
- [Paneles de Control e Interfaces](#-paneles-de-control-e-interfaces)
- [API Endpoints y Ejemplos (cURL)](#-api-endpoints-y-ejemplos-curl)
- [Guía de Pruebas (Flujo Exitoso y DLQ)](#-guía-de-pruebas-flujo-exitoso-y-dlq)






## 📱 Visión General

La **Streaming Royalty Platform** resuelve el problema de registrar eventos masivos de reproducciones de video (*views*) garantizando la persistencia transaccional inmediata en el servicio de streaming y la transmisión confiable asíncrona hacia el servicio de contabilidad y liquidación de regalías (*accounting*).

### ⚡ Características Clave
- **Desacoplamiento Total (Database-per-Service):** Los microservicios de Streaming y Contabilidad operan de forma independiente con sus propias bases de datos PostgreSQL en contenedores aislados.
- **Confiabilidad Transaccional (Outbox Pattern):** Registro atómico de reproducciones y eventos en base de datos local para garantizar el despacho de eventos sin pérdidas (*At-Least-Once Delivery*).
- **Tolerancia a Fallos y DLQ (Dead Letter Queue):** Clasificación de errores y desvío automático de mensajes corruptos o con fallas de validación de negocio a la Dead Letter Queue.
- **Resiliencia y Reintentos:** Manejo de reconexiones automáticas a bases de datos y reintentos ante caídas temporales de infraestructura.
- **Orquestación Simplificada con .NET Aspire:** Despliegue automatizado de infraestructura y microservicios con un solo comando.

---

## 📐 Arquitectura del Sistema

```mermaid
graph TD
    subgraph Clients["💻 Clientes / UIs"]
        SC["Streaming Client (Web UI)"]
        AC["Accounting Client (Web UI)"]
    end

    subgraph StreamingMicroservice["🎬 Microservicio Streaming"]
        SAPI["Streaming.Api"]
        SDB[(StreamingDb PostgreSQL)]
        OW["OutboxWorker (Background Service)"]
    end

    subgraph MessagingBroker["📩 Broker de Mensajería (Azure Service Bus Emulator)"]
        ASB["video-finished-queue"]
        DLQ["Dead Letter Queue (DLQ)"]
        ASBUI["ASB Emulator UI"]
    end

    subgraph AccountingMicroservice["💰 Microservicio Accounting"]
        AAPI["Accounting.Api"]
        ADB[(AccountingDb PostgreSQL)]
        REC["RoyaltyEventConsumer (Background Worker)"]
    end

    SC -->|POST /api/views| SAPI
    SAPI -->|Transacción Atómica| SDB
    OW -->|Poll Outbox Table (FIFO)| SDB
    OW -->|Publica Eventos| ASB

    REC -->|Consume Mensajes| ASB
    REC -->|Persiste Regalías| ADB
    REC -->|Mensaje Inválido| DLQ
    AC -->|GET /api/royalties/{creatorId}| AAPI

    ASBUI -.->|Monitorea Colas| ASB
```

---

## 🧱 Componentes y Microservicios

| Componente | Tipo | Tecnología | Descripción |
| :--- | :--- | :--- | :--- |
| **`apphost.cs`** | Orquestador | .NET Aspire AppHost | Punto de entrada que provisiona PostgreSQL, Azure Service Bus Emulator, UIs y microservicios. |
| **`Streaming.Api`** | Microservicio Backend | ASP.NET Core Minimal API | Expone catálogo y registra reproducciones de usuarios (`UserViews`). Implementa el patrón Outbox. |
| **`Streaming.Client`** | Frontend Web | C# / HTML5 / CSS3 | UI interactiva para explorar el catálogo y enviar reproducciones de video. |
| **`Accounting.Api`** | Microservicio Backend | ASP.NET Core Minimal API | Procesa eventos de reproducciones, calcula regalías ($0.05 por vista) y expone consultas por creador. |
| **`Accounting.Client`** | Frontend Web | C# / HTML5 / CSS3 | Dashboard administrativo para la consulta en tiempo real de las regalías acumuladas. |

---

## 🗄️ Modelo de Datos y Esquema BD

### 1. Base de Datos `StreamingDb`
- **`UserViews`**: Almacena el historial de reproducciones.
  - `Id` (Guid, PK), `VideoId` (string), `UserId` (string), `CreatorId` (string), `DurationSeconds` (int), `WatchedAt` (DateTime).
- **`OutboxMessages`**: Tabla de eventos pendientes de publicación.
  - `Id` (Guid, PK), `Type` (string), `Payload` (string JSON), `CreatedAt` (DateTime), `ProcessedDate` (DateTime?).

### 2. Base de Datos `AccountingDb`
- **`CreatorRoyalties`**: Acumula el balance de vistas y regalías por creador.
  - `Id` (Guid, PK), `CreatorId` (string, Unique), `TotalViews` (int), `EstimatedRevenue` (decimal).

---

## 🛠️ Patrones de Diseño e Integración

> [!NOTE]
> **Transactional Outbox Pattern (`Streaming.Api`)**
> 
> Para evitar inconsistencias distribuidas (*Dual Write Problem*), la recepción de un POST `/api/views`:
> 1. Inicia una transacción explícita en EF Core (`CreateExecutionStrategy`).
> 2. Inserta el registro `UserView` y el evento `OutboxMessage` dentro de la **misma transacción local**.
> 3. El worker en segundo plano `OutboxWorker` sondea periódicamente la tabla `OutboxMessages` (orden FIFO), transmite los eventos a Azure Service Bus y actualiza `ProcessedDate`.

> [!IMPORTANT]
> **Manejo de Errores y Dead Letter Queue (DLQ) (`Accounting.Api`)**
> 
> El consumidor `RoyaltyEventConsumer` valida cada mensaje entrante:
> - **Mensaje Válido:** Procesa el cálculo de regalías ($0.05 por vista) y confirma el mensaje con `CompleteMessageAsync`.
> - **Mensaje Inválido (`VideoId` vacío o `DurationSeconds < 0`):** Desvía inmediatamente el mensaje a la **Dead Letter Queue** con la razón `InvalidBusinessData` mediante `DeadLetterMessageAsync`.
> - **Falla Temporal de Base de Datos:** Libera el mensaje con `AbandonMessageAsync` para ser reintentado en el próximo ciclo.

---

## 📁 Estructura del Proyecto

```text
examen-final-integracion/
├── apphost.cs                         # Orquestador .NET Aspire (Single-file AppHost)
├── apphost.run.json                   # Configuración de launch settings y perfiles
├── aspire.config.json                 # Configuración del entorno Aspire
├── StreamingRoyaltyPlatform.slnx       # Solución global .NET
│
├── Streaming.Api/                     # Microservicio de Reproducciones (Producer)
│   ├── Data/                          # StreamingDbContext & Configuración EF Core
│   ├── Models/                        # UserView, OutboxMessage, VideoFinishedEvent
│   ├── Workers/                       # OutboxWorker (Poller & Publisher)
│   └── Program.cs                     # API Endpoints (/api/catalog y /api/views)
│
├── Streaming.Client/                  # Dashboard Web Cliente Streaming
│   └── Program.cs                     # UI embebida & Service Discovery ("http://streaming-api")
│
├── Accounting.Api/                    # Microservicio de Contabilidad (Consumer)
│   ├── Data/                          # AccountingDbContext & Configuración EF Core
│   ├── Models/                        # CreatorRoyalty, VideoFinishedEvent
│   ├── Workers/                       # RoyaltyEventConsumer (ServiceBusProcessor & DLQ)
│   └── Program.cs                     # API Endpoint (/api/royalties/{creatorId})
│
└── Accounting.Client/                 # Dashboard Web Contabilidad
    └── Program.cs                     # UI embebida & Service Discovery ("http://accounting-api")
```

---

## 📋 Requisitos Previos

Asegúrate de contar con las siguientes herramientas instaladas:

1. **[.NET 9.0 o .NET 10.0 SDK](https://dotnet.microsoft.com/download)**
2. **[Docker Desktop](https://www.docker.com/products/docker-desktop/)** (Activo y ejecutándose para contenedores PostgreSQL y Service Bus).
3. **.NET Aspire Workload:**
   ```bash
   dotnet workload install aspire
   ```

---

## 🚀 Guía de Instalación y Ejecución

### 1. Clonar el repositorio y posicionarse en el proyecto:
```bash
cd "examen-final-integracion"
```

### 2. Verificar que Docker esté activo:
Asegúrate de que Docker Desktop esté en estado **Running**.

### 3. Iniciar la aplicación completa con .NET Aspire:
```bash
dotnet run --project apphost.cs
```

.NET Aspire inicializará automáticamente:
- Contenedor PostgreSQL (`StreamingDb` y `AccountingDb`).
- Contenedor Azure Service Bus Emulator + UI (`video-finished-queue`).
- PgAdmin.
- Los microservicios y clientes web.

---

## 🖥️ Paneles de Control e Interfaces

Al iniciar la solución, la URL del **Dashboard de .NET Aspire** se desplegará en la terminal (`https://localhost:17197` o `http://localhost:15132`).

| Interfaz | Descripción |
| :--- | :--- |
| **Aspire Dashboard** | Monitorización de logs en vivo, trazas distribuidas (Telemetry) y estados de salud. |
| **Streaming Client** | UI web para seleccionar videos y registrar reproducciones. |
| **Accounting Client** | UI web para consultar el acumulado de ingresos por creador. |
| **ASB Emulator UI** | Inspección en tiempo real de la cola `video-finished-queue` y mensajes en **DLQ**. |
| **PgAdmin** | Administrador web de bases de datos PostgreSQL. |

---

## 🔌 API Endpoints y Ejemplos (cURL)

### 🎬 `Streaming.Api`

#### 1. Obtener Catálogo de Videos
```bash
curl -X GET "http://localhost:STREAMING_API_PORT/api/catalog"
```

#### 2. Registrar Reproducción de Video
```bash
curl -X POST "http://localhost:STREAMING_API_PORT/api/views" \
     -H "Content-Type: application/json" \
     -d '{
           "videoId": "video-aspire-intro",
           "userId": "user-100",
           "creatorId": "creator-csharp-master",
           "durationSeconds": 180
         }'
```

---

### 💰 `Accounting.Api`

#### Consultar Regalías de un Creador
```bash
curl -X GET "http://localhost:ACCOUNTING_API_PORT/api/royalties/creator-csharp-master"
```

**Respuesta de ejemplo (HTTP 200 OK):**
```json
{
  "id": "e5b8719f-22a4-4d89-9a2f-76a218d6a8b1",
  "creatorId": "creator-csharp-master",
  "totalViews": 1,
  "estimatedRevenue": 0.05
}
```

---

## 🧪 Guía de Pruebas (Flujo Exitoso y DLQ)

> [!TIP]
> **Prueba 1: Flujo Exitoso (End-to-End)**
> 1. Abre la interfaz web **Streaming Client**.
> 2. Selecciona un video del catálogo y presiona **"Registrar Reproducción"**.
> 3. Verifica el código HTTP **201 Created**.
> 4. Espera 5 segundos a que el `OutboxWorker` publique el evento en `video-finished-queue`.
> 5. Abre **Accounting Client** y consulta el Creador `creator-csharp-master`. Comprobarás que se ha incrementado el total de vistas y el saldo acumulado ($0.05 por vista).

> [!WARNING]
> **Prueba 2: Desvío a Dead Letter Queue (DLQ)**
> 1. En **Streaming Client**, activa la opción de prueba de datos inválidos o envía un payload con `VideoId` vacío o `duración negativa` (`-50`).
> 2. El evento será registrado en el Outbox y publicado a la cola `video-finished-queue`.
> 3. El microservicio **Accounting.Api** procesará el mensaje, identificará los datos no válidos y enviará el mensaje a la **Dead Letter Queue (DLQ)**.
> 4. Accede a **ASB Emulator UI** (desde el Dashboard de Aspire) y entra a la sección **Dead Letter Queue** para ver el mensaje rechazado y el motivo `InvalidBusinessData`.

---

## ✒️ Autor y Créditos

Proyecto desarrollado para el **Examen Final de Integración de Sistemas**, aplicando arquitecturas distribuidas orientadas a eventos, resiliencia con el patrón Outbox y gestión de mensajes en DLQ mediante **.NET Aspire**, **PostgreSQL** y **Azure Service Bus**.
