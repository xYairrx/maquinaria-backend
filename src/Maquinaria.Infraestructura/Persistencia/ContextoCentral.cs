using Maquinaria.Dominio.Plataforma;
using Maquinaria.Infraestructura.Persistencia.Configuraciones.Central;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Persistencia;

/// <summary>
/// La base central: el negocio DEL SaaS. Que empresas existen, que contrataron y
/// en que estado esta cada una. Aqui no vive ni un equipo ni una renta.
///
/// Su cadena de conexion es fija, de configuracion. La de cada empresa se resuelve
/// en tiempo de ejecucion y la sirve ContextoEmpresa.
/// </summary>
public class ContextoCentral : DbContext
{
    public ContextoCentral(DbContextOptions<ContextoCentral> opciones)
        : base(opciones)
    {
    }

    public DbSet<Plan> Planes => Set<Plan>();

    public DbSet<PlanLimite> PlanLimites => Set<PlanLimite>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Suscripcion> Suscripciones => Set<Suscripcion>();

    public DbSet<UsuarioPlataforma> UsuariosPlataforma => Set<UsuarioPlataforma>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        // Requerida por el EXCLUDE de suscripcion: sin btree_gist no se puede
        // combinar un operador de igualdad sobre uuid con un rango en un indice gist.
        //
        // Se declara en el modelo y no con migrationBuilder.Sql para que quede
        // registrada en el snapshot: asi EF sabe que existe y no hay SQL invisible
        // para el en el historial.
        modelo.HasPostgresExtension("btree_gist");

        // El filtro NO es opcional. Sin el, ApplyConfigurationsFromAssembly
        // recogeria tambien las de Configuraciones/Empresa y crearia las 10 tablas
        // de las empresas dentro de la base central.
        //
        // Se compara contra typeof(...).Namespace y no contra una cadena literal
        // para que renombrar o mover la carpeta no rompa el filtro en silencio.
        var espacio = typeof(PlanConfiguracion).Namespace!;

        modelo.ApplyConfigurationsFromAssembly(
            typeof(ContextoCentral).Assembly,
            tipo => tipo.Namespace?.StartsWith(espacio, StringComparison.Ordinal) == true);
    }
}
