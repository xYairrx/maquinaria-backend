using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Persistencia;

/// <summary>
/// Construye un <see cref="ContextoEmpresa"/> contra una base ESPECIFICADA
/// EXPLICITAMENTE, sin pasar por el tenant de la peticion.
///
/// Hace falta para los dos caminos que no tienen peticion detras:
///
/// - el APROVISIONAMIENTO, que migra una base que acaba de crear y que todavia no
///   esta en ninguna cache ni en ningun JWT;
/// - el comando MIGRAR-EMPRESAS, que recorre todas las bases una por una.
///
/// Usa la cadena DIRECTA, no la pooled: los dos casos corren DDL.
///
/// OJO: el contexto que devuelve NO pasa por el contenedor de DI, asi que no lleva
/// los interceptores registrados —incluido el de auditoria—. Es lo correcto: migrar
/// no es una operacion de negocio auditable fila por fila, y el interceptor
/// necesitaria un usuario que en este camino no existe.
/// </summary>
public sealed class ProveedorContextoEmpresa(FabricaConexionesEmpresa fabrica)
{
    public ContextoEmpresa ParaMigrar(string nombreBd)
    {
        var opciones = new DbContextOptionsBuilder<ContextoEmpresa>();
        opciones.UsarPostgres(fabrica.CadenaDeMigracion(nombreBd));

        return new ContextoEmpresa(opciones.Options);
    }
}
