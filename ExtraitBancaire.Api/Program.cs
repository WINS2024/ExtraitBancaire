using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using ExtraitBancaire.Common;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using ExtraitBancaire.Common.Models;
using ExtraitBancaire.Api.Services;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;
using PdfSharp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ExtraitBancaire API", Version = "v1" });
});

// Configuration JSON
builder.Services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
    options.JsonSerializerOptions.WriteIndented = true;
});

// Register MariaDbService with connection string from configuration
builder.Services.AddScoped<MariaDbService>(sp => 
    new MariaDbService(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .WithOrigins(
                "http://localhost",
                "http://localhost:80",
                "http://localhost:8080",
                "https://localhost:7212",
                "https://localhost:7213",
                "https://localhost:7214",
                "https://localhost:7215",
                "http://localhost:5026"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Content-Type", "Content-Disposition"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ExtraitBancaire API V1");
    });
}

// Use CORS middleware - IMPORTANT: doit être avant les endpoints
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

const string API_KEY = "llx-CVJC7vca0h6U3B4FIBHUrHfjia8pHQoGJoCEV3RzJwTpSYSy"; // Nouvelle API key
const string EXTRACTION_AGENT = "014816c6-388f-4e75-8e6d-d939c86aef7f"; // Nouvel agent
const string API_BASE_URL = "https://api.cloud.eu.llamaindex.ai/api/v1";

app.MapPost("/extractpdf", async ([FromBody] PdfExtractionRequest request) =>
{
    Console.WriteLine($"Requête d'extraction PDF reçue. Fichier: {request.FileName}, Longueur Base64: {request.Base64Content?.Length}");

    if (string.IsNullOrEmpty(request.Base64Content))
    {
        Console.WriteLine("Le contenu du PDF est vide.");
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
            fileBytes = Convert.FromBase64String(request.Base64Content);
            Console.WriteLine($"Bytes du PDF : {fileBytes.Length}");
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
        uploadContent.Add(fileContent, "upload_file", request.FileName);

        Console.WriteLine($"Début upload PDF vers LLama Cloud: {request.FileName}...");
        var uploadResponse = await httpClient.PostAsync(
            $"{API_BASE_URL}/files",
            uploadContent);

        var uploadResult = await uploadResponse.Content.ReadAsStringAsync();
        Console.WriteLine("Réponse upload : " + uploadResult);

        if (!uploadResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("Erreur upload : " + uploadResult);
            return Results.Problem("Erreur lors de l'upload du PDF : " + uploadResult);
        }

        var uploadJson = JsonSerializer.Deserialize<JsonNode>(uploadResult);
        string fileId = uploadJson["id"].GetValue<string>();

        // Étape 2 : Créer un job d'extraction
        var jobRequest = new
        {
            extraction_agent_id = EXTRACTION_AGENT,
            file_id = fileId
        };

        Console.WriteLine("Création du job d'extraction...");
        var jobContent = new StringContent(
            JsonSerializer.Serialize(jobRequest),
            Encoding.UTF8,
            "application/json");

        var jobResponse = await httpClient.PostAsync(
            $"{API_BASE_URL}/extraction/jobs",
            jobContent);

        Console.WriteLine($"Réponse création job: {await jobResponse.Content.ReadAsStringAsync()}");
        jobResponse.EnsureSuccessStatusCode(); // Throws if not success
        var jobResult = await jobResponse.Content.ReadAsStringAsync();
        var jobJson = JsonSerializer.Deserialize<JsonNode>(jobResult);
        string jobId = jobJson["id"].GetValue<string>();
        Console.WriteLine($"Job ID créé: {jobId}");

        // Étape 3 : Polling du statut
        string status;
        int attempts = 0;
        const int MAX_ATTEMPTS = 120; // 10 minutes maximum (120 * 5 secondes)
        const int POLLING_INTERVAL = 5000; // 5 secondes entre chaque tentative
        const int MAX_WAIT_TIME = 600000; // 10 minutes en millisecondes
        var startTime = DateTime.UtcNow;

        do
        {
            attempts++;
            var elapsedTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            Console.WriteLine($"Tentative {attempts} - Temps écoulé: {elapsedTime/1000:F0} secondes");
            
            await Task.Delay(POLLING_INTERVAL);
            var statusResponse = await httpClient.GetAsync($"{API_BASE_URL}/extraction/jobs/{jobId}");
            statusResponse.EnsureSuccessStatusCode();
            var statusResult = await statusResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"Statut reçu: {statusResult}");
            
            var statusJson = JsonSerializer.Deserialize<JsonNode>(statusResult);
            status = statusJson["status"]?.GetValue<string>() ?? "UNKNOWN";
            Console.WriteLine($"Statut actuel: {status}");

            if (status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Extraction réussie !");
                break;
            }
            if (status.Equals("ERROR", StringComparison.OrdinalIgnoreCase) || 
                status.Equals("FAILED", StringComparison.OrdinalIgnoreCase))
            {
                var error = statusJson["error"]?.GetValue<string>() ?? "Erreur inconnue";
                throw new Exception($"Extraction échouée avec le statut : {status}, Erreur : {error}");
            }
            if (elapsedTime >= MAX_WAIT_TIME)
            {
                throw new Exception($"Timeout: L'extraction prend trop de temps (plus de 10 minutes)");
            }
        } while (true);

        // Étape 4 : Récupération du résultat
        Console.WriteLine("Récupération du résultat...");
        var resultResponse = await httpClient.GetAsync($"{API_BASE_URL}/extraction/jobs/{jobId}/result");
        resultResponse.EnsureSuccessStatusCode();
        var resultContent = await resultResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Résultat reçu: {resultContent}");
        
        // Désérialiser le contenu brut en notre modèle commun ExtractionResult
        var extractionResult = JsonSerializer.Deserialize<ExtractionResult>(resultContent);
        Console.WriteLine("Résultat désérialisé avec succès");

        // Si nous atteignons ce point, l'extraction LlamaIndex a été un succès.
        // Nous définissons explicitement le statut pour le client Blazor.
        extractionResult.Status = "SUCCESS"; 

        // Stocker le dernier résultat d'extraction réussi
        ApiState.LastExtractionResult = extractionResult;

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

app.MapGet("/extractpdf/result/{jobId}", async (string jobId) =>
{
    try
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");
        httpClient.DefaultRequestHeaders.Add("accept", "application/json");

        // Récupérer le résultat du job
        var resultResponse = await httpClient.GetAsync($"{API_BASE_URL}/extraction/jobs/{jobId}/result");
        resultResponse.EnsureSuccessStatusCode();

        var resultContent = await resultResponse.Content.ReadAsStringAsync();
        
        // Log du contenu JSON reçu
        Console.WriteLine("Contenu JSON reçu de l'API Llama :");
        Console.WriteLine(resultContent);

        // S'assurer que le contenu est un objet JSON valide
        var jsonDocument = JsonDocument.Parse(resultContent);
        var root = jsonDocument.RootElement;

        // Retourner le contenu JSON directement
        return Results.Ok(root);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur lors de la récupération du résultat : {ex.Message}");
        return Results.Problem($"Erreur lors de la récupération du résultat : {ex.Message}");
    }
})
.WithName("GetExtractionResult")
.WithOpenApi();

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

// Endpoint pour obtenir le dernier résultat d'extraction
app.MapGet("/extractpdf/result/latest", () =>
{
    if (ApiState.LastExtractionResult == null)
    {
        return Results.NotFound("Aucun résultat d'extraction disponible.");
    }
    return Results.Ok(ApiState.LastExtractionResult);
})
.WithName("GetLatestExtractionResult")
.WithOpenApi();

// Add a new API endpoint to save monthly data
app.MapPost("/api/monthlydata/save", async ([FromBody] MonthlyData monthlyData, MariaDbService dbService) =>
{
    if (monthlyData == null)
    {
        return Results.BadRequest("Les données mensuelles à sauvegarder sont vides.");
    }

    try
    {
        await dbService.SaveMonthlyDataAsync(monthlyData);
        return Results.Ok();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Erreur lors de la sauvegarde des données mensuelles.");
        return Results.Problem($"Erreur lors de la sauvegarde des données mensuelles : {ex.Message}");
    }
})
.WithName("SaveMonthlyData")
.WithOpenApi();

// Add a new API endpoint to load all monthly data
app.MapGet("/api/monthlydata", async (MariaDbService dbService) =>
{
    try
    {
        var monthlyData = await dbService.LoadAllMonthlyDataAsync();
        return Results.Ok(monthlyData);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Erreur lors du chargement de toutes les données mensuelles.");
        return Results.Problem($"Erreur lors du chargement de toutes les données mensuelles : {ex.Message}");
    }
})
.WithName("LoadAllMonthlyData")
.WithOpenApi();

// Add a new API endpoint to test database connection
app.MapGet("/api/test-db", async (MariaDbService dbService) =>
{
    try
    {
        using var conn = dbService.GetConnection();
        await conn.OpenAsync();
        return Results.Ok("Connexion à la base de données réussie");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Erreur lors de la connexion à la base de données.");
        return Results.Problem($"Erreur lors de la connexion à la base de données : {ex.Message}");
    }
})
.WithName("TestDatabaseConnection")
.WithOpenApi();

// Add a new API endpoint to check tables
app.MapGet("/api/check-tables", async (MariaDbService dbService) =>
{
    try
    {
        using var conn = dbService.GetConnection();
        await conn.OpenAsync();
        
        var tables = new List<string>();
        using (var cmd = new MySqlConnector.MySqlCommand("SHOW TABLES;", conn))
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }
        
        return Results.Ok(new { 
            Tables = tables,
            HasExtraitsMensuels = tables.Contains("extraits_mensuels"),
            HasOperationsMensuelles = tables.Contains("operations_mensuelles")
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Erreur lors de la vérification des tables.");
        return Results.Problem($"Erreur lors de la vérification des tables : {ex.Message}");
    }
})
.WithName("CheckTables")
.WithOpenApi();

// Add a new API endpoint to create tables
app.MapPost("/api/create-tables", async (MariaDbService dbService) =>
{
    try
    {
        using var conn = dbService.GetConnection();
        await conn.OpenAsync();
        
        // Créer la table extraits_mensuels
        string createExtraitsMensuelsSql = @"
            CREATE TABLE IF NOT EXISTS `extraits_mensuels` (
                `annee` INT NOT NULL,
                `mois` INT NOT NULL,
                `nom_mois` VARCHAR(50) NOT NULL,
                `solde_initial` DECIMAL(18, 2) NOT NULL,
                `solde_final` DECIMAL(18, 2) NOT NULL,
                `total_debit` DECIMAL(18, 2) NOT NULL,
                `total_credit` DECIMAL(18, 2) NOT NULL,
                `mouvement_net` DECIMAL(18, 2) NOT NULL,
                `nombre_operations` INT NOT NULL,
                PRIMARY KEY (`annee`, `mois`)
            );";
            
        // Créer la table operations_mensuelles
        string createOperationsMensuellesSql = @"
            CREATE TABLE IF NOT EXISTS `operations_mensuelles` (
                `id` INT AUTO_INCREMENT PRIMARY KEY,
                `annee_extrait` INT NOT NULL,
                `mois_extrait` INT NOT NULL,
                `date_operation` DATETIME NOT NULL,
                `libelle` VARCHAR(255) NOT NULL,
                `debit` DECIMAL(18, 2) NULL,
                `credit` DECIMAL(18, 2) NULL,
                `est_ajoute_manuellement` BOOLEAN NOT NULL,
                FOREIGN KEY (`annee_extrait`, `mois_extrait`) REFERENCES `extraits_mensuels`(`annee`, `mois`) ON DELETE CASCADE
            );";
            
        using (var cmd = new MySqlConnector.MySqlCommand(createExtraitsMensuelsSql, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }
        
        using (var cmd = new MySqlConnector.MySqlCommand(createOperationsMensuellesSql, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }
        
        return Results.Ok("Tables créées avec succès");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Erreur lors de la création des tables.");
        return Results.Problem($"Erreur lors de la création des tables : {ex.Message}");
    }
})
.WithName("CreateTables")
.WithOpenApi();

// Endpoint pour la division du PDF
app.MapPost("/splitpdf", async ([FromBody] PdfSplitRequest request) =>
{
    if (string.IsNullOrEmpty(request.Base64Content) || string.IsNullOrEmpty(request.PagesToSplit))
    {
        Console.WriteLine("Requête de division PDF invalide: contenu Base64 ou pages à diviser manquants.");
        return Results.BadRequest("Contenu PDF ou pages à diviser manquants.");
    }

    Console.WriteLine($"Requête de division PDF reçue. Pages à diviser: {request.PagesToSplit}");

    byte[] pdfBytes;
    try
    {
        pdfBytes = Convert.FromBase64String(request.Base64Content);
    }
    catch (FormatException ex)
    {
        Console.WriteLine($"Erreur de formatage Base64 lors de la division du PDF: {ex.Message}");
        return Results.BadRequest("Contenu PDF invalide (chaîne Base64 mal formée).");
    }

    try
    {
        int totalPages;
        using (MemoryStream tempStream = new MemoryStream(pdfBytes))
        {
            using (PdfDocument tempInputDocument = PdfReader.Open(tempStream, PdfDocumentOpenMode.Import))
            {
                totalPages = tempInputDocument.PageCount;
                Console.WriteLine($"Document PDF temporairement ouvert pour compter les pages. Nombre total de pages: {totalPages}");
            }
        }

        List<string> splitFileBase64s = new List<string>();
        List<string> splitFileNames = new List<string>();
        int fileIndex = 1;

        // Nouvelle logique : chaque groupe séparé par '/' devient un fichier
        var pageGroups = request.PagesToSplit.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pageGroups == null || !pageGroups.Any())
        {
            Console.WriteLine($"Aucun groupe de pages valide n'a été parsé pour: {request.PagesToSplit}");
            return Results.BadRequest("Format des pages à diviser invalide.");
        }

        foreach (var group in pageGroups)
        {
            var pageList = ParsePageList(group, totalPages);
            if (pageList.Count == 0)
                continue;

            using (MemoryStream currentPdfStream = new MemoryStream(pdfBytes))
            using (PdfDocument inputDocument = PdfReader.Open(currentPdfStream, PdfDocumentOpenMode.Import))
            using (PdfDocument outputDocument = new PdfDocument())
            {
                foreach (var pageNum in pageList)
                {
                    if (pageNum > 0 && pageNum <= inputDocument.PageCount)
                    {
                        outputDocument.AddPage(inputDocument.Pages[pageNum - 1]);
                    }
                }

                if (outputDocument.PageCount == 0)
                    continue;

                using (MemoryStream outputStream = new MemoryStream())
                {
                    outputDocument.Save(outputStream);
                    string base64File = Convert.ToBase64String(outputStream.ToArray());
                    splitFileBase64s.Add(base64File);
                    string fileName = $"split_part_{fileIndex++}.pdf";
                    splitFileNames.Add(fileName);
                }
            }
        }

        Console.WriteLine($"Division terminée. Nombre de fichiers créés: {splitFileBase64s.Count}");

        if (!splitFileBase64s.Any())
        {
            return Results.Problem("Aucun fichier n'a pu être divisé.");
        }

        return Results.Ok(new { message = "PDF divisé avec succès.", splitFiles = splitFileBase64s, splitFileNames = splitFileNames });
    }
    catch (PdfSharpException ex)
    {
        Console.WriteLine($"Erreur PdfSharp lors de la division du PDF: {ex.Message}");
        return Results.Problem($"Erreur lors de la division du PDF (PdfSharp): {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erreur inattendue lors de la division du PDF: {ex.Message}");
        return Results.Problem($"Erreur inattendue lors de la division du PDF: {ex.Message}");
    }
})
.WithName("SplitPdf")
.WithOpenApi()
.DisableAntiforgery()
.WithMetadata(new RequestSizeLimitAttribute(100 * 1024 * 1024));

// Nouvelle fonction utilitaire pour parser une liste de pages séparées par '.' ou ','
static List<int> ParsePageList(string group, int totalPages)
{
    var pages = new List<int>();
    // Accepte '.' ou ',' comme séparateur
    var parts = group.Split(new[] { '.', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var part in parts)
    {
        if (part.Contains("-"))
        {
            var rangeParts = part.Split('-');
            if (rangeParts.Length == 2 && int.TryParse(rangeParts[0], out int startPage) && int.TryParse(rangeParts[1], out int endPage))
            {
                if (startPage > 0 && startPage <= totalPages && endPage >= startPage && endPage <= totalPages)
                {
                    for (int i = startPage; i <= endPage; i++)
                        pages.Add(i);
                }
            }
        }
        else if (int.TryParse(part, out int pageNum))
        {
            if (pageNum > 0 && pageNum <= totalPages)
                pages.Add(pageNum);
        }
    }
    return pages.Distinct().OrderBy(x => x).ToList();
}

app.Run();

// Déclarations de types à placer APRÈS app.Run()

public static class ApiState
{
    public static ExtraitBancaire.Common.Models.ExtractionResult? LastExtractionResult { get; set; } = null;
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
