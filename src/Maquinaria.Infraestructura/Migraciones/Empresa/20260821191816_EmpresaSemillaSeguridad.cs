using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maquinaria.Infraestructura.Migraciones.Empresa
{
    /// <summary>
    /// Semilla de seguridad: los 108 permisos y los 9 roles del modulo 25.
    ///
    /// rol_permiso SE QUEDA VACIA A PROPOSITO. El reparto de permisos lo define el
    /// administrador de cada empresa, porque en una empresa ventas autoriza y en
    /// otra solo cotiza. El arranque no depende de esa tabla: 'administrador' trae
    /// acceso_total y salta la verificacion.
    ///
    /// Tampoco se crea ningun usuario. El primer administrador lo crea el servicio
    /// de aprovisionamiento, que es quien sabe a que correo invitar; una migracion
    /// no puede saberlo.
    /// </summary>
    public partial class EmpresaSemillaSeguridad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ------------------------------------------------------------------
            // permiso - 18 modulos x 6 acciones
            // ------------------------------------------------------------------
            //
            // LA LISTA ESTA CONGELADA EN ESTE TEXTO, no se lee de ClavesModulo ni de
            // AccionesPermiso. Es deliberado y es la regla mas importante de una
            // semilla: una migracion tiene que producir el mismo resultado en toda
            // base donde se aplique, hoy y en dos anos. Si leyera las constantes de
            // C#, agregar un modulo cambiaria el SQL de una migracion YA APLICADA y
            // una base nueva recibiria permisos que las viejas no tienen.
            //
            // Los 12 modulos que faltan del catalogo reciben sus permisos en una
            // migracion NUEVA, que es lo que el diseno ya dice: cada migracion que
            // agrega un modulo agrega tambien sus permisos.
            //
            // La columna modulo referencia modulo.clave de la base CENTRAL y no
            // puede tener FK: son bases distintas. Hace falta la prueba en CI que
            // verifique la correspondencia.
            //
            // uuid v7 FIJOS con el numero del modulo y el indice de la accion en los
            // ultimos digitos, para que leer un rol_permiso en crudo no obligue a
            // hacer dos joins.
            migrationBuilder.Sql("""
                INSERT INTO permiso (id, clave, modulo, accion, descripcion)
                VALUES
                    ('01994d10-0001-7000-8000-000000000001', 'dashboard.consultar', 'dashboard', 'consultar', 'Consultar en Dashboard.'),
                    ('01994d10-0001-7000-8000-000000000002', 'dashboard.crear', 'dashboard', 'crear', 'Crear en Dashboard.'),
                    ('01994d10-0001-7000-8000-000000000003', 'dashboard.editar', 'dashboard', 'editar', 'Editar en Dashboard.'),
                    ('01994d10-0001-7000-8000-000000000004', 'dashboard.eliminar', 'dashboard', 'eliminar', 'Eliminar en Dashboard.'),
                    ('01994d10-0001-7000-8000-000000000005', 'dashboard.autorizar', 'dashboard', 'autorizar', 'Autorizar en Dashboard.'),
                    ('01994d10-0001-7000-8000-000000000006', 'dashboard.exportar', 'dashboard', 'exportar', 'Exportar en Dashboard.'),
                    ('01994d10-0002-7000-8000-000000000001', 'equipos.consultar', 'equipos', 'consultar', 'Consultar en Equipos.'),
                    ('01994d10-0002-7000-8000-000000000002', 'equipos.crear', 'equipos', 'crear', 'Crear en Equipos.'),
                    ('01994d10-0002-7000-8000-000000000003', 'equipos.editar', 'equipos', 'editar', 'Editar en Equipos.'),
                    ('01994d10-0002-7000-8000-000000000004', 'equipos.eliminar', 'equipos', 'eliminar', 'Eliminar en Equipos.'),
                    ('01994d10-0002-7000-8000-000000000005', 'equipos.autorizar', 'equipos', 'autorizar', 'Autorizar en Equipos.'),
                    ('01994d10-0002-7000-8000-000000000006', 'equipos.exportar', 'equipos', 'exportar', 'Exportar en Equipos.'),
                    ('01994d10-0003-7000-8000-000000000001', 'disponibilidad.consultar', 'disponibilidad', 'consultar', 'Consultar en Disponibilidad.'),
                    ('01994d10-0003-7000-8000-000000000002', 'disponibilidad.crear', 'disponibilidad', 'crear', 'Crear en Disponibilidad.'),
                    ('01994d10-0003-7000-8000-000000000003', 'disponibilidad.editar', 'disponibilidad', 'editar', 'Editar en Disponibilidad.'),
                    ('01994d10-0003-7000-8000-000000000004', 'disponibilidad.eliminar', 'disponibilidad', 'eliminar', 'Eliminar en Disponibilidad.'),
                    ('01994d10-0003-7000-8000-000000000005', 'disponibilidad.autorizar', 'disponibilidad', 'autorizar', 'Autorizar en Disponibilidad.'),
                    ('01994d10-0003-7000-8000-000000000006', 'disponibilidad.exportar', 'disponibilidad', 'exportar', 'Exportar en Disponibilidad.'),
                    ('01994d10-0004-7000-8000-000000000001', 'clientes.consultar', 'clientes', 'consultar', 'Consultar en Clientes.'),
                    ('01994d10-0004-7000-8000-000000000002', 'clientes.crear', 'clientes', 'crear', 'Crear en Clientes.'),
                    ('01994d10-0004-7000-8000-000000000003', 'clientes.editar', 'clientes', 'editar', 'Editar en Clientes.'),
                    ('01994d10-0004-7000-8000-000000000004', 'clientes.eliminar', 'clientes', 'eliminar', 'Eliminar en Clientes.'),
                    ('01994d10-0004-7000-8000-000000000005', 'clientes.autorizar', 'clientes', 'autorizar', 'Autorizar en Clientes.'),
                    ('01994d10-0004-7000-8000-000000000006', 'clientes.exportar', 'clientes', 'exportar', 'Exportar en Clientes.'),
                    ('01994d10-0005-7000-8000-000000000001', 'cotizaciones.consultar', 'cotizaciones', 'consultar', 'Consultar en Cotizaciones.'),
                    ('01994d10-0005-7000-8000-000000000002', 'cotizaciones.crear', 'cotizaciones', 'crear', 'Crear en Cotizaciones.'),
                    ('01994d10-0005-7000-8000-000000000003', 'cotizaciones.editar', 'cotizaciones', 'editar', 'Editar en Cotizaciones.'),
                    ('01994d10-0005-7000-8000-000000000004', 'cotizaciones.eliminar', 'cotizaciones', 'eliminar', 'Eliminar en Cotizaciones.'),
                    ('01994d10-0005-7000-8000-000000000005', 'cotizaciones.autorizar', 'cotizaciones', 'autorizar', 'Autorizar en Cotizaciones.'),
                    ('01994d10-0005-7000-8000-000000000006', 'cotizaciones.exportar', 'cotizaciones', 'exportar', 'Exportar en Cotizaciones.'),
                    ('01994d10-0006-7000-8000-000000000001', 'contratos.consultar', 'contratos', 'consultar', 'Consultar en Contratos.'),
                    ('01994d10-0006-7000-8000-000000000002', 'contratos.crear', 'contratos', 'crear', 'Crear en Contratos.'),
                    ('01994d10-0006-7000-8000-000000000003', 'contratos.editar', 'contratos', 'editar', 'Editar en Contratos.'),
                    ('01994d10-0006-7000-8000-000000000004', 'contratos.eliminar', 'contratos', 'eliminar', 'Eliminar en Contratos.'),
                    ('01994d10-0006-7000-8000-000000000005', 'contratos.autorizar', 'contratos', 'autorizar', 'Autorizar en Contratos.'),
                    ('01994d10-0006-7000-8000-000000000006', 'contratos.exportar', 'contratos', 'exportar', 'Exportar en Contratos.'),
                    ('01994d10-0007-7000-8000-000000000001', 'rentas.consultar', 'rentas', 'consultar', 'Consultar en Rentas.'),
                    ('01994d10-0007-7000-8000-000000000002', 'rentas.crear', 'rentas', 'crear', 'Crear en Rentas.'),
                    ('01994d10-0007-7000-8000-000000000003', 'rentas.editar', 'rentas', 'editar', 'Editar en Rentas.'),
                    ('01994d10-0007-7000-8000-000000000004', 'rentas.eliminar', 'rentas', 'eliminar', 'Eliminar en Rentas.'),
                    ('01994d10-0007-7000-8000-000000000005', 'rentas.autorizar', 'rentas', 'autorizar', 'Autorizar en Rentas.'),
                    ('01994d10-0007-7000-8000-000000000006', 'rentas.exportar', 'rentas', 'exportar', 'Exportar en Rentas.'),
                    ('01994d10-0008-7000-8000-000000000001', 'logistica.consultar', 'logistica', 'consultar', 'Consultar en Logistica y fletes.'),
                    ('01994d10-0008-7000-8000-000000000002', 'logistica.crear', 'logistica', 'crear', 'Crear en Logistica y fletes.'),
                    ('01994d10-0008-7000-8000-000000000003', 'logistica.editar', 'logistica', 'editar', 'Editar en Logistica y fletes.'),
                    ('01994d10-0008-7000-8000-000000000004', 'logistica.eliminar', 'logistica', 'eliminar', 'Eliminar en Logistica y fletes.'),
                    ('01994d10-0008-7000-8000-000000000005', 'logistica.autorizar', 'logistica', 'autorizar', 'Autorizar en Logistica y fletes.'),
                    ('01994d10-0008-7000-8000-000000000006', 'logistica.exportar', 'logistica', 'exportar', 'Exportar en Logistica y fletes.'),
                    ('01994d10-0011-7000-8000-000000000001', 'evidencias.consultar', 'evidencias', 'consultar', 'Consultar en Evidencias.'),
                    ('01994d10-0011-7000-8000-000000000002', 'evidencias.crear', 'evidencias', 'crear', 'Crear en Evidencias.'),
                    ('01994d10-0011-7000-8000-000000000003', 'evidencias.editar', 'evidencias', 'editar', 'Editar en Evidencias.'),
                    ('01994d10-0011-7000-8000-000000000004', 'evidencias.eliminar', 'evidencias', 'eliminar', 'Eliminar en Evidencias.'),
                    ('01994d10-0011-7000-8000-000000000005', 'evidencias.autorizar', 'evidencias', 'autorizar', 'Autorizar en Evidencias.'),
                    ('01994d10-0011-7000-8000-000000000006', 'evidencias.exportar', 'evidencias', 'exportar', 'Exportar en Evidencias.'),
                    ('01994d10-0012-7000-8000-000000000001', 'horometros.consultar', 'horometros', 'consultar', 'Consultar en Horometros y kilometraje.'),
                    ('01994d10-0012-7000-8000-000000000002', 'horometros.crear', 'horometros', 'crear', 'Crear en Horometros y kilometraje.'),
                    ('01994d10-0012-7000-8000-000000000003', 'horometros.editar', 'horometros', 'editar', 'Editar en Horometros y kilometraje.'),
                    ('01994d10-0012-7000-8000-000000000004', 'horometros.eliminar', 'horometros', 'eliminar', 'Eliminar en Horometros y kilometraje.'),
                    ('01994d10-0012-7000-8000-000000000005', 'horometros.autorizar', 'horometros', 'autorizar', 'Autorizar en Horometros y kilometraje.'),
                    ('01994d10-0012-7000-8000-000000000006', 'horometros.exportar', 'horometros', 'exportar', 'Exportar en Horometros y kilometraje.'),
                    ('01994d10-0019-7000-8000-000000000001', 'pagos.consultar', 'pagos', 'consultar', 'Consultar en Pagos y cobranza.'),
                    ('01994d10-0019-7000-8000-000000000002', 'pagos.crear', 'pagos', 'crear', 'Crear en Pagos y cobranza.'),
                    ('01994d10-0019-7000-8000-000000000003', 'pagos.editar', 'pagos', 'editar', 'Editar en Pagos y cobranza.'),
                    ('01994d10-0019-7000-8000-000000000004', 'pagos.eliminar', 'pagos', 'eliminar', 'Eliminar en Pagos y cobranza.'),
                    ('01994d10-0019-7000-8000-000000000005', 'pagos.autorizar', 'pagos', 'autorizar', 'Autorizar en Pagos y cobranza.'),
                    ('01994d10-0019-7000-8000-000000000006', 'pagos.exportar', 'pagos', 'exportar', 'Exportar en Pagos y cobranza.'),
                    ('01994d10-0020-7000-8000-000000000001', 'facturacion.consultar', 'facturacion', 'consultar', 'Consultar en Facturacion.'),
                    ('01994d10-0020-7000-8000-000000000002', 'facturacion.crear', 'facturacion', 'crear', 'Crear en Facturacion.'),
                    ('01994d10-0020-7000-8000-000000000003', 'facturacion.editar', 'facturacion', 'editar', 'Editar en Facturacion.'),
                    ('01994d10-0020-7000-8000-000000000004', 'facturacion.eliminar', 'facturacion', 'eliminar', 'Eliminar en Facturacion.'),
                    ('01994d10-0020-7000-8000-000000000005', 'facturacion.autorizar', 'facturacion', 'autorizar', 'Autorizar en Facturacion.'),
                    ('01994d10-0020-7000-8000-000000000006', 'facturacion.exportar', 'facturacion', 'exportar', 'Exportar en Facturacion.'),
                    ('01994d10-0024-7000-8000-000000000001', 'configuracion.consultar', 'configuracion', 'consultar', 'Consultar en Configuracion.'),
                    ('01994d10-0024-7000-8000-000000000002', 'configuracion.crear', 'configuracion', 'crear', 'Crear en Configuracion.'),
                    ('01994d10-0024-7000-8000-000000000003', 'configuracion.editar', 'configuracion', 'editar', 'Editar en Configuracion.'),
                    ('01994d10-0024-7000-8000-000000000004', 'configuracion.eliminar', 'configuracion', 'eliminar', 'Eliminar en Configuracion.'),
                    ('01994d10-0024-7000-8000-000000000005', 'configuracion.autorizar', 'configuracion', 'autorizar', 'Autorizar en Configuracion.'),
                    ('01994d10-0024-7000-8000-000000000006', 'configuracion.exportar', 'configuracion', 'exportar', 'Exportar en Configuracion.'),
                    ('01994d10-0025-7000-8000-000000000001', 'seguridad.consultar', 'seguridad', 'consultar', 'Consultar en Seguridad.'),
                    ('01994d10-0025-7000-8000-000000000002', 'seguridad.crear', 'seguridad', 'crear', 'Crear en Seguridad.'),
                    ('01994d10-0025-7000-8000-000000000003', 'seguridad.editar', 'seguridad', 'editar', 'Editar en Seguridad.'),
                    ('01994d10-0025-7000-8000-000000000004', 'seguridad.eliminar', 'seguridad', 'eliminar', 'Eliminar en Seguridad.'),
                    ('01994d10-0025-7000-8000-000000000005', 'seguridad.autorizar', 'seguridad', 'autorizar', 'Autorizar en Seguridad.'),
                    ('01994d10-0025-7000-8000-000000000006', 'seguridad.exportar', 'seguridad', 'exportar', 'Exportar en Seguridad.'),
                    ('01994d10-0026-7000-8000-000000000001', 'notificaciones.consultar', 'notificaciones', 'consultar', 'Consultar en Notificaciones.'),
                    ('01994d10-0026-7000-8000-000000000002', 'notificaciones.crear', 'notificaciones', 'crear', 'Crear en Notificaciones.'),
                    ('01994d10-0026-7000-8000-000000000003', 'notificaciones.editar', 'notificaciones', 'editar', 'Editar en Notificaciones.'),
                    ('01994d10-0026-7000-8000-000000000004', 'notificaciones.eliminar', 'notificaciones', 'eliminar', 'Eliminar en Notificaciones.'),
                    ('01994d10-0026-7000-8000-000000000005', 'notificaciones.autorizar', 'notificaciones', 'autorizar', 'Autorizar en Notificaciones.'),
                    ('01994d10-0026-7000-8000-000000000006', 'notificaciones.exportar', 'notificaciones', 'exportar', 'Exportar en Notificaciones.'),
                    ('01994d10-0027-7000-8000-000000000001', 'rentabilidad.consultar', 'rentabilidad', 'consultar', 'Consultar en Rentabilidad y reportes.'),
                    ('01994d10-0027-7000-8000-000000000002', 'rentabilidad.crear', 'rentabilidad', 'crear', 'Crear en Rentabilidad y reportes.'),
                    ('01994d10-0027-7000-8000-000000000003', 'rentabilidad.editar', 'rentabilidad', 'editar', 'Editar en Rentabilidad y reportes.'),
                    ('01994d10-0027-7000-8000-000000000004', 'rentabilidad.eliminar', 'rentabilidad', 'eliminar', 'Eliminar en Rentabilidad y reportes.'),
                    ('01994d10-0027-7000-8000-000000000005', 'rentabilidad.autorizar', 'rentabilidad', 'autorizar', 'Autorizar en Rentabilidad y reportes.'),
                    ('01994d10-0027-7000-8000-000000000006', 'rentabilidad.exportar', 'rentabilidad', 'exportar', 'Exportar en Rentabilidad y reportes.'),
                    ('01994d10-0029-7000-8000-000000000001', 'campo.consultar', 'campo', 'consultar', 'Consultar en Campo.'),
                    ('01994d10-0029-7000-8000-000000000002', 'campo.crear', 'campo', 'crear', 'Crear en Campo.'),
                    ('01994d10-0029-7000-8000-000000000003', 'campo.editar', 'campo', 'editar', 'Editar en Campo.'),
                    ('01994d10-0029-7000-8000-000000000004', 'campo.eliminar', 'campo', 'eliminar', 'Eliminar en Campo.'),
                    ('01994d10-0029-7000-8000-000000000005', 'campo.autorizar', 'campo', 'autorizar', 'Autorizar en Campo.'),
                    ('01994d10-0029-7000-8000-000000000006', 'campo.exportar', 'campo', 'exportar', 'Exportar en Campo.'),
                    ('01994d10-0030-7000-8000-000000000001', 'subrenta.consultar', 'subrenta', 'consultar', 'Consultar en Subrenta.'),
                    ('01994d10-0030-7000-8000-000000000002', 'subrenta.crear', 'subrenta', 'crear', 'Crear en Subrenta.'),
                    ('01994d10-0030-7000-8000-000000000003', 'subrenta.editar', 'subrenta', 'editar', 'Editar en Subrenta.'),
                    ('01994d10-0030-7000-8000-000000000004', 'subrenta.eliminar', 'subrenta', 'eliminar', 'Eliminar en Subrenta.'),
                    ('01994d10-0030-7000-8000-000000000005', 'subrenta.autorizar', 'subrenta', 'autorizar', 'Autorizar en Subrenta.'),
                    ('01994d10-0030-7000-8000-000000000006', 'subrenta.exportar', 'subrenta', 'exportar', 'Exportar en Subrenta.')
                ON CONFLICT (clave) DO NOTHING;
                """);

            // ------------------------------------------------------------------
            // rol - los 9 del modulo 25
            // ------------------------------------------------------------------
            //
            // es_sistema en TODOS: son semilla y borrarlos dejaria a la empresa sin
            // estructura de roles. Pero es_sistema por si solo NO concede nada.
            //
            // acceso_total SOLO en 'administrador'. Dos garantias en la base lo
            // sostienen, ninguna en codigo: el indice unico parcial
            // rol_acceso_total_unico admite como maximo una fila con acceso_total, y
            // el trigger rol_sistema_inmutable rechaza UPDATE y DELETE sobre la fila
            // que trae es_sistema y acceso_total. Ese rol no se puede editar,
            // borrar, ni apagarle el acceso.
            //
            // es_sistema y acceso_total se dan explicitamente porque NO tienen
            // DEFAULT en la base: un DEFAULT en un bool haria que EF Core omitiera
            // la columna al insertar con false, que es su valor sentinel.
            //
            // Los nombres son un punto de partida: el diseno dice que cada empresa
            // los renombra y ajusta.
            migrationBuilder.Sql("""
                INSERT INTO rol (id, codigo, nombre, descripcion, es_sistema, acceso_total)
                VALUES
                    ('01994d11-0000-7000-8000-000000000001', 'administrador', 'Administrador', 'Acceso total. No se puede editar ni borrar, y no se asigna desde la interfaz.', true, true),
                    ('01994d11-0000-7000-8000-000000000002', 'direccion', 'Direccion', 'Vision de negocio, indicadores y reportes.', true, false),
                    ('01994d11-0000-7000-8000-000000000003', 'ventas', 'Ventas', 'Clientes, obras y cotizaciones.', true, false),
                    ('01994d11-0000-7000-8000-000000000004', 'rentas', 'Rentas', 'Contratos, rentas, extensiones y devoluciones.', true, false),
                    ('01994d11-0000-7000-8000-000000000005', 'logistica', 'Logistica', 'Fletes, vehiculos, operadores y rutas.', true, false),
                    ('01994d11-0000-7000-8000-000000000006', 'taller', 'Taller', 'Mantenimiento, ordenes de trabajo y refacciones.', true, false),
                    ('01994d11-0000-7000-8000-000000000007', 'operador', 'Operador', 'Campo: inspecciones, evidencias y horometros.', true, false),
                    ('01994d11-0000-7000-8000-000000000008', 'cobranza', 'Cobranza', 'Pagos, saldos y facturacion.', true, false),
                    ('01994d11-0000-7000-8000-000000000009', 'cliente', 'Cliente', 'Usuario externo: consulta sus propias rentas.', true, false)
                ON CONFLICT (codigo) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El trigger rol_sistema_inmutable rechazaria el DELETE de
            // 'administrador' — que es justo su trabajo — asi que revertir la
            // semilla exige desactivarlo mientras dura la operacion.
            //
            // Se desactiva y se reactiva en el mismo Down, nunca se borra: la
            // funcion y el trigger pertenecen a EmpresaInicial, no a esta migracion.
            //
            // Si algun usuario ya tiene roles asignados, fk_usuario_rol_rol es
            // RESTRICT y este DELETE fallara. Es lo correcto: revertir la semilla no
            // debe poder dejar asignaciones apuntando a un rol inexistente. Lo mismo
            // con fk_rol_permiso_permiso y los permisos concedidos.
            migrationBuilder.Sql("""
                ALTER TABLE rol DISABLE TRIGGER rol_sistema_inmutable;
                DELETE FROM rol;
                ALTER TABLE rol ENABLE TRIGGER rol_sistema_inmutable;
                """);

            migrationBuilder.Sql("DELETE FROM permiso;");
        }
    }
}
