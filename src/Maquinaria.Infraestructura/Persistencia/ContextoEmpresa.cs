using Maquinaria.Dominio.Archivos;
using Maquinaria.Dominio.Catalogos;
using Maquinaria.Dominio.Organizacion;
using Maquinaria.Dominio.Terceros;
using Maquinaria.Dominio.Configuracion;
using Maquinaria.Dominio.Seguridad;
using Maquinaria.Dominio.Trazabilidad;
using Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Persistencia;

/// <summary>
/// La base de UNA empresa. Aqui viven sus usuarios, sus equipos y sus rentas.
///
/// A diferencia de <see cref="ContextoCentral"/>, NO tiene cadena de conexion
/// fija: se construye en tiempo de ejecucion con la de la empresa resuelta en el
/// login, a partir de tenant.nombre_bd. Sus migraciones se aplican N veces, una
/// por empresa, y cada base lleva su propio __EFMigrationsHistory.
///
/// Ninguna tabla lleva tenant_id: la base entera es de un solo cliente y el
/// aislamiento es fisico.
/// </summary>
public class ContextoEmpresa : DbContext
{
    public ContextoEmpresa(DbContextOptions<ContextoEmpresa> opciones)
        : base(opciones)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<TokenAcceso> TokensAcceso => Set<TokenAcceso>();

    public DbSet<SesionRefresh> SesionesRefresh => Set<SesionRefresh>();

    public DbSet<Permiso> Permisos => Set<Permiso>();

    public DbSet<Rol> Roles => Set<Rol>();

    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();

    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();

    // ---------------------------------------------------------- Fase 1 --
    public DbSet<CategoriaEquipo> CategoriasEquipo => Set<CategoriaEquipo>();

    public DbSet<TipoEquipo> TiposEquipo => Set<TipoEquipo>();

    public DbSet<Marca> Marcas => Set<Marca>();

    public DbSet<ModeloEquipo> ModelosEquipo => Set<ModeloEquipo>();

    public DbSet<Sucursal> Sucursales => Set<Sucursal>();

    public DbSet<Ubicacion> Ubicaciones => Set<Ubicacion>();

    public DbSet<Puesto> Puestos => Set<Puesto>();

    public DbSet<Trabajador> Trabajadores => Set<Trabajador>();

    public DbSet<Proveedor> Proveedores => Set<Proveedor>();

    // ---------------------------------------------------------- Fase 0 --
    public DbSet<Parametro> Parametros => Set<Parametro>();

    public DbSet<Archivo> Archivos => Set<Archivo>();

    /// <summary>
    /// La bitacora. Se ESCRIBE por interceptor, no por caso de uso, y no se lee para
    /// decidir nada: un trigger la vuelve append-only en el motor.
    /// </summary>
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        // Van en el modelo y no en migrationBuilder.Sql para que queden en el
        // snapshot: asi EF sabe que existen y no hay SQL invisible para el.
        //
        // btree_gist lo necesita el EXCLUDE de ocupacion_equipo (Fase 1), que es la
        // razon tecnica mas fuerte para haber elegido PostgreSQL. pg_trgm, la
        // busqueda por texto parcial. Se declaran desde la PRIMERA migracion para
        // que toda base de empresa nueva las traiga.
        modelo.HasPostgresExtension("btree_gist");
        modelo.HasPostgresExtension("pg_trgm");

        // El filtro NO es opcional. Sin el, ApplyConfigurationsFromAssembly
        // recogeria tambien las de Configuraciones/Central y crearia las tablas de
        // plataforma dentro de la base de cada empresa.
        //
        // Se compara contra typeof(...).Namespace y no contra una cadena literal
        // para que renombrar o mover la carpeta no rompa el filtro en silencio.
        var espacio = typeof(UsuarioConfiguracion).Namespace!;

        modelo.ApplyConfigurationsFromAssembly(
            typeof(ContextoEmpresa).Assembly,
            tipo => tipo.Namespace?.StartsWith(espacio, StringComparison.Ordinal) == true);
    }
}
