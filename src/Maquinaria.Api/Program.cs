var builder = WebApplication.CreateBuilder(args);

// Documento OpenAPI nativo de .NET 10 (sin Swashbuckle). Se expone en /openapi/v1.json,
// que es la fuente del cliente HTTP generado del frontend (npm run api:sync).
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
