using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Maquinaria.Api.Arranque;

/// <summary>
/// Corrige lo que .NET 10 emite mal en /openapi/v1.json.
///
/// EXISTE PORQUE EL DOCUMENTO ES UN CONTRATO, NO UNA CORTESIA. El frontend genera sus
/// tipos de aqui con `npm run api:sync`, asi que cada imprecision del documento se
/// convierte en un tipo debil en las 18 pantallas de la Fase 1 — y en un `as` o un
/// `Number(...)` repartido por todas ellas.
///
/// Se midio antes de escribir esto, contra el documento real:
///
///   - 279 campos salian como `number | string`
///   - 17 enums salian como `number` pelado, sin sus valores
///
/// Los dos defectos vienen del comportamiento por defecto de AddOpenApi(), que se
/// llamaba pelado. Ninguno es un error del modelo ni de los controladores.
/// </summary>
internal static class EsquemaOpenApi
{
    public static IServiceCollection AgregarOpenApiDelProducto(this IServiceCollection servicios)
        => servicios.AddOpenApi(opciones =>
        {
            opciones.AddSchemaTransformer((esquema, contexto, _) =>
            {
                NormalizarNumeros(esquema);
                DeclararValoresDeEnum(esquema, contexto.JsonTypeInfo.Type);

                return Task.CompletedTask;
            });
        });

    /// <summary>
    /// Colapsa <c>type: ["integer","string"]</c> a <c>integer</c>.
    ///
    /// .NET 10 declara los numericos como union con string porque su lector acepta
    /// "42" ademas de 42. Es cierto de la ENTRADA, pero el documento no distingue
    /// entrada de salida, asi que tambien contamina las respuestas: `MarcaDto.modelos`
    /// —un conteo que el servidor SIEMPRE serializa como numero— llegaba tipado
    /// `number | string`.
    ///
    /// El costo de dejarlo era que ninguna cantidad, importe ni folio se pudiera sumar
    /// ni pasar por un pipe numerico sin estrechar el tipo primero, en 279 campos.
    ///
    /// Se quita tambien el <c>pattern</c>, que solo existia para validar la variante
    /// string y sin ella no describe nada.
    /// </summary>
    private static void NormalizarNumeros(IOpenApiSchema esquema)
    {
        if (esquema is not OpenApiSchema concreto || concreto.Type is not { } tipo)
        {
            return;
        }

        var esNumerico = tipo.HasFlag(JsonSchemaType.Integer) || tipo.HasFlag(JsonSchemaType.Number);

        if (!esNumerico || !tipo.HasFlag(JsonSchemaType.String))
        {
            return;
        }

        concreto.Type = tipo & ~JsonSchemaType.String;
        concreto.Pattern = null;
    }

    /// <summary>
    /// Escribe los valores admitidos de un enum en su esquema.
    ///
    /// Las convenciones del proyecto guardan los enums como enteros en la base y como
    /// <c>enum : short</c> en C#, y AddOpenApi() emitia por eso un <c>type: integer</c>
    /// sin mas. El documento decia entonces que <c>EstadoRenta</c> es "un entero
    /// cualquiera", asi que el tipo generado era <c>number</c> y nada impedia mandar 99.
    ///
    /// Justo donde mas importa: los estados de renta, cotizacion y contrato SON maquinas
    /// de estados, y son el corazon de la fase.
    ///
    /// Los valores se leen del propio tipo con reflexion, asi que agregar un valor al
    /// enum lo publica solo. Se emiten los NUMEROS, no los nombres, porque eso es lo que
    /// viaja en el JSON.
    /// </summary>
    private static void DeclararValoresDeEnum(IOpenApiSchema esquema, Type tipoClr)
    {
        if (esquema is not OpenApiSchema concreto)
        {
            return;
        }

        // Un enum anulable llega como Nullable<TEnum>; el que interesa es el de dentro.
        var tipo = Nullable.GetUnderlyingType(tipoClr) ?? tipoClr;

        if (!tipo.IsEnum || concreto.Enum is { Count: > 0 })
        {
            return;
        }

        // Convert.ToInt64 sobre el valor del enum funciona para cualquier tipo
        // subyacente —short, int, byte—, que es lo que este proyecto mezcla.
        concreto.Enum = [.. Enum.GetValues(tipo)
            .Cast<object>()
            .Select(valor => (JsonNode)JsonValue.Create(Convert.ToInt64(valor)))];

        // El nombre de cada valor, en el orden de los numeros de arriba. No es
        // decorativo: sin esto, quien lee el documento ve [1,2,3,4] y no sabe cual es
        // cual, y el generador de tipos tampoco puede nombrarlos.
        concreto.Description = string.Join(
            " · ",
            Enum.GetValues(tipo)
                .Cast<object>()
                .Select(valor => $"{Convert.ToInt64(valor)} = {valor}"));
    }
}
