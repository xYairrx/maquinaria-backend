using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maquinaria.Infraestructura.Migraciones.Central
{
    /// <summary>
    /// Completa el catalogo de modulos a los 26 que define la especificacion funcional,
    /// y corrige cuatro nombres.
    ///
    /// POR QUE FALTABAN. Cuando se sembro el catalogo, el .docx de la especificacion NO
    /// ESTABA EN EL REPOSITORIO: docs/README.md lo enlazaba y el archivo no existia. Se
    /// sembraron los 18 modulos cuya identidad los documentos de diseno fijaban sin
    /// ambiguedad, y los otros 8 quedaron fuera a proposito en lugar de inventarlos.
    /// Con el documento a la vista ya se pueden agregar.
    ///
    /// Y SON 26, NO 30. El documento numera hasta 30 pero salta el 21, 22, 23 y 28: esos
    /// modulos no existen. La cifra "30 modulos" que aparecia en toda la documentacion
    /// del proyecto era incorrecta.
    ///
    /// Los cuatro renombres corrigen suposiciones: el peor era M29, que se habia sembrado
    /// como "Campo" —por el nombre de la Fase 5— cuando el documento lo define como
    /// "QR de equipos". La PWA de campo no es un modulo, es una fase.
    ///
    /// OJO: modulo.clave se refleja en permiso.modulo de CADA base de empresa, y esa
    /// relacion no puede tener FK porque son bases distintas. Los renombres de aqui
    /// tienen su contraparte en EmpresaPermisosModulosCompletos, y las dos migraciones
    /// hay que aplicarlas juntas.
    /// </summary>
    public partial class CentralModulosCompletos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se actualiza POR NUMERO y no por clave: el numero es la referencia estable
            // al documento funcional y no cambia nunca; la clave es justo lo que aqui se
            // esta cambiando.
            migrationBuilder.Sql("""
                UPDATE modulo SET clave = 'sucursales', nombre = 'Sucursales y patios', descripcion = 'Sucursales, patios y transferencias de equipo entre ellos.'
                 WHERE numero = 24;
                UPDATE modulo SET clave = 'usuarios', nombre = 'Usuarios y permisos', descripcion = 'Usuarios, roles y la matriz de permisos por modulo.'
                 WHERE numero = 25;
                UPDATE modulo SET clave = 'reportes', nombre = 'Reportes', descripcion = 'Reportes operativos, de mantenimiento, financieros y de rentabilidad.'
                 WHERE numero = 27;
                UPDATE modulo SET clave = 'qr', nombre = 'QR de equipos', descripcion = 'QR unico por equipo que abre su expediente al escanearlo.'
                 WHERE numero = 29;
                """);

            migrationBuilder.Sql("""
                INSERT INTO modulo (id, clave, numero, nombre, descripcion, orden, activo)
                VALUES
                    ('01994d00-0001-7000-8000-000000000009', 'inspeccion-salida', 9, 'Inspeccion de salida', 'Checklist y evidencias del estado del equipo antes de entregarlo.', 9, true),
                    ('01994d00-0001-7000-8000-000000000010', 'inspeccion-devolucion', 10, 'Inspeccion de devolucion', 'Comparacion contra la inspeccion de salida: danos nuevos, faltantes y excedentes.', 10, true),
                    ('01994d00-0001-7000-8000-000000000013', 'mantenimiento', 13, 'Mantenimiento', 'Mantenimiento preventivo y correctivo.', 13, true),
                    ('01994d00-0001-7000-8000-000000000014', 'ordenes-trabajo', 14, 'Ordenes de trabajo', 'Cada trabajo realizado a un equipo, con refacciones, mano de obra y costo.', 14, true),
                    ('01994d00-0001-7000-8000-000000000015', 'proximo-servicio', 15, 'Proximo servicio', 'Calculo automatico del siguiente servicio por fecha, horometro, kilometraje o condicion.', 15, true),
                    ('01994d00-0001-7000-8000-000000000016', 'refacciones', 16, 'Inventario de refacciones', 'Existencias, stock minimo y consumo de piezas en mantenimiento.', 16, true),
                    ('01994d00-0001-7000-8000-000000000017', 'compras', 17, 'Compras', 'Solicitud, autorizacion, orden de compra, recepcion y entrada a inventario.', 17, true),
                    ('01994d00-0001-7000-8000-000000000018', 'proveedores', 18, 'Proveedores', 'Proveedores de refacciones, mantenimiento, fletes, subrentas y servicios.', 18, true)
                ON CONFLICT (clave) DO NOTHING;
                """);

            // El plan 'base' es el provisional que incluye TODO, asi que hereda los ocho
            // nuevos. Se arma con INSERT ... SELECT para no volver a enumerar modulos.
            migrationBuilder.Sql("""
                INSERT INTO plan_modulo (plan_id, modulo_id)
                SELECT p.id, m.id FROM plan p, modulo m WHERE p.codigo = 'base'
                ON CONFLICT (plan_id, modulo_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Primero las filas de plan_modulo de los ocho nuevos: fk_plan_modulo_modulo
            // es RESTRICT, asi que sin esto el DELETE de modulo fallaria.
            migrationBuilder.Sql("""
                DELETE FROM plan_modulo
                 WHERE modulo_id IN (SELECT id FROM modulo WHERE numero IN (9, 10, 13, 14, 15, 16, 17, 18));

                DELETE FROM modulo WHERE numero IN (9, 10, 13, 14, 15, 16, 17, 18);
                """);

            // Y se revierten los cuatro renombres a los valores que tenian.
            migrationBuilder.Sql("""
                UPDATE modulo SET clave = 'configuracion' WHERE numero = 24;
                UPDATE modulo SET clave = 'seguridad' WHERE numero = 25;
                UPDATE modulo SET clave = 'rentabilidad' WHERE numero = 27;
                UPDATE modulo SET clave = 'campo' WHERE numero = 29;
                """);
        }
    }
}
