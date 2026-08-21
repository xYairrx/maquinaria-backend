using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maquinaria.Infraestructura.Migraciones.Empresa
{
    /// <summary>
    /// Contraparte de CentralModulosCompletos en la base de cada empresa: renombra los
    /// permisos de los cuatro modulos corregidos y siembra los 48 de los ocho nuevos.
    ///
    /// El catalogo de permisos pasa de 108 a 156: los 26 modulos por las 6 acciones.
    ///
    /// LAS DOS MIGRACIONES VAN JUNTAS. permiso.modulo referencia modulo.clave de la base
    /// CENTRAL y no puede tener FK, porque son bases distintas. Si solo se aplica una, la
    /// compuerta de autorizacion —permisos del rol interseccion modulos del plan— deja de
    /// cerrar en silencio, sin que nada truene. Eso es exactamente lo que la prueba de CI
    /// pendiente tiene que detectar.
    ///
    /// rol_permiso NO se toca: sigue vacia. El reparto lo define el administrador de cada
    /// empresa, y 'administrador' no lo necesita porque trae acceso_total.
    /// </summary>
    public partial class EmpresaPermisosModulosCompletos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La clave se recompone como modulo.accion en lugar de escribir las 24 filas
            // a mano: menos superficie para una errata.
            //
            // permiso_clave_unica lo protege: si algun destino ya existiera, esto falla en
            // lugar de crear un duplicado silencioso.
            migrationBuilder.Sql("""
                UPDATE permiso SET modulo = 'sucursales', clave = 'sucursales.' || accion
                 WHERE modulo = 'configuracion';
                UPDATE permiso SET modulo = 'usuarios', clave = 'usuarios.' || accion
                 WHERE modulo = 'seguridad';
                UPDATE permiso SET modulo = 'reportes', clave = 'reportes.' || accion
                 WHERE modulo = 'rentabilidad';
                UPDATE permiso SET modulo = 'qr', clave = 'qr.' || accion
                 WHERE modulo = 'campo';
                """);

            // Mismos uuid v7 fijos y mismo esquema que la semilla original: el numero del
            // modulo y el indice de la accion en los ultimos digitos.
            //
            // La lista va CONGELADA en este texto, no leida de ClavesModulo: una migracion
            // tiene que producir el mismo resultado en toda base donde se aplique.
            migrationBuilder.Sql("""
                INSERT INTO permiso (id, clave, modulo, accion, descripcion)
                VALUES
                    ('01994d10-0009-7000-8000-000000000001', 'inspeccion-salida.consultar', 'inspeccion-salida', 'consultar', 'Consultar en Inspeccion de salida.'),
                    ('01994d10-0009-7000-8000-000000000002', 'inspeccion-salida.crear', 'inspeccion-salida', 'crear', 'Crear en Inspeccion de salida.'),
                    ('01994d10-0009-7000-8000-000000000003', 'inspeccion-salida.editar', 'inspeccion-salida', 'editar', 'Editar en Inspeccion de salida.'),
                    ('01994d10-0009-7000-8000-000000000004', 'inspeccion-salida.eliminar', 'inspeccion-salida', 'eliminar', 'Eliminar en Inspeccion de salida.'),
                    ('01994d10-0009-7000-8000-000000000005', 'inspeccion-salida.autorizar', 'inspeccion-salida', 'autorizar', 'Autorizar en Inspeccion de salida.'),
                    ('01994d10-0009-7000-8000-000000000006', 'inspeccion-salida.exportar', 'inspeccion-salida', 'exportar', 'Exportar en Inspeccion de salida.'),
                    ('01994d10-0010-7000-8000-000000000001', 'inspeccion-devolucion.consultar', 'inspeccion-devolucion', 'consultar', 'Consultar en Inspeccion de devolucion.'),
                    ('01994d10-0010-7000-8000-000000000002', 'inspeccion-devolucion.crear', 'inspeccion-devolucion', 'crear', 'Crear en Inspeccion de devolucion.'),
                    ('01994d10-0010-7000-8000-000000000003', 'inspeccion-devolucion.editar', 'inspeccion-devolucion', 'editar', 'Editar en Inspeccion de devolucion.'),
                    ('01994d10-0010-7000-8000-000000000004', 'inspeccion-devolucion.eliminar', 'inspeccion-devolucion', 'eliminar', 'Eliminar en Inspeccion de devolucion.'),
                    ('01994d10-0010-7000-8000-000000000005', 'inspeccion-devolucion.autorizar', 'inspeccion-devolucion', 'autorizar', 'Autorizar en Inspeccion de devolucion.'),
                    ('01994d10-0010-7000-8000-000000000006', 'inspeccion-devolucion.exportar', 'inspeccion-devolucion', 'exportar', 'Exportar en Inspeccion de devolucion.'),
                    ('01994d10-0013-7000-8000-000000000001', 'mantenimiento.consultar', 'mantenimiento', 'consultar', 'Consultar en Mantenimiento.'),
                    ('01994d10-0013-7000-8000-000000000002', 'mantenimiento.crear', 'mantenimiento', 'crear', 'Crear en Mantenimiento.'),
                    ('01994d10-0013-7000-8000-000000000003', 'mantenimiento.editar', 'mantenimiento', 'editar', 'Editar en Mantenimiento.'),
                    ('01994d10-0013-7000-8000-000000000004', 'mantenimiento.eliminar', 'mantenimiento', 'eliminar', 'Eliminar en Mantenimiento.'),
                    ('01994d10-0013-7000-8000-000000000005', 'mantenimiento.autorizar', 'mantenimiento', 'autorizar', 'Autorizar en Mantenimiento.'),
                    ('01994d10-0013-7000-8000-000000000006', 'mantenimiento.exportar', 'mantenimiento', 'exportar', 'Exportar en Mantenimiento.'),
                    ('01994d10-0014-7000-8000-000000000001', 'ordenes-trabajo.consultar', 'ordenes-trabajo', 'consultar', 'Consultar en Ordenes de trabajo.'),
                    ('01994d10-0014-7000-8000-000000000002', 'ordenes-trabajo.crear', 'ordenes-trabajo', 'crear', 'Crear en Ordenes de trabajo.'),
                    ('01994d10-0014-7000-8000-000000000003', 'ordenes-trabajo.editar', 'ordenes-trabajo', 'editar', 'Editar en Ordenes de trabajo.'),
                    ('01994d10-0014-7000-8000-000000000004', 'ordenes-trabajo.eliminar', 'ordenes-trabajo', 'eliminar', 'Eliminar en Ordenes de trabajo.'),
                    ('01994d10-0014-7000-8000-000000000005', 'ordenes-trabajo.autorizar', 'ordenes-trabajo', 'autorizar', 'Autorizar en Ordenes de trabajo.'),
                    ('01994d10-0014-7000-8000-000000000006', 'ordenes-trabajo.exportar', 'ordenes-trabajo', 'exportar', 'Exportar en Ordenes de trabajo.'),
                    ('01994d10-0015-7000-8000-000000000001', 'proximo-servicio.consultar', 'proximo-servicio', 'consultar', 'Consultar en Proximo servicio.'),
                    ('01994d10-0015-7000-8000-000000000002', 'proximo-servicio.crear', 'proximo-servicio', 'crear', 'Crear en Proximo servicio.'),
                    ('01994d10-0015-7000-8000-000000000003', 'proximo-servicio.editar', 'proximo-servicio', 'editar', 'Editar en Proximo servicio.'),
                    ('01994d10-0015-7000-8000-000000000004', 'proximo-servicio.eliminar', 'proximo-servicio', 'eliminar', 'Eliminar en Proximo servicio.'),
                    ('01994d10-0015-7000-8000-000000000005', 'proximo-servicio.autorizar', 'proximo-servicio', 'autorizar', 'Autorizar en Proximo servicio.'),
                    ('01994d10-0015-7000-8000-000000000006', 'proximo-servicio.exportar', 'proximo-servicio', 'exportar', 'Exportar en Proximo servicio.'),
                    ('01994d10-0016-7000-8000-000000000001', 'refacciones.consultar', 'refacciones', 'consultar', 'Consultar en Inventario de refacciones.'),
                    ('01994d10-0016-7000-8000-000000000002', 'refacciones.crear', 'refacciones', 'crear', 'Crear en Inventario de refacciones.'),
                    ('01994d10-0016-7000-8000-000000000003', 'refacciones.editar', 'refacciones', 'editar', 'Editar en Inventario de refacciones.'),
                    ('01994d10-0016-7000-8000-000000000004', 'refacciones.eliminar', 'refacciones', 'eliminar', 'Eliminar en Inventario de refacciones.'),
                    ('01994d10-0016-7000-8000-000000000005', 'refacciones.autorizar', 'refacciones', 'autorizar', 'Autorizar en Inventario de refacciones.'),
                    ('01994d10-0016-7000-8000-000000000006', 'refacciones.exportar', 'refacciones', 'exportar', 'Exportar en Inventario de refacciones.'),
                    ('01994d10-0017-7000-8000-000000000001', 'compras.consultar', 'compras', 'consultar', 'Consultar en Compras.'),
                    ('01994d10-0017-7000-8000-000000000002', 'compras.crear', 'compras', 'crear', 'Crear en Compras.'),
                    ('01994d10-0017-7000-8000-000000000003', 'compras.editar', 'compras', 'editar', 'Editar en Compras.'),
                    ('01994d10-0017-7000-8000-000000000004', 'compras.eliminar', 'compras', 'eliminar', 'Eliminar en Compras.'),
                    ('01994d10-0017-7000-8000-000000000005', 'compras.autorizar', 'compras', 'autorizar', 'Autorizar en Compras.'),
                    ('01994d10-0017-7000-8000-000000000006', 'compras.exportar', 'compras', 'exportar', 'Exportar en Compras.'),
                    ('01994d10-0018-7000-8000-000000000001', 'proveedores.consultar', 'proveedores', 'consultar', 'Consultar en Proveedores.'),
                    ('01994d10-0018-7000-8000-000000000002', 'proveedores.crear', 'proveedores', 'crear', 'Crear en Proveedores.'),
                    ('01994d10-0018-7000-8000-000000000003', 'proveedores.editar', 'proveedores', 'editar', 'Editar en Proveedores.'),
                    ('01994d10-0018-7000-8000-000000000004', 'proveedores.eliminar', 'proveedores', 'eliminar', 'Eliminar en Proveedores.'),
                    ('01994d10-0018-7000-8000-000000000005', 'proveedores.autorizar', 'proveedores', 'autorizar', 'Autorizar en Proveedores.'),
                    ('01994d10-0018-7000-8000-000000000006', 'proveedores.exportar', 'proveedores', 'exportar', 'Exportar en Proveedores.')
                ON CONFLICT (clave) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // fk_rol_permiso_permiso es RESTRICT: si algun rol ya tiene concedido uno de
            // estos permisos, el DELETE falla. Es lo correcto — revertir no debe dejar
            // concesiones apuntando a un permiso inexistente.
            migrationBuilder.Sql("""
                DELETE FROM permiso WHERE modulo IN ('inspeccion-salida', 'inspeccion-devolucion', 'mantenimiento', 'ordenes-trabajo', 'proximo-servicio', 'refacciones', 'compras', 'proveedores');
                """);

            migrationBuilder.Sql("""
                UPDATE permiso SET modulo = 'configuracion', clave = 'configuracion.' || accion WHERE modulo = 'sucursales';
                UPDATE permiso SET modulo = 'seguridad', clave = 'seguridad.' || accion WHERE modulo = 'usuarios';
                UPDATE permiso SET modulo = 'rentabilidad', clave = 'rentabilidad.' || accion WHERE modulo = 'reportes';
                UPDATE permiso SET modulo = 'campo', clave = 'campo.' || accion WHERE modulo = 'qr';
                """);
        }
    }
}
