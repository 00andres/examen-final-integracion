using System;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar Service Discovery de .NET Aspire
builder.Services.AddServiceDiscovery();

// 2. Configurar HttpClient para apuntar al nombre lógico "streaming-api" usando Service Discovery
builder.Services.AddHttpClient("StreamingApi", client =>
{
    client.BaseAddress = new Uri("http://streaming-api");
}).AddServiceDiscovery();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// 3. Servir el Dashboard Interactivo del Cliente
app.MapGet("/", () => Results.Content(@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Client Dashboard - Streaming Service</title>
    <link href='https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;500;600;700&display=swap' rel='stylesheet'>
    <style>
        :root {
            --bg-color: #0d0f14;
            --card-bg: #161b22;
            --primary: #ff007f;
            --primary-glow: rgba(255, 0, 127, 0.4);
            --primary-hover: #e0006c;
            --text-color: #c9d1d9;
            --title-color: #f0f6fc;
            --border-color: #30363d;
            --success: #34d399;
            --error: #f87171;
            --warning: #fbbf24;
            --font-family: 'Outfit', sans-serif;
        }

        body {
            margin: 0;
            padding: 0;
            background-color: var(--bg-color);
            color: var(--text-color);
            font-family: var(--font-family);
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            background-image: 
                radial-gradient(at 0% 0%, rgba(255, 0, 127, 0.08) 0px, transparent 50%),
                radial-gradient(at 100% 100%, rgba(52, 211, 153, 0.05) 0px, transparent 50%);
            box-sizing: border-box;
        }

        .dashboard {
            display: grid;
            grid-template-columns: 1.2fr 1fr;
            gap: 30px;
            width: 100%;
            max-width: 960px;
            padding: 20px;
            box-sizing: border-box;
        }

        @media (max-width: 768px) {
            .dashboard {
                grid-template-columns: 1fr;
            }
        }

        .card {
            background-color: var(--card-bg);
            border-radius: 20px;
            box-shadow: 0 10px 40px rgba(0, 0, 0, 0.6);
            border: 1px solid var(--border-color);
            padding: 30px;
            box-sizing: border-box;
            transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
            display: flex;
            flex-direction: column;
        }

        .card:hover {
            border-color: rgba(255, 0, 127, 0.3);
            box-shadow: 0 15px 45px rgba(255, 0, 127, 0.15);
        }

        h1, h2 {
            color: var(--title-color);
            margin: 0 0 15px 0;
            font-weight: 700;
        }

        h1 {
            font-size: 2.1rem;
            background: linear-gradient(to right, var(--primary), #a855f7);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            text-align: center;
        }

        h2 {
            font-size: 1.4rem;
            border-bottom: 1px solid var(--border-color);
            padding-bottom: 10px;
            color: #a855f7;
        }

        p.subtitle {
            text-align: center;
            font-size: 0.95rem;
            color: #8b949e;
            margin: 0 0 25px 0;
        }

        .form-group {
            margin-bottom: 18px;
        }

        label {
            display: block;
            margin-bottom: 8px;
            font-size: 0.75rem;
            font-weight: 600;
            color: var(--text-color);
            text-transform: uppercase;
            letter-spacing: 1px;
        }

        input, select {
            width: 100%;
            padding: 12px 16px;
            background-color: #0d0f14;
            border: 1px solid var(--border-color);
            border-radius: 10px;
            color: #fff;
            font-size: 0.95rem;
            font-family: var(--font-family);
            box-sizing: border-box;
            transition: all 0.2s ease;
        }

        select {
            appearance: none;
            background-image: url('data:image/svg+xml;charset=UTF-8,%3Csvg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 24 24"" fill=""none"" stroke=""%23ff007f"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""%3E%3Cpolyline points=""6 9 12 15 18 9""%3E%3C/polyline%3E%3C/svg%3E');
            background-repeat: no-repeat;
            background-position: right 16px center;
            background-size: 16px;
            cursor: pointer;
        }

        input:focus, select:focus {
            outline: none;
            border-color: var(--primary);
            box-shadow: 0 0 10px var(--primary-glow);
        }

        .helper-text {
            font-size: 0.75rem;
            color: #8b949e;
            margin-top: 4px;
        }

        .btn-submit {
            width: 100%;
            padding: 14px;
            background: linear-gradient(135deg, var(--primary), #a855f7);
            border: none;
            border-radius: 10px;
            color: #ffffff;
            font-size: 1rem;
            font-weight: 700;
            cursor: pointer;
            transition: all 0.3s ease;
            margin-top: 10px;
        }

        .btn-submit:hover {
            background: linear-gradient(135deg, var(--primary-hover), #9333ea);
            box-shadow: 0 5px 20px rgba(168, 85, 247, 0.4);
            transform: scale(1.02);
        }

        /* Diagnostic Panel Buttons */
        .btn-test {
            width: 100%;
            padding: 12px;
            border: 1px solid var(--border-color);
            background-color: #0d0f14;
            color: var(--text-color);
            border-radius: 10px;
            font-size: 0.9rem;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.2s ease;
            margin-bottom: 12px;
            text-align: left;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .btn-test:hover {
            background-color: #21262d;
            border-color: #8b949e;
            transform: translateX(3px);
        }

        .btn-test-ok { border-left: 4px solid var(--success); }
        .btn-test-neg { border-left: 4px solid var(--warning); }
        .btn-test-empty { border-left: 4px solid var(--error); }

        .result {
            margin-top: 20px;
            padding: 16px;
            border-radius: 10px;
            font-size: 0.9rem;
            display: none;
            line-height: 1.5;
            animation: fadeIn 0.4s cubic-bezier(0.16, 1, 0.3, 1) forwards;
        }

        .result.success {
            background-color: rgba(52, 211, 153, 0.08);
            border: 1px solid var(--success);
            color: var(--success);
        }

        .result.error {
            background-color: rgba(248, 113, 113, 0.08);
            border: 1px solid var(--error);
            color: var(--error);
        }

        .result.warning {
            background-color: rgba(251, 191, 36, 0.08);
            border: 1px solid var(--warning);
            color: var(--warning);
        }

        .dlq-warning {
            margin-top: 10px;
            padding: 8px 12px;
            background-color: rgba(251, 191, 36, 0.08);
            border: 1px solid var(--warning);
            color: var(--warning);
            border-radius: 8px;
            font-size: 0.8rem;
            line-height: 1.4;
        }

        .explainer {
            font-size: 0.85rem;
            color: #8b949e;
            line-height: 1.5;
            margin-bottom: 15px;
        }

        .dot {
            height: 10px;
            width: 10px;
            border-radius: 50%;
            display: inline-block;
        }
        .dot-green { background-color: var(--success); box-shadow: 0 0 8px var(--success); }
        .dot-yellow { background-color: var(--warning); box-shadow: 0 0 8px var(--warning); }
        .dot-red { background-color: var(--error); box-shadow: 0 0 8px var(--error); }

        @keyframes fadeIn {
            from { opacity: 0; transform: translateY(10px); }
            to { opacity: 1; transform: translateY(0); }
        }
    </style>
</head>
<body>
    <div class='dashboard'>
        <!-- PANEL DE REPRODUCCIÓN (CLIENTE 1) -->
        <div class='card'>
            <h1>Streaming Service</h1>
            <p class='subtitle'>Simulador de reproducción finalizada</p>

            <form id='viewForm'>
                <div class='form-group'>
                    <label for='videoSelector'>Catálogo disponible (API 1)</label>
                    <select id='videoSelector'>
                        <option value=''>Cargando catálogo...</option>
                    </select>
                </div>
                
                <div class='form-group'>
                    <label for='videoId'>Video ID</label>
                    <input type='text' id='videoId' placeholder='e.g. video-intro-aspire'>
                </div>
                <div class='form-group'>
                    <label for='userId'>Usuario (Cliente 1)</label>
                    <select id='userId'>
                        <option value='user-cristhian'>Cristhian Conforme</option>
                        <option value='user-andres'>Andres Herrera</option>
                        <option value='user-invitado'>Usuario Invitado</option>
                    </select>
                </div>
                <div class='form-group'>
                    <label for='creatorId'>Creator ID (Para regalías)</label>
                    <input type='text' id='creatorId' placeholder='e.g. creator-csharp-master'>
                </div>
                <div class='form-group'>
                    <label for='duration'>Duración (segundos)</label>
                    <input type='number' id='duration' placeholder='e.g. 300' value='300' required>
                </div>
                <button type='submit' class='btn-submit' id='submitBtn'>Simular Fin de Video</button>
            </form>

            <div id='resultBox' class='result'></div>
        </div>

        <!-- PANEL DE DIAGNÓSTICO Y EXAMEN -->
        <div class='card'>
            <h2>Panel de Pruebas (Examen)</h2>
            <p class='explainer'>
                Usa estos atajos para forzar escenarios de resiliencia y Dead Letter Queue (DLQ). Los botones de error saltan las validaciones HTML del navegador para simular cargas corruptas reales.
            </p>

            <button class='btn-test btn-test-ok' id='btnOk'>
                <span class='dot dot-green'></span>
                <div>
                    <strong>Caso Feliz (Mensaje Válido)</strong>
                    <div class='helper-text'>Envía reproducción válida de 300s. Se suma a las regalías.</div>
                </div>
            </button>

            <button class='btn-test btn-test-neg' id='btnNeg'>
                <span class='dot dot-yellow'></span>
                <div>
                    <strong>Forzar DLQ (Duración Negativa)</strong>
                    <div class='helper-text'>Envía duración -100s. Se guarda en el Outbox y la API 2 lo desvía al DLQ.</div>
                </div>
            </button>

            <button class='btn-test btn-test-empty' id='btnEmpty'>
                <span class='dot dot-red'></span>
                <div>
                    <strong>Forzar DLQ (Video ID Vacío)</strong>
                    <div class='helper-text'>Bypassa validaciones y envía VideoId vacío para disparar el DLQ.</div>
                </div>
            </button>

            <h2 style='margin-top: 20px; color: #ff007f;'>Arquitectura del Examen</h2>
            <div class='explainer' style='font-size: 0.8rem;'>
                <strong>1. Resguardo Local:</strong> Si detienes el Broker en Aspire y haces un envío, la página dirá <em>¡Transacción Completada!</em> porque se guarda a nivel local en PostgreSQL (tabla Outbox).<br><br>
                <strong>2. Recuperación:</strong> Al encender el Broker, el Background Service (Worker) lee el Outbox y los despacha al Broker en orden FIFO.<br><br>
                <strong>3. DLQ:</strong> Si envías un caso amarillo o rojo, en la UI del emulador (<code>asb-ui</code>) verás subir el contador en la sección <strong>Dead-letter</strong>.
            </div>
        </div>
    </div>

    <script>
        // Cargar catálogo desde la API 1
        async function loadCatalog() {
            try {
                const response = await fetch('/api/proxy/catalog');
                if (response.ok) {
                    const catalog = await response.json();
                    const selector = document.getElementById('videoSelector');
                    selector.innerHTML = `<option value=''>-- Selecciona del catálogo o escribe abajo --</option>`;
                    catalog.forEach(video => {
                        const opt = document.createElement('option');
                        opt.value = video.videoId;
                        opt.dataset.creatorId = video.creatorId;
                        opt.textContent = video.title + ' [' + video.genre + ']';
                        selector.appendChild(opt);
                    });
                } else {
                    document.getElementById('videoSelector').innerHTML = `<option value=''>Error al cargar catálogo</option>`;
                }
            } catch (err) {
                document.getElementById('videoSelector').innerHTML = `<option value=''>Error de conexión con API 1</option>`;
            }
        }

        // Auto-completar campos al seleccionar un video del catálogo
        document.getElementById('videoSelector').addEventListener('change', (e) => {
            const selectedOpt = e.target.options[e.target.selectedIndex];
            if (selectedOpt && selectedOpt.value) {
                document.getElementById('videoId').value = selectedOpt.value;
                document.getElementById('creatorId').value = selectedOpt.dataset.creatorId;
            } else {
                document.getElementById('videoId').value = '';
                document.getElementById('creatorId').value = '';
            }
        });

        // Cargar catálogo al inicio
        loadCatalog();

        // Enviar Payload general
        async function sendPayload(payload) {
            const submitBtn = document.getElementById('submitBtn');
            const resultBox = document.getElementById('resultBox');
            
            submitBtn.disabled = true;
            submitBtn.textContent = 'Procesando Transacción...';
            resultBox.style.display = 'none';

            try {
                const response = await fetch('/api/proxy/views', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                let data = {};
                const contentType = response.headers.get('content-type');
                if (contentType && contentType.includes('application/json')) {
                    data = await response.json();
                } else {
                    const text = await response.text();
                    data = { error: text || 'Error interno del servidor proxy.' };
                }
                
                if (response.ok) {
                    const isDlq = !payload.VideoId || payload.DurationSeconds < 0;
                    
                    if (isDlq) {
                        resultBox.className = 'result warning';
                        resultBox.innerHTML = `
                            <strong>⚠️ ¡Transacción Registrada (Caso DLQ)!</strong><br>
                            1. Vista guardada en PostgreSQL.<br>
                            2. Mensaje de prueba inválido encolado en <code>OutboxMessages</code>.<br>
                            <div class='dlq-warning' style='margin-top: 10px; border: none; background: none; padding: 0;'>
                                El Background Service enviará este mensaje al Broker, y la <strong>API 2 lo desviará a la Dead Letter Queue (DLQ)</strong> por reglas de negocio no cumplidas.
                            </div>
                            <small style='display:block;margin-top:8px;'>View ID: \${data.id}</small>
                        `;
                    } else {
                        resultBox.className = 'result success';
                        resultBox.innerHTML = `
                            <strong>✅ ¡Transacción Completada con éxito!</strong><br>
                            1. Registro guardado en la tabla <code>UserViews</code>.<br>
                            2. Evento persistido en el <code>OutboxMessages</code>.<br>
                            <small style='display:block;margin-top:8px;'>View ID: \${data.id}</small>
                        `;
                    }
                } else {
                    resultBox.className = 'result error';
                    resultBox.innerHTML = `<strong>Error de validación (API):</strong> ` + (data.error || 'No se pudo registrar la vista.');
                }
            } catch (err) {
                resultBox.className = 'result error';
                resultBox.innerHTML = `<strong>Error de Red/Proxy:</strong> ` + err.message;
            } finally {
                resultBox.style.display = 'block';
                submitBtn.disabled = false;
                submitBtn.textContent = 'Simular Fin de Video';
            }
        }

        // Envío manual del formulario
        document.getElementById('viewForm').addEventListener('submit', async (e) => {
            e.preventDefault();
            const payload = {
                VideoId: document.getElementById('videoId').value,
                UserId: document.getElementById('userId').value,
                CreatorId: document.getElementById('creatorId').value,
                DurationSeconds: parseInt(document.getElementById('duration').value)
            };
            await sendPayload(payload);
        });

        // Atajo: Caso Feliz
        document.getElementById('btnOk').addEventListener('click', async () => {
            document.getElementById('videoId').value = 'video-aspire-intro';
            document.getElementById('creatorId').value = 'creator-csharp-master';
            document.getElementById('duration').value = '300';
            document.getElementById('videoSelector').value = 'video-aspire-intro';
            document.getElementById('userId').value = 'user-cristhian';
            
            const payload = {
                VideoId: 'video-aspire-intro',
                UserId: 'user-cristhian',
                CreatorId: 'creator-csharp-master',
                DurationSeconds: 300
            };
            await sendPayload(payload);
        });

        // Atajo: DLQ Duración Negativa
        document.getElementById('btnNeg').addEventListener('click', async () => {
            document.getElementById('videoId').value = 'video-outbox-pattern';
            document.getElementById('creatorId').value = 'creator-csharp-master';
            document.getElementById('duration').value = '-100';
            document.getElementById('videoSelector').value = 'video-outbox-pattern';
            document.getElementById('userId').value = 'user-andres';
            
            const payload = {
                VideoId: 'video-outbox-pattern',
                UserId: 'user-andres',
                CreatorId: 'creator-csharp-master',
                DurationSeconds: -100
            };
            await sendPayload(payload);
        });

        // Atajo: DLQ Video ID Vacío (Bypass de validaciones front)
        document.getElementById('btnEmpty').addEventListener('click', async () => {
            document.getElementById('videoId').value = '';
            document.getElementById('creatorId').value = 'creator-cloud-architect';
            document.getElementById('duration').value = '200';
            document.getElementById('videoSelector').value = '';
            document.getElementById('userId').value = 'user-invitado';
            
            const payload = {
                VideoId: '',
                UserId: 'user-invitado',
                CreatorId: 'creator-cloud-architect',
                DurationSeconds: 200
            };
            await sendPayload(payload);
        });
    </script>
</body>
</html>
", "text/html", System.Text.Encoding.UTF8));

// Endpoint proxy para obtener el catálogo de la API 1
app.MapGet("/api/proxy/catalog", async (
    [FromServices] IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("StreamingApi");
    try
    {
        var response = await client.GetAsync("/api/catalog");
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<object>();
            return Results.Ok(result);
        }
        return Results.StatusCode((int)response.StatusCode);
    }
    catch (Exception)
    {
        return Results.StatusCode(500);
    }
});

// Endpoint proxy para registrar visitas hacia la API 1
app.MapPost("/api/proxy/views", async (
    [FromBody] CreateViewRequest request,
    [FromServices] IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("StreamingApi");
    
    try
    {
        var response = await client.PostAsJsonAsync("/api/views", request);
        var responseContent = await response.Content.ReadAsStringAsync();
        
        if (response.IsSuccessStatusCode)
        {
            var result = JsonSerializer.Deserialize<object>(responseContent);
            return Results.Ok(result);
        }
        else
        {
            var err = JsonSerializer.Deserialize<object>(responseContent);
            return Results.BadRequest(err);
        }
    }
    catch (Exception)
    {
        return Results.StatusCode(500);
    }
});

app.Run();

public record CreateViewRequest(string VideoId, string UserId, string CreatorId, int DurationSeconds);
