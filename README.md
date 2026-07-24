# 🎬 Streaming Royalty Platform — Examen Final de Integración

![.NET 9 / Aspire](https://img.shields.io/badge/.NET%20Aspire-13.4.6-512BD4?style=for-the-badge&logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Containerized-4169E1?style=for-the-badge&logo=postgresql)
![Azure Service Bus](https://img.shields.io/badge/Azure%20Service%20Bus-Emulator-0078D4?style=for-the-badge&logo=microsoftazure)
![Architecture](https://img.shields.io/badge/Architecture-Event--Driven%20%26%20Outbox-success?style=for-the-badge)

Sistema distribuido de microservicios para la **gestión de reproducciones de video en streaming y el cálculo automatizado de regalías para creadores**, orquestado mediante **.NET Aspire**.

---

## 📌 Tabla de Contenido

- [Visión General](#-visión-general)
- [Arquitectura del Sistema](#-arquitectura-del-sistema)
- [Componentes y Microservicios](#-componentes-y-microservicios)
- [Patrones de Diseño e Integración](#-patrones-de-diseño-e-integración)
- [Estructura del Proyecto](#-estructura-del-proyecto)
- [Requisitos Previos](#-requisitos-previos)
- [Guía de Instalación y Ejecución](#-guía-de-instalación-y-ejecución)
- [Paneles de Control e Interfaces](#-paneles-de-control-e-interfaces)
- [API Endpoints y Documentación](#-api-endpoints-y-documentación)
- [Guía de Pruebas (Flujo Exitoso y DLQ)](#-guía-de-pruebas-flujo-exitoso-y-dlq)

---

## 📱 Visión General

La **Streaming Royalty Platform** resuelve el reto de registrar eventos de reproducción masivos (*views*) garantizando la persistencia local y la transmisión confiable hacia el sistema de contabilidad y liquidación de regalías (*accounting*).

### Características Clave:
- ⚡ **Desacoplamiento Total:** Los microservicios de Streaming y Contabilidad operan de forma independiente con sus propias bases de datos PostgreSQL (**Database-per-Service**).
- 🔄 **Confiabilidad Transaccional:** Implementación del patrón **Transactional Outbox** para evitar la pérdida de eventos incluso si el broker de mensajería no está disponible al momento del registro.
- 🛡️ **Tolerancia a Fallos y DLQ:** Gestión automatizada de mensajes corruptos o con fallas de negocio enviándolos a una **Dead Letter Queue (DLQ)**.
- 📊 **Dashboards Interactivos Integrados:** UIs interactivas en HTML/CSS embebidas para interactuar con los microservicios sin necesidad de clientes externos.
- 🚀 **Orquestación Simplificada:** Lanzamiento de toda la infraestructura (Bases de Datos, Emulador de Azure Service Bus, UIs y Microservicios) con un solo comando mediante **.NET Aspire**.

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
    OW -->|Poll Outbox Table| SDB
    OW -->|Publica Eventos| ASB

    REC -->|Consume Mensajes| ASB
    REC -->|Persiste Regalías| ADB
    REC -->|Mensaje Inválido| DLQ
    AC -->|GET /api/royalties/{creatorId}| AAPI

    ASBUI -.->|Monitorea| ASB
```

---

## 🧱 Componentes y Microservicios

| Componente | Tipo | Descripción |
| :--- | :--- | :--- |
| **`apphost.cs`** | .NET Aspire AppHost | Punto de entrada y orquestador de contenedores (PostgreSQL, Azure Service Bus Emulator) y servicios de la solución. |
| **`Streaming.Api`** | Microservicio Backend | Expone catálogo y registra reproducciones de usuarios (`UserViews`). Utiliza el patrón Outbox. |
| **`Streaming.Client`** | Frontend Web | Interfaz de usuario interactiva para explorar el catálogo de videos y simular reproducciones. |
| **`Accounting.Api`** | Microservicio Backend | Consume eventos de reproducción, procesa el cálculo de regalías ($0.05 / vista) y expone consultas por creador. |
| **`Accounting.Client`** | Frontend Web | Dashboard administrativo para consultar las regalías acumuladas de cada creador. |

---

## 🛠️ Patrones de Diseño e Integración

### 1. Transactional Outbox Pattern (`Streaming.Api`)
Para evitar fallas de inconsistencia distribuidas (Dual Write Problem), la API de Streaming no publica directamente al Service Bus durante la recepción del HTTP POST. En su lugar:
1. Registra el `UserView` y el `OutboxMessage` dentro de la **misma transacción de base de datos** en `StreamingDb`.
2. El servicio en segundo plano `OutboxWorker` sondea la tabla `OutboxMessages` cada 5 segundos.
3. Transmite los mensajes ordenadamente (**FIFO**) al Azure Service Bus y los marca como procesados (`ProcessedDate`).

### 2. Consumer & Dead Letter Queue (DLQ) (`Accounting.Api`)
El trabajador en segundo plano `RoyaltyEventConsumer` escucha la cola `video-finished-queue`:
- **Procesamiento Exitoso:** Calcula las regalías y actualiza la tabla `CreatorRoyalties` en `AccountingDb`, confirmando el mensaje con `CompleteMessageAsync`.
- **Manejo de DLQ:** Si el mensaje carece de `VideoId` o posee una duración negativa (`DurationSeconds < 0`), se transfiere inmediatamente a la **Dead Letter Queue** mediante `DeadLetterMessageAsync` indicando la razón `InvalidBusinessData`.
- **Reintentos en Fallas de BD:** Si la base de datos no está disponible, el mensaje es abandonado (`AbandonMessageAsync`) para ser reintentado posteriormente.

### 3. Aislamiento de Bases de Datos (Database-per-Service)
Cada microservicio cuenta con su propia base de datos física independiente dentro del contenedor de PostgreSQL:
- `StreamingDb`: Almacena `UserViews` y `OutboxMessages`.
- `AccountingDb`: Almacena `CreatorRoyalties`.

---

## 📁 Estructura del Proyecto

```text
examen-final-integracion/
├── apphost.cs                         # Orquestador .NET Aspire (Single-file AppHost)
├── apphost.run.json                   # Configuración de perfiles de inicio (Launch Settings)
├── aspire.config.json                 # Configuración de Aspire
├── StreamingRoyaltyPlatform.slnx       # Solución global .NET
│
├── Streaming.Api/                     # Microservicio de Reproducciones
│   ├── Data/                          # StreamingDbContext (EF Core)
│   ├── Models/                        # UserView, OutboxMessage, VideoFinishedEvent
│   ├── Workers/                       # OutboxWorker (Poller & Publisher)
│   └── Program.cs                     # Endpoints API /api/catalog y /api/views
│
├── Streaming.Client/                  # UI Web del Cliente de Streaming
│   └── Program.cs                     # Dashboard SPA embebido y Service Discovery
│
├── Accounting.Api/                    # Microservicio de Contabilidad y Regalías
│   ├── Data/                          # AccountingDbContext (EF Core)
│   ├── Models/                        # CreatorRoyalty, VideoFinishedEvent
│   ├── Workers/                       # RoyaltyEventConsumer (ASB Consumer + DLQ)
│   └── Program.cs                     # Endpoint API /api/royalties/{creatorId}
│
└── Accounting.Client/                 # UI Web de Contabilidad
    └── Program.cs                     # Dashboard SPA embebido para consulta de regalías
```

---

## 📋 Requisitos Previos

Asegúrate de contar con los siguientes elementos instalados en tu sistema:

1. **[.NET 9.0 o 10.0 SDK](https://dotnet.microsoft.com/download)**
2. **[Docker Desktop](https://www.docker.com/products/docker-desktop/)** (Debe estar iniciado para ejecutar los contenedores de PostgreSQL y Azure Service Bus Emulator).
3. **.NET Aspire Workload:**
   ```bash
   dotnet workload install aspire
   ```

---

## 🚀 Guía de Instalación y Ejecución

### 1. Clonar el repositorio y navegar a la carpeta:
```bash
cd "examen-final-integracion"
```

### 2. Verificar que Docker esté ejecutándose:
Asegúrate de que el motor de Docker (`Docker Desktop`) esté en estado **Running**.

### 3. Iniciar la aplicación con .NET Aspire:
Ejecuta el AppHost desde la terminal:
```bash
dotnet run --project apphost.cs
```

Al iniciar, .NET Aspire desplegará automáticamente:
- Contenedor de **PostgreSQL** con las bases `StreamingDb` y `AccountingDb`.
- Contenedor del **Azure Service Bus Emulator**.
- Interfaz gráfica del emulador (**ASB Emulator UI**).
- Contenedor de **PgAdmin**.
- Los 4 microservicios/proyectos de la solución.

---

## 🖥️ Paneles de Control e Interfaces

Al ejecutar la aplicación, el **Dashboard de .NET Aspire** se abrirá automáticamente en tu navegador (por defecto en `https://localhost:17197` o `http://localhost:15132`).

Desde el dashboard de Aspire podrás acceder directamente a las UIs de la solución:

| Interfaz | Función |
| :--- | :--- |
| **Aspire Dashboard** | Visualización de logs unificados, métricas de rendimiento y estado de contenedores/servicios. |
| **Streaming Client UI** | Registra vistas de videos e interactúa con `Streaming.Api`. |
| **Accounting Client UI** | Consulta los ingresos y visualizaciones acumuladas por creador en `Accounting.Api`. |
| **ASB Emulator UI** | Inspecciona los mensajes en la cola `video-finished-queue` y analiza mensajes en **Dead Letter Queue (DLQ)**. |
| **PgAdmin** | Administración de las bases de datos PostgreSQL `StreamingDb` y `AccountingDb`. |

---

## 🔌 API Endpoints y Documentación

### 🎬 `Streaming.Api`

- **GET `/api/catalog`**
  - Devuelve el catálogo de videos disponibles.
- **POST `/api/views`**
  - Registra una vista de video y la encola en el Outbox.
  - **Body JSON:**
    ```json
    {
      "videoId": "video-aspire-intro",
      "userId": "user-123",
      "creatorId": "creator-csharp-master",
      "durationSeconds": 300
    }
    ```

### 💰 `Accounting.Api`

- **GET `/api/royalties/{creatorId}`**
  - Devuelve el total acumulado de vistas y regalías estimadas para un creador.
  - **Ejemplo de Respuesta:**
    ```json
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "creatorId": "creator-csharp-master",
      "totalViews": 5,
      "estimatedRevenue": 0.25
    }
    ```

---

## 🧪 Guía de Pruebas (Flujo Exitoso y DLQ)

### Pruebas desde la Interfaz Web (Clientes):

1. **Flujo Exitoso:**
   - Abre la URL de **Streaming Client**.
   - Haz clic en **"Simular Reproducción Exitosa"**.
   - Observa la confirmación del HTTP 201 (Creado).
   - En unos segundos, el `OutboxWorker` procesará el evento y el `RoyaltyEventConsumer` actualizará las regalías.
   - Abre la URL de **Accounting Client** y consulta el Creador `creator-csharp-master`. Verás incrementado el número de vistas y el saldo ($0.05 por vista).

2. **Prueba de Desvío a Dead Letter Queue (DLQ):**
   - Abre la URL de **Streaming Client**.
   - En la sección de pruebas de error, envía un payload con `VideoId` vacío o con `Duración negativa` (ej: `-50`).
   - El mensaje ingresará a la cola a través del Outbox.
   - En **Accounting.Api**, el consumidor detectará el dato inválido y lo desviará a la **Dead Letter Queue**.
   - Abre la interfaz **ASB Emulator UI** desde el Aspire Dashboard y navega a la sección **Dead Letter Queue** de `video-finished-queue` para inspeccionar el mensaje desviado y su motivo de rechazo (`InvalidBusinessData`).

---

## ✒️ Autor y Créditos

Proyecto desarrollado como **Examen Final de Integración de Sistemas**, demostrando patrones de microservicios, resiliencia y mensajería asíncrona con **.NET Aspire**, **PostgreSQL** y **Azure Service Bus**.
