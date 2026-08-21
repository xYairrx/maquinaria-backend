namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// El unico lugar donde se escriben las claves de <see cref="Modulo"/>.
///
/// Mismo papel que <see cref="ClavesLimite"/>, y por la misma razon: la clave de
/// un modulo aparece en dos bases de datos distintas —modulo.clave en la central y
/// permiso.modulo en la de cada empresa— y esa relacion no puede tener FK. Una
/// cadena mal escrita en cualquiera de los dos lados abre la compuerta en silencio.
///
/// SON 26 MODULOS, NO 30. La especificacion funcional numera hasta 30 pero SALTA el
/// 21, 22, 23 y 28: esos modulos no existen. La cifra "30 modulos" que circulo en la
/// documentacion del proyecto era incorrecta. Ver docs/especificacion-funcional.md.
///
/// El numero de cada modulo es la referencia estable al documento y no cambia nunca;
/// la clave es lo que se compone con la accion para formar un permiso.
/// </summary>
public static class ClavesModulo
{
    /// <summary>M1.</summary>
    public const string Dashboard = "dashboard";

    /// <summary>M2.</summary>
    public const string Equipos = "equipos";

    /// <summary>M3.</summary>
    public const string Disponibilidad = "disponibilidad";

    /// <summary>M4.</summary>
    public const string Clientes = "clientes";

    /// <summary>M5.</summary>
    public const string Cotizaciones = "cotizaciones";

    /// <summary>M6.</summary>
    public const string Contratos = "contratos";

    /// <summary>M7.</summary>
    public const string Rentas = "rentas";

    /// <summary>M8.</summary>
    public const string Logistica = "logistica";

    /// <summary>M9.</summary>
    public const string InspeccionSalida = "inspeccion-salida";

    /// <summary>M10.</summary>
    public const string InspeccionDevolucion = "inspeccion-devolucion";

    /// <summary>M11.</summary>
    public const string Evidencias = "evidencias";

    /// <summary>M12.</summary>
    public const string Horometros = "horometros";

    /// <summary>M13.</summary>
    public const string Mantenimiento = "mantenimiento";

    /// <summary>M14.</summary>
    public const string OrdenesTrabajo = "ordenes-trabajo";

    /// <summary>M15.</summary>
    public const string ProximoServicio = "proximo-servicio";

    /// <summary>M16.</summary>
    public const string Refacciones = "refacciones";

    /// <summary>M17.</summary>
    public const string Compras = "compras";

    /// <summary>M18.</summary>
    public const string Proveedores = "proveedores";

    /// <summary>M19.</summary>
    public const string Pagos = "pagos";

    /// <summary>M20.</summary>
    public const string Facturacion = "facturacion";

    /// <summary>M24.</summary>
    public const string Sucursales = "sucursales";

    /// <summary>M25.</summary>
    public const string Usuarios = "usuarios";

    /// <summary>M26.</summary>
    public const string Notificaciones = "notificaciones";

    /// <summary>M27.</summary>
    public const string Reportes = "reportes";

    /// <summary>M29.</summary>
    public const string Qr = "qr";

    /// <summary>M30.</summary>
    public const string Subrenta = "subrenta";

    /// <summary>
    /// Los 26 modulos. Sirve para validar las semillas y para detectar filas
    /// huerfanas tras un cambio de nombre.
    ///
    /// NO la leen las migraciones: una semilla congela su propia lista, porque tiene
    /// que producir el mismo resultado en toda base donde se aplique, hoy y en dos
    /// anos. Si leyera esta constante, agregar un modulo cambiaria el SQL de una
    /// migracion ya aplicada.
    /// </summary>
    public static readonly string[] Todas =
    [
        Dashboard,
        Equipos,
        Disponibilidad,
        Clientes,
        Cotizaciones,
        Contratos,
        Rentas,
        Logistica,
        InspeccionSalida,
        InspeccionDevolucion,
        Evidencias,
        Horometros,
        Mantenimiento,
        OrdenesTrabajo,
        ProximoServicio,
        Refacciones,
        Compras,
        Proveedores,
        Pagos,
        Facturacion,
        Sucursales,
        Usuarios,
        Notificaciones,
        Reportes,
        Qr,
        Subrenta,
    ];
}
