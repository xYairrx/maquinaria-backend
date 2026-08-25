namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Que version de esquema tiene cada empresa y quien quedo atrasado, para el panel de
/// superadministracion.
///
/// LEE version_esquema DE LA CENTRAL y no se conecta a las bases de las empresas. Es una
/// simplificacion deliberada: consultar __EFMigrationsHistory de N bases en una peticion
/// HTTP son N conexiones y N puntos de falla, y el dato ya lo mantienen los dos unicos
/// caminos que aplican migraciones —el aprovisionamiento y migrar-empresas—.
///
/// Consecuencia aceptada: si alguien aplica migraciones a mano sin actualizar la central,
/// este reporte miente hasta la siguiente corrida de migrar-empresas, que la corrige.
/// </summary>
// ponytail: la alternativa fiel seria abrir cada base por peticion; no vale el costo
// mientras version_esquema solo lo escriban esos dos caminos.
public sealed class SaludEsquemas(IRegistroTenants registro, IAprovisionadorBaseDatos bases)
{
    public async Task<ReporteSaludEsquemas> EjecutarAsync(CancellationToken ct)
    {
        var disponibles = bases.VersionesDisponibles();
        var empresas = await registro.ListarConEsquemaAsync(ct);

        // La proyeccion no es ceremonia: es lo que deja fuera nombre_bd, que no sale del
        // servidor ni en este endpoint ni en ninguno.
        var estados = new List<EstadoEsquemaEmpresa>(empresas.Count);

        foreach (var empresa in empresas)
        {
            var comparacion = ComparadorEsquema.Comparar(empresa.VersionEsquema, disponibles);

            estados.Add(new EstadoEsquemaEmpresa(
                empresa.Id,
                empresa.Slug,
                empresa.RazonSocial,
                empresa.Estado,
                empresa.Aprovisionamiento,
                comparacion.VersionAplicada,
                comparacion.MigracionesPendientes,
                comparacion.Desfasada,
                comparacion.VersionReconocida));
        }

        return new ReporteSaludEsquemas(
            disponibles.Count > 0 ? disponibles[^1] : null,
            estados.Count,
            estados.Count(e => e.Desfasada),
            estados);
    }
}
