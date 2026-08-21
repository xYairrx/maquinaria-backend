using Maquinaria.Infraestructura.Persistencia;

var builder = WebApplication.CreateBuilder(args);

// Documento OpenAPI nativo de .NET 10 (sin Swashbuckle). Se expone en /openapi/v1.json,
// que es la fuente del cliente HTTP generado del frontend (npm run api:sync).
builder.Services.AddOpenApi();

// Base central. Cadena POOLED: es el runtime. Las migraciones usan la directa,
// via FabricaContextoCentral.
builder.Services.AddDbContext<ContextoCentral>(opciones =>
    opciones.UsarPostgres(
        builder.Configuration.GetConnectionString("Central")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Central.")));

var app = builder.Build();
    
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();  
