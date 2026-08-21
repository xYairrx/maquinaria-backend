using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maquinaria.Infraestructura.Migraciones.Central
{
    /// <summary>
    /// Semilla de los catalogos de plataforma: modulos, tipos de limite y el plan
    /// de arranque.
    ///
    /// modulo y tipo_limite son catalogos DE CODIGO, no configuracion del cliente:
    /// existen porque hay pantallas, endpoints y verificaciones que los implementan.
    /// Por eso se siembran aqui y no se cargan desde el panel, igual que permiso en
    /// la base de empresa.
    ///
    /// El PLAN, en cambio, es una decision comercial todavia abierta (ver
    /// docs/04-pendientes.md). El plan 'base' que se inserta abajo es PROVISIONAL:
    /// existe solo para desbloquear la Fase 0, porque el aprovisionamiento necesita
    /// un plan al que asociar la suscripcion de la primera empresa. Los precios y la
    /// composicion real se cargaran desde el panel de superadministrador, que es
    /// donde deben vivir: congelarlos en una migracion append-only significaria que
    /// cambiar un precio exige un despliegue.
    /// </summary>
    public partial class CentralSemillaCatalogos : Migration
    {
        /// <summary>
        /// uuid v7 FIJOS, no generados.
        ///
        /// Una migracion tiene que producir el mismo resultado en todas las bases
        /// donde se aplique. Con Guid.CreateVersion7() cada base recibiria
        /// identificadores distintos, y cualquier cosa que despues los referencie
        /// dejaria de ser portable. Aqui importa poco porque la central es una sola,
        /// pero la semilla de permisos y roles de ContextoEmpresa correra en N bases
        /// y ahi es critico. Se establece la regla desde ahora.
        ///
        /// Los de modulo llevan el numero del modulo en los ultimos digitos, para que
        /// leer un plan_modulo en crudo no obligue a hacer un join.
        /// </summary>
        private const string IdPlanBase = "01994d00-0000-7000-8000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ------------------------------------------------------------------
            // modulo - LOS MODULOS SON LA DEFINICION DEL PLAN
            // ------------------------------------------------------------------
            //
            // INCOMPLETO A PROPOSITO: 18 de los 30 que define la especificacion
            // funcional. Son los unicos cuya identidad los documentos de diseno
            // fijan sin ambiguedad. Faltan M9 y M10 (campo e inspecciones, sin
            // reparto individual claro), M13 a M18 (taller, nombrado solo como
            // rango), y M21 a M23 y M28 (no aparecen en ningun documento).
            //
            // El documento funcional (.docx) no esta en el repositorio, asi que no
            // hay de donde sacarlos. Agregarlos despues no cuesta nada:
            // modulo_numero_unico admite los numeros en cualquier orden.
            //
            // Las claves tienen que coincidir con ClavesModulo y con la columna
            // 'modulo' de la tabla permiso de cada base de empresa. Esa relacion no
            // puede tener FK -- son bases distintas -- asi que hace falta una prueba
            // en CI que la verifique.
            //
            // orden y activo se dan explicitamente porque NO tienen DEFAULT en la
            // base. Eso es deliberado: un DEFAULT true en activo haria que EF Core
            // omitiera la columna al insertar con Activo = false (false es el valor
            // sentinel de bool) y se guardaria activo. El precio de esa decision es
            // que todo INSERT en SQL crudo debe darles valor.
            migrationBuilder.Sql(
                """
                INSERT INTO modulo (id, clave, numero, nombre, descripcion, orden, activo)
                VALUES
                    ('01994d00-0001-7000-8000-000000000001', 'dashboard',      1,  'Dashboard',                'Indicadores y alertas de la operacion.',                  1,  true),
                    ('01994d00-0001-7000-8000-000000000002', 'equipos',        2,  'Equipos',                  'Expediente del equipo, tarifas, documentos y multimedia.', 2,  true),
                    ('01994d00-0001-7000-8000-000000000003', 'disponibilidad', 3,  'Disponibilidad',           'Calendario de ocupacion del equipo.',                     3,  true),
                    ('01994d00-0001-7000-8000-000000000004', 'clientes',       4,  'Clientes',                 'Clientes, contactos, domicilios y obras.',                4,  true),
                    ('01994d00-0001-7000-8000-000000000005', 'cotizaciones',   5,  'Cotizaciones',             'Cotizaciones y sus lineas.',                              5,  true),
                    ('01994d00-0001-7000-8000-000000000006', 'contratos',      6,  'Contratos',                'Formalizacion de la cotizacion.',                         6,  true),
                    ('01994d00-0001-7000-8000-000000000007', 'rentas',         7,  'Rentas',                   'Rentas, lineas, cargos y extensiones.',                   7,  true),
                    ('01994d00-0001-7000-8000-000000000008', 'logistica',      8,  'Logistica y fletes',       'Fletes, vehiculos, operadores y rutas.',                  8,  true),
                    ('01994d00-0001-7000-8000-000000000011', 'evidencias',     11, 'Evidencias',               'Fotos, videos, documentos y firmas por evento.',          11, true),
                    ('01994d00-0001-7000-8000-000000000012', 'horometros',     12, 'Horometros y kilometraje', 'Lecturas de horometro y kilometraje.',                    12, true),
                    ('01994d00-0001-7000-8000-000000000019', 'pagos',          19, 'Pagos y cobranza',         'Pagos, aplicaciones y saldos.',                          19, true),
                    ('01994d00-0001-7000-8000-000000000020', 'facturacion',    20, 'Facturacion',              'Documentos fiscales y CFDI.',                            20, true),
                    ('01994d00-0001-7000-8000-000000000024', 'configuracion',  24, 'Configuracion',            'Sucursales, patios y parametros.',                       24, true),
                    ('01994d00-0001-7000-8000-000000000025', 'seguridad',      25, 'Seguridad',                'Usuarios, roles y permisos.',                            25, true),
                    ('01994d00-0001-7000-8000-000000000026', 'notificaciones', 26, 'Notificaciones',           'Avisos y alertas.',                                      26, true),
                    ('01994d00-0001-7000-8000-000000000027', 'rentabilidad',   27, 'Rentabilidad y reportes',  'Movimientos de costo y reportes.',                       27, true),
                    ('01994d00-0001-7000-8000-000000000029', 'campo',          29, 'Campo',                    'Operacion en sitio, con trabajo sin red.',               29, true),
                    ('01994d00-0001-7000-8000-000000000030', 'subrenta',       30, 'Subrenta',                 'Equipo de proveedor rentado al cliente.',                30, true)
                ON CONFLICT (clave) DO NOTHING;
                """);

            // ------------------------------------------------------------------
            // tipo_limite - LOS LIMITES CUELGAN DEL TENANT, NO DEL PLAN
            // ------------------------------------------------------------------
            //
            // Este es el catalogo; el valor de cada empresa vive en tenant_limite.
            // valor_defecto en -1 (ilimitado) a proposito: dar de alta una empresa no
            // tiene que insertar ni una fila de tenant_limite, y nadie queda limitado
            // por omision.
            //
            // OJO con lo que este catalogo NO hace: un tipo de limite es solo un
            // nombre con integridad referencial. No acota nada hasta que exista
            // codigo que lo lea y bloquee la operacion. Hoy no hay ninguno.
            migrationBuilder.Sql(
                """
                INSERT INTO tipo_limite (id, clave, nombre, descripcion, unidad, valor_defecto, orden, activo)
                VALUES
                    ('01994d00-0002-7000-8000-000000000001', 'max_equipos',           'Equipos',        'Maximo de equipos dados de alta.',            'equipos',    -1, 1, true),
                    ('01994d00-0002-7000-8000-000000000002', 'max_usuarios',          'Usuarios',       'Maximo de cuentas de usuario activas.',       'usuarios',   -1, 2, true),
                    ('01994d00-0002-7000-8000-000000000003', 'max_sucursales',        'Sucursales',     'Maximo de sucursales.',                       'sucursales', -1, 3, true),
                    ('01994d00-0002-7000-8000-000000000004', 'max_almacenamiento_gb', 'Almacenamiento', 'Maximo de almacenamiento de archivos en R2.', 'GB',         -1, 4, true)
                ON CONFLICT (clave) DO NOTHING;
                """);

            // ------------------------------------------------------------------
            // plan 'base' - PROVISIONAL
            // ------------------------------------------------------------------
            //
            // ON CONFLICT DO NOTHING la vuelve idempotente: si alguien ya creo un
            // plan con este codigo desde el panel, la migracion no lo pisa ni truena.
            migrationBuilder.Sql(
                $"""
                INSERT INTO plan (id, codigo, nombre, descripcion, precio_mensual, moneda, orden, activo)
                VALUES (
                    '{IdPlanBase}',
                    'base',
                    'Plan base',
                    'Plan provisional de arranque, con todos los modulos y sin limites. Sin definicion comercial: reemplazar cuando el negocio defina el catalogo.',
                    0,
                    'MXN',
                    0,
                    true
                )
                ON CONFLICT (codigo) DO NOTHING;
                """);

            // El plan base incluye TODOS los modulos sembrados. Se arma con
            // INSERT ... SELECT y no con una lista de pares, para que no haya dos
            // sitios que enumeren los modulos y puedan desincronizarse.
            migrationBuilder.Sql(
                $"""
                INSERT INTO plan_modulo (plan_id, modulo_id)
                SELECT '{IdPlanBase}', id FROM modulo
                ON CONFLICT (plan_id, modulo_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El Down de una semilla SI hay que escribirlo: EF solo genera el de lo
            // que el mismo produjo, y aqui todo entro por migrationBuilder.Sql.
            //
            // El ORDEN importa por las FK. plan primero: fk_plan_modulo_plan es
            // CASCADE, asi que se lleva las filas de plan_modulo. Solo entonces se
            // pueden borrar los modulos, porque fk_plan_modulo_modulo es RESTRICT.
            //
            // Si el plan ya tiene suscripciones, fk_suscripcion_plan es RESTRICT y
            // este DELETE fallara. Es lo correcto: revertir la semilla no debe poder
            // dejar suscripciones apuntando a un plan inexistente.
            //
            // Se borra por codigo y no por id para que tambien limpie un plan 'base'
            // que hubiera creado el ON CONFLICT de otra ruta.
            migrationBuilder.Sql("DELETE FROM plan WHERE codigo = 'base';");

            migrationBuilder.Sql("DELETE FROM modulo;");

            // tenant_limite referencia tipo_limite con RESTRICT. Si una empresa ya
            // tiene cupos fijados, este DELETE fallara, y tambien es lo correcto.
            migrationBuilder.Sql("DELETE FROM tipo_limite;");
        }
    }
}
