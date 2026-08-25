using Maquinaria.Dominio.Activos;
using Maquinaria.Dominio.Archivos;
using Maquinaria.Dominio.Catalogos;
using Maquinaria.Dominio.Comercial;
using Maquinaria.Dominio.Compras;
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

    public DbSet<Ubicacion> Ubicaciones => Set<Ubicacion>();

    public DbSet<Tarifa> Tarifas => Set<Tarifa>();

    public DbSet<Clausula> Clausulas => Set<Clausula>();

    public DbSet<Puesto> Puestos => Set<Puesto>();

    public DbSet<Trabajador> Trabajadores => Set<Trabajador>();

    public DbSet<Proveedor> Proveedores => Set<Proveedor>();

    public DbSet<Cliente> Clientes => Set<Cliente>();

    // ------------------------------------------------- Fase 1: activos --
    public DbSet<Equipo> Equipos => Set<Equipo>();

    public DbSet<EquipoArchivo> EquipoArchivos => Set<EquipoArchivo>();

    public DbSet<EquipoTarifa> EquipoTarifas => Set<EquipoTarifa>();

    public DbSet<TransferenciaEquipo> TransferenciasEquipo => Set<TransferenciaEquipo>();

    /// <summary>
    /// El calendario de cada maquina. Es la tabla que impide rentar dos veces las
    /// mismas fechas, y lo hace con una restriccion EXCLUDE en el motor.
    /// </summary>
    public DbSet<OcupacionEquipo> OcupacionesEquipo => Set<OcupacionEquipo>();

    // ----------------------------------------------- Fase 1: comercial --
    public DbSet<Cotizacion> Cotizaciones => Set<Cotizacion>();

    public DbSet<CotizacionLinea> CotizacionLineas => Set<CotizacionLinea>();

    public DbSet<Renta> Rentas => Set<Renta>();

    public DbSet<RentaLinea> RentaLineas => Set<RentaLinea>();

    public DbSet<RentaConcepto> RentaConceptos => Set<RentaConcepto>();

    public DbSet<ExtensionRenta> ExtensionesRenta => Set<ExtensionRenta>();

    /// <summary>
    /// Inmutable en cuanto sale de borrador. Lo impone un disparador, no la aplicacion.
    /// </summary>
    public DbSet<Contrato> Contratos => Set<Contrato>();

    public DbSet<ContratoClausula> ContratoClausulas => Set<ContratoClausula>();

    public DbSet<OrdenVenta> OrdenesVenta => Set<OrdenVenta>();

    public DbSet<OrdenVentaDetalle> OrdenVentaDetalles => Set<OrdenVentaDetalle>();

    // ------------------------------------------------- Fase 1: compras --
    public DbSet<OrdenCompra> OrdenesCompra => Set<OrdenCompra>();

    public DbSet<OrdenCompraDetalle> OrdenCompraDetalles => Set<OrdenCompraDetalle>();

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
