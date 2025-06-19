using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ExtraitBancaire.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configuration du HttpClient avec l'URL de l'API
builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri("http://localhost:8080/")
});

// Configuration JSON
builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.PropertyNamingPolicy = null;
    options.WriteIndented = true;
});

await builder.Build().RunAsync();

