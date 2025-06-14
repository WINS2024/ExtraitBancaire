using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using ExtraitBancaire.Common;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using ExtraitBancaire.Common.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Use CORS middleware
app.UseCors("AllowAll");

// Add logging
app.Logger.LogInformation("Application starting up...");
app.Logger.LogInformation($"Environment: {app.Environment.EnvironmentName}");
app.Logger.LogInformation($"Content root path: {app.Environment.ContentRootPath}");

// Test endpoint
app.MapGet("/test", () => 
{
    app.Logger.LogInformation("Test endpoint called");
    return "API is running!";
})
.WithName("Test")
.WithOpenApi();

const string API_KEY = "llx-SHIS0Rjodz7yEdOBkjwDkOSXdiZ1VtT5msR9XLzwnvvGOt8f"; // TODO: Move to appsettings.json
const string EXTRACTION_AGENT = "0a4216cf-d883-448a-a9bf-2df431244f6b"; // TODO: Move to appsettings.json
const string API_BASE_URL = "https://api.cloud.eu.llamaindex.ai/api/v1";

app.MapPost("/extractpdf", async (HttpRequest request) =>
{
    string base64Pdf;
    using (var reader = new StreamReader(request.Body, Encoding.UTF8))
    {
        base64Pdf = await reader.ReadToEndAsync();
    }

    if (string.IsNullOrEmpty(base64Pdf))
    {
        return Results.BadRequest("Le contenu du PDF est vide.");
    }

    try
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");
        httpClient.DefaultRequestHeaders.Add("accept", "application/json");

        byte[] fileBytes;
        try
        {
            fileBytes = Convert.FromBase64String(base64Pdf);
        }
        catch (FormatException ex)
        {
            Console.WriteLine($"Erreur de formatage Base64 : {ex.Message}");
            return Results.BadRequest("Contenu PDF invalide (chaîne Base64 mal formée).");
        }

        // Étape 1 : Upload du fichier PDF
        var uploadContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        uploadContent.Add(fileContent, "upload_file", "document.pdf");

        var uploadResponse = await httpClient.PostAsync(
            $"{API_BASE_URL}/files",
            uploadContent);

        uploadResponse.EnsureSuccessStatusCode(); // Throws if not success
        var uploadResult = await uploadResponse.Content.ReadAsStringAsync();
        var uploadJson = JsonSerializer.Deserialize<JsonNode>(uploadResult);
        string fileId = uploadJson["id"].GetValue<string>();

        // Étape 2 : Créer un job d'extraction
        var jobRequest = new
        {
            extraction_agent_id = EXTRACTION_AGENT,
            file_id = fileId
        };

        var jobContent = new StringContent(
            JsonSerializer.Serialize(jobRequest),
            Encoding.UTF8,
            "application/json");

        var jobResponse = await httpClient.PostAsync(
            $"{API_BASE_URL}/extraction/jobs",
            jobContent);

        jobResponse.EnsureSuccessStatusCode(); // Throws if not success
        var jobResult = await jobResponse.Content.ReadAsStringAsync();
        var jobJson = JsonSerializer.Deserialize<JsonNode>(jobResult);
        string jobId = jobJson["id"].GetValue<string>();

        // Étape 3 : Polling du statut
        string status;
        do
        {
            await Task.Delay(500); // Polling interval
            var statusResponse = await httpClient.GetAsync($"{API_BASE_URL}/extraction/jobs/{jobId}");
            statusResponse.EnsureSuccessStatusCode();
            var statusResult = await statusResponse.Content.ReadAsStringAsync();
            var statusJson = JsonSerializer.Deserialize<JsonNode>(statusResult);
            status = statusJson["status"].GetValue<string>();

            if (status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            if (status.Equals("ERROR", StringComparison.OrdinalIgnoreCase) || status.Equals("FAILED", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Extraction échouée avec le statut : {status}");
            }
        } while (true);

        // Étape 4 : Récupération du résultat
        var resultResponse = await httpClient.GetAsync($"{API_BASE_URL}/extraction/jobs/{jobId}/result");
        resultResponse.EnsureSuccessStatusCode();
        var resultContent = await resultResponse.Content.ReadAsStringAsync();
        
        // Désérialiser le contenu brut en notre modèle commun ExtractionResult
        var extractionResult = JsonSerializer.Deserialize<ExtractionResult>(resultContent);

        return Results.Ok(extractionResult);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
})
.WithName("ExtractPdf")
.DisableAntiforgery()
.WithMetadata(new RequestSizeLimitAttribute(100 * 1024 * 1024)); // Définir la limite à 100 Mo

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
