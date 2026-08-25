using Microsoft.Extensions.Logging;

namespace Maquinaria.Aplicacion.Plataforma;

/// <summary>
/// Crea un plan del catalogo comercial.
///
/// POR QUE ESTO EXISTE Y NO SE SIEMBRA POR MIGRACION: una migracion es append-only, asi que
/// cambiar un precio exigiria un despliegue y dejaria el historial de precios enterrado en
/// el codigo. La decision esta en `docs/04-pendientes.md` §3, que pide expresamente que los
/// precios reales se carguen desde el panel. El plan `base` que siembra
/// `CentralSemillaCatalogos` es provisional y su propio comentario lo dice.
///
/// LO QUE ESTE CASO DE USO NO HACE, y es deliberado: no edita. Un plan no se puede
/// modificar por dos razones que estan en el modelo:
///
/// 1. `Suscripcion` no guarda importe, solo apunta al plan. Cambiar `PrecioMensual`
///    cambiaria lo que pagan los suscriptores actuales Y reescribiria lo que pagaron los
///    historicos. Hasta que la suscripcion congele su precio, editarlo es reescribir el
///    pasado.
/// 2. El plan ES su conjunto de modulos, asi que quitarle uno se lo quita a todos sus
///    suscriptores, retroactivamente y sin aviso. El dominio ya lo advierte: quien necesite
///    un modulo extra necesita otro plan.
///
/// Lo que si se puede es RETIRAR un plan y crear el sucesor, que es lo que el modelo
/// contempla con `Activo`. Ver `CambiarActivoAsync`.
/// </summary>
public sealed class CrearPlan(ICatalogoPlanes catalogo, ILogger<CrearPlan> log)
{
    public async Task<ResultadoPlan> EjecutarAsync(AltaDePlan alta, CancellationToken ct)
    {
        var codigo = FormatoCodigoPlan.Normalizar(alta.Codigo);

        // ---------- validaciones que no cuestan nada ----------
        if (!FormatoCodigoPlan.EsValido(codigo))
        {
            return ResultadoPlan.Rechazado(FormatoCodigoPlan.Explicacion);
        }

        if (string.IsNullOrWhiteSpace(alta.Nombre))
        {
            return ResultadoPlan.Rechazado("El nombre del plan es obligatorio.");
        }

        // El CHECK de la base tambien lo impide, pero llegar hasta el INSERT convierte un
        // dato mal capturado en un 500 generico en lugar de en un mensaje util.
        if (alta.PrecioMensual < 0)
        {
            return ResultadoPlan.Rechazado("El precio mensual no puede ser negativo.");
        }

        var moneda = alta.Moneda?.Trim().ToUpperInvariant() ?? string.Empty;

        if (moneda.Length != 3)
        {
            return ResultadoPlan.Rechazado(
                "La moneda es un codigo ISO 4217 de tres letras, por ejemplo MXN.");
        }

        // Un plan sin modulos no da acceso a nada: la empresa que lo contrate entra y no ve
        // ni una pantalla. Es un plan que no se puede vender, asi que no se puede crear.
        if (alta.Modulos.Count == 0)
        {
            return ResultadoPlan.Rechazado(
                "Un plan necesita al menos un modulo: es lo que define a que da acceso.");
        }

        var claves = alta.Modulos
            .Select(m => m.Trim().ToLowerInvariant())
            .Where(m => m.Length > 0)
            .Distinct()
            .ToArray();

        // ---------- validaciones que si cuestan una consulta ----------
        if (await catalogo.ExisteCodigoAsync(codigo, ct))
        {
            // El UNIQUE de la base lo impediria igual, pero como excepcion de Npgsql: este
            // mensaje dice cual es el codigo repetido.
            return ResultadoPlan.Rechazado($"Ya existe un plan con el codigo '{codigo}'.");
        }

        var desconocidas = await catalogo.ClavesDeModuloDesconocidasAsync(claves, ct);

        if (desconocidas.Count > 0)
        {
            return ResultadoPlan.Rechazado(
                $"Estos modulos no existen o estan inactivos: {string.Join(", ", desconocidas)}.");
        }

        var plan = await catalogo.CrearAsync(
            alta with { Codigo = codigo, Moneda = moneda, Modulos = claves },
            ct);

        // A nivel de aviso: crear un plan es una decision comercial, poco frecuente, y
        // conviene que quede en la bitacora sin tener que subir el nivel de todo el log.
        log.LogWarning(
            "Plan '{Codigo}' creado con {Modulos} modulos y precio {Precio} {Moneda}.",
            plan.Codigo, plan.Modulos.Count, plan.PrecioMensual, plan.Moneda);

        return ResultadoPlan.Exito(plan);
    }
}
