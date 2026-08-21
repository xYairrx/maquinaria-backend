using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maquinaria.Infraestructura.Migraciones.Central
{
    /// <summary>
    /// Semilla del plan de arranque.
    ///
    /// PROVISIONAL Y A PROPOSITO. Las definiciones comerciales de los planes son una
    /// decision de producto todavia abierta (ver docs/04-pendientes.md: "Que diferencia
    /// al Basico del Profesional"). Congelar precios y limites reales en una migracion
    /// append-only significaria que cambiar un precio exige un despliegue.
    ///
    /// Este plan existe solo para desbloquear la Fase 0: el aprovisionamiento necesita
    /// un plan al que asociar la suscripcion de la primera empresa. El catalogo real se
    /// cargara desde el panel de superadministrador, que es donde deben vivir los precios.
    /// </summary>
    public partial class CentralSemillaPlanBase : Migration
    {
        /// <summary>
        /// uuid v7 FIJOS, no generados.
        ///
        /// Una migracion tiene que producir el mismo resultado en todas las bases donde
        /// se aplique. Con Guid.CreateVersion7() cada base recibiria identificadores
        /// distintos, y cualquier cosa que despues los referencie dejaria de ser portable.
        /// Aqui importa poco porque la central es una sola, pero la semilla de permisos y
        /// roles de ContextoEmpresa correra en N bases y ahi es critico. Se establece la
        /// regla desde ahora.
        /// </summary>
        private const string IdPlan = "01994d00-0000-7000-8000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ON CONFLICT DO NOTHING la vuelve idempotente: si alguien ya creo un plan
            // con este codigo desde el panel, la migracion no lo pisa ni truena.
            //
            // orden y activo se dan explicitamente porque NO tienen DEFAULT en la base.
            // Eso es deliberado: un DEFAULT true en activo haria que EF Core omitiera la
            // columna al insertar un plan con Activo = false (false es el valor sentinel
            // de bool) y se guardaria activo. El precio de esa decision es que todo
            // INSERT en SQL crudo debe darles valor.
            migrationBuilder.Sql($"""
                INSERT INTO plan (id, codigo, nombre, descripcion, precio_mensual, moneda, orden, activo)
                VALUES (
                    '{IdPlan}',
                    'base',
                    'Plan base',
                    'Plan provisional de arranque. Sin definicion comercial: precio cero y sin limites. Reemplazar cuando el negocio defina el catalogo.',
                    0,
                    'MXN',
                    0,
                    true
                )
                ON CONFLICT (codigo) DO NOTHING;
                """);

            // -1 es PlanLimite.Ilimitado. El CHECK plan_limite_valor solo admite >= -1,
            // asi que es el unico negativo valido.
            migrationBuilder.Sql($"""
                INSERT INTO plan_limite (id, plan_id, clave, valor)
                VALUES
                    ('01994d00-0000-7000-8000-000000000011', '{IdPlan}', 'max_equipos',            -1),
                    ('01994d00-0000-7000-8000-000000000012', '{IdPlan}', 'max_usuarios',           -1),
                    ('01994d00-0000-7000-8000-000000000013', '{IdPlan}', 'max_sucursales',         -1),
                    ('01994d00-0000-7000-8000-000000000014', '{IdPlan}', 'max_almacenamiento_gb',  -1)
                ON CONFLICT (plan_id, clave) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El Down de una semilla SI hay que escribirlo: EF solo genera el de lo que
            // el mismo produjo, y aqui todo entro por migrationBuilder.Sql.
            //
            // Se borra por codigo y no por id para que tambien limpie un plan 'base' que
            // hubiera creado el ON CONFLICT de otra ruta. Los limites se van solos por el
            // ON DELETE CASCADE de fk_plan_limite_plan.
            //
            // Si el plan ya tiene suscripciones, fk_suscripcion_plan es RESTRICT y este
            // DELETE fallara. Es lo correcto: revertir la semilla no debe poder dejar
            // suscripciones apuntando a un plan inexistente.
            migrationBuilder.Sql("DELETE FROM plan WHERE codigo = 'base';");
        }
    }
}
