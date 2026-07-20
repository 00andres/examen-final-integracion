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

// 2. Configurar HttpClient para apuntar al microservicio "accounting-api"
builder.Services.AddHttpClient("AccountingApi", client =>
{
    client.BaseAddress = new Uri("http://accounting-api");
}).AddServiceDiscovery();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// 3. Servir el Dashboard de Métricas de Regalías del Cliente
app.MapGet("/", () => Results.Content(@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Creator Royalties - Accounting Service</title>
    <link href='https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;500;600;700&display=swap' rel='stylesheet'>
    <style>
        :root {
            --bg-color: #0b0f19;
            --card-bg: #111827;
            --primary: #10b981;
            --primary-glow: rgba(16, 185, 129, 0.3);
            --primary-hover: #059669;
            --text-color: #d1d5db;
            --title-color: #f3f4f6;
            --border-color: #1f2937;
            --success: #34d399;
            --error: #f87171;
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
                radial-gradient(at 0% 100%, rgba(16, 185, 129, 0.08) 0px, transparent 50%),
                radial-gradient(at 100% 0%, rgba(59, 130, 246, 0.05) 0px, transparent 50%);
        }

        .container {
            width: 100%;
            max-width: 500px;
            background-color: var(--card-bg);
            border-radius: 24px;
            box-shadow: 0 20px 50px rgba(0, 0, 0, 0.5);
            border: 1px solid var(--border-color);
            padding: 40px;
            box-sizing: border-box;
            transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
        }

        .container:hover {
            transform: translateY(-5px);
            border-color: rgba(16, 185, 129, 0.3);
            box-shadow: 0 20px 45px rgba(16, 185, 129, 0.1);
        }

        h1 {
            color: var(--title-color);
            font-size: 2.1rem;
            font-weight: 700;
            margin: 0 0 8px 0;
            text-align: center;
            background: linear-gradient(to right, var(--primary), #3b82f6);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }

        p.subtitle {
            text-align: center;
            font-size: 0.95rem;
            color: #9ca3af;
            margin: 0 0 35px 0;
        }

        .search-box {
            display: flex;
            gap: 12px;
            margin-bottom: 30px;
        }

        input, select {
            flex: 1;
            min-width: 0;
            width: 100%;
            padding: 14px 18px;
            background-color: #0b0f19;
            border: 1px solid var(--border-color);
            border-radius: 12px;
            color: #fff;
            font-size: 1rem;
            font-family: var(--font-family);
            transition: all 0.2s ease;
            box-sizing: border-box;
        }

        input:focus, select:focus {
            outline: none;
            border-color: var(--primary);
            box-shadow: 0 0 12px var(--primary-glow);
        }

        button {
            padding: 14px 24px;
            background-color: var(--primary);
            border: none;
            border-radius: 12px;
            color: #0b0f19;
            font-size: 1rem;
            font-weight: 700;
            cursor: pointer;
            transition: all 0.3s ease;
        }

        button:hover {
            background-color: var(--primary-hover);
            color: #ffffff;
            box-shadow: 0 4px 15px rgba(16, 185, 129, 0.4);
        }

        .stats-card {
            background-color: #0b0f19;
            border: 1px solid var(--border-color);
            border-radius: 16px;
            padding: 24px;
            display: none;
            animation: fadeIn 0.5s cubic-bezier(0.16, 1, 0.3, 1) forwards;
        }

        .stat-row {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 12px 0;
            border-bottom: 1px solid rgba(255, 255, 255, 0.05);
        }

        .stat-row:last-child {
            border-bottom: none;
            padding-bottom: 0;
        }

        .stat-label {
            font-size: 0.9rem;
            color: #9ca3af;
        }

        .stat-value {
            font-size: 1.1rem;
            font-weight: 600;
            color: #fff;
        }

        .stat-value.highlight {
            color: var(--primary);
            font-size: 1.3rem;
            font-weight: 700;
        }

        .error-message {
            color: var(--error);
            background-color: rgba(248, 113, 113, 0.1);
            border: 1px solid var(--error);
            border-radius: 12px;
            padding: 16px;
            display: none;
            font-size: 0.95rem;
            text-align: center;
            animation: fadeIn 0.4s ease forwards;
        }

        @keyframes fadeIn {
            from { opacity: 0; transform: translateY(10px); }
            to { opacity: 1; transform: translateY(0); }
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>Métricas de Regalías</h1>
        <p class='subtitle'>Consulta de regalías de creadores en tiempo real</p>

        <div class='search-box'>
            <select id='creatorId'>
                <option value='creator-csharp-master'>C# Master</option>
                <option value='creator-architecture'>Architecture Expert</option>
                <option value='creator-cloud-architect'>Cloud Architect</option>
                <option value='creator-unknown'>Creador Inexistente (Prueba de Error)</option>
            </select>
            <button id='queryBtn'>Consultar</button>
        </div>

        <div id='statsCard' class='stats-card'>
            <div class='stat-row'>
                <span class='stat-label'>Creador ID</span>
                <span id='resCreatorId' class='stat-value'>-</span>
            </div>
            <div class='stat-row'>
                <span class='stat-label'>Total Visualizaciones</span>
                <span id='resTotalViews' class='stat-value'>-</span>
            </div>
            <div class='stat-row'>
                <span class='stat-label'>Regalías Estimadas</span>
                <span id='resRevenue' class='stat-value highlight'>-</span>
            </div>
        </div>

        <div id='errorBox' class='error-message'></div>
    </div>

    <script>
        document.getElementById('queryBtn').addEventListener('submit', (e) => e.preventDefault()); // prevent any form
        
        async function fetchRoyalties() {
            const creatorId = document.getElementById('creatorId').value.trim();
            const queryBtn = document.getElementById('queryBtn');
            const statsCard = document.getElementById('statsCard');
            const errorBox = document.getElementById('errorBox');

            if (!creatorId) return;

            queryBtn.disabled = true;
            queryBtn.textContent = 'Buscando...';
            statsCard.style.display = 'none';
            errorBox.style.display = 'none';

            try {
                const response = await fetch('/api/proxy/royalties/' + encodeURIComponent(creatorId));
                
                if (response.ok) {
                    const data = await response.json();
                    document.getElementById('resCreatorId').textContent = data.creatorId;
                    document.getElementById('resTotalViews').textContent = data.totalViews.toLocaleString();
                    
                    // Formatear como moneda USD
                    const formattedRevenue = new Intl.NumberFormat('en-US', {
                        style: 'currency',
                        currency: 'USD'
                    }).format(data.estimatedRevenue);
                    
                    document.getElementById('resRevenue').textContent = formattedRevenue;
                    
                    statsCard.style.display = 'block';
                } else {
                    errorBox.textContent = 'No se encontraron registros de regalías para este creador.';
                    errorBox.style.display = 'block';
                }
            } catch (err) {
                errorBox.textContent = 'Error al conectar con la API: ' + err.message;
                errorBox.style.display = 'block';
            } finally {
                queryBtn.disabled = false;
                queryBtn.textContent = 'Consultar';
            }
        }

        document.getElementById('queryBtn').addEventListener('click', fetchRoyalties);
        document.getElementById('creatorId').addEventListener('change', fetchRoyalties);
        // Cargar datos por defecto al iniciar
        fetchRoyalties();
    </script>
</body>
</html>
", "text/html", System.Text.Encoding.UTF8));

// Endpoint que actúa como proxy hacia el microservicio real de contabilidad
app.MapGet("/api/proxy/royalties/{creatorId}", async (
    string creatorId,
    [FromServices] IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("AccountingApi");
    
    try
    {
        var response = await client.GetAsync($"/api/royalties/{creatorId}");
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<object>();
            return Results.Ok(result);
        }
        else
        {
            return Results.NotFound();
        }
    }
    catch (Exception)
    {
        return Results.StatusCode(500);
    }
});

app.Run();
