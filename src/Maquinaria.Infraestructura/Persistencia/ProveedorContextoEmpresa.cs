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

    /// <summary>
    /// Un contexto para PREGUNTARLE AL ENSAMBLADO que migraciones trae el codigo, sin
    /// conectarse a nada: <c>Database.GetMigrations()</c> no toca la base.
    ///
    /// Apunta a la plantilla porque construir el contexto exige una cadena y esa es la
    /// base inofensiva por definicion —existe justo para que un comando distraido no
    /// pueda hacer dano—. Si algun dia alguien le agrega una consulta a este contexto, va
    /// a dar contra maquinaria_plantilla y se va a notar.
    /// </summary>
    public ContextoEmpresa ParaLeerMigraciones() => ParaMigrar(fabrica.Prefijo + "plantilla");
    /// Un contexto solo para preguntarle al ENSAMBLADO que migraciones existen.
    ///
    /// GetMigrations() lee el ensamblado, no la base, asi que esto NO abre conexion.
    /// Existe para no tener que nombrar una base concreta con la unica intencion de
    /// construir un contexto: la version anterior nombraba maquinaria_plantilla, que
    /// es una base desechable, y ataba la revision de esquema a que siguiera existiendo.
    /// </summary>
    public ContextoEmpresa ParaLeerMigraciones()
    {
        var opciones = new DbContextOptionsBuilder<ContextoEmpresa>();
        opciones.UsarPostgres(fabrica.CadenaCentralDirecta());

        return new ContextoEmpresa(opciones.Options);
    }
}
