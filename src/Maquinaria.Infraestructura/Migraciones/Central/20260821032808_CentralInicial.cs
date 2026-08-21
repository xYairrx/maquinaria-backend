using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maquinaria.Infraestructura.Migraciones.Central
{
    /// <inheritdoc />
    public partial class CentralInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "plan",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "text", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    precio_mensual = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    moneda = table.Column<string>(type: "text", nullable: false, defaultValue: "MXN"),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan", x => x.id);
                    table.CheckConstraint("plan_moneda_valida", "length(moneda) = 3");
                    table.CheckConstraint("plan_precio_valido", "precio_mensual >= 0");
                });

            migrationBuilder.CreateTable(
                name: "tenant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    nombre_bd = table.Column<string>(type: "text", nullable: false),
                    razon_social = table.Column<string>(type: "text", nullable: false),
                    nombre_comercial = table.Column<string>(type: "text", nullable: true),
                    rfc = table.Column<string>(type: "text", nullable: true),
                    telefono = table.Column<string>(type: "text", nullable: true),
                    correo_contacto = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<short>(type: "smallint", nullable: false),
                    estado_aprovisionamiento = table.Column<short>(type: "smallint", nullable: false),
                    version_esquema = table.Column<string>(type: "text", nullable: true),
                    zona_horaria = table.Column<string>(type: "text", nullable: false, defaultValue: "America/Mexico_City"),
                    moneda = table.Column<string>(type: "text", nullable: false, defaultValue: "MXN"),
                    dia_pago = table.Column<short>(type: "smallint", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    eliminado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant", x => x.id);
                    table.CheckConstraint("tenant_aprovisionamiento", "estado_aprovisionamiento BETWEEN 1 AND 4");
                    table.CheckConstraint("tenant_bd_formato", "nombre_bd ~ '^[a-z][a-z0-9_]{2,62}$'");
                    table.CheckConstraint("tenant_dia_pago", "dia_pago IS NULL OR dia_pago BETWEEN 1 AND 31");
                    table.CheckConstraint("tenant_estado", "estado BETWEEN 1 AND 4");
                    table.CheckConstraint("tenant_moneda_valida", "length(moneda) = 3");
                    table.CheckConstraint("tenant_slug_formato", "slug ~ '^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$'");
                });

            migrationBuilder.CreateTable(
                name: "usuario_plataforma",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correo = table.Column<string>(type: "text", nullable: false),
                    hash_contrasena = table.Column<string>(type: "text", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    ultimo_acceso_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_plataforma", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plan_limite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clave = table.Column<string>(type: "text", nullable: false),
                    valor = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_limite", x => x.id);
                    table.CheckConstraint("plan_limite_valor", "valor >= -1");
                    table.ForeignKey(
                        name: "fk_plan_limite_plan",
                        column: x => x.plan_id,
                        principalTable: "plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "suscripcion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    estado = table.Column<short>(type: "smallint", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suscripcion", x => x.id);
                    table.CheckConstraint("suscripcion_estado", "estado BETWEEN 1 AND 4");
                    table.CheckConstraint("suscripcion_periodo_valido", "fin IS NULL OR fin > inicio");
                    table.ForeignKey(
                        name: "fk_suscripcion_plan",
                        column: x => x.plan_id,
                        principalTable: "plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_suscripcion_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "plan_codigo_unico",
                table: "plan",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "plan_limite_unico",
                table: "plan_limite",
                columns: new[] { "plan_id", "clave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_suscripcion_plan_id",
                table: "suscripcion",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_suscripcion_tenant",
                table: "suscripcion",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_estado",
                table: "tenant",
                column: "estado",
                filter: "eliminado_en IS NULL");

            migrationBuilder.CreateIndex(
                name: "tenant_bd_unica",
                table: "tenant",
                column: "nombre_bd",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "tenant_slug_unico",
                table: "tenant",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "usuario_plataforma_correo_unico",
                table: "usuario_plataforma",
                column: "correo",
                unique: true);

            // --------------------------------------------------------------
            // ESCRITO A MANO. EF Core no sabe generar constraints EXCLUDE: es
            // una caracteristica propia de PostgreSQL y EF es multi-motor.
            // --------------------------------------------------------------
            //
            // Impide que una empresa tenga dos suscripciones VIGENTES con periodos
            // traslapados. Lo garantiza el motor, no el codigo: con un
            // "if (existe) throw" en C#, dos peticiones simultaneas leerian ambas
            // "no existe" y ambas insertarian.
            //
            //   tenant_id WITH =                 misma empresa
            //   tstzrange(inicio, fin) WITH &&   periodos que se solapan
            //   WHERE estado IN (1, 2)           parcial: solo Prueba y Activa.
            //                                    Vencida y Cancelada son historial
            //                                    y no estorban para una nueva.
            //
            // Se usa la EXPRESION tstzrange(inicio, fin) y no una columna de tipo
            // rango, porque NpgsqlRange<T> obligaria a Maquinaria.Dominio a depender
            // de Npgsql. Los EXCLUDE aceptan expresiones, asi que sale gratis.
            //
            // Es el mismo mecanismo que en la Fase 1 impedira rentar dos veces el
            // mismo equipo en fechas traslapadas.
            migrationBuilder.Sql("""
                ALTER TABLE suscripcion
                    ADD CONSTRAINT suscripcion_sin_traslape
                    EXCLUDE USING gist (
                        tenant_id WITH =,
                        tstzrange(inicio, fin) WITH &&
                    ) WHERE (estado IN (1, 2));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No hace falta un DROP CONSTRAINT para suscripcion_sin_traslape: al
            // borrar la tabla se van sus constraints. Si en el futuro un EXCLUDE se
            // agrega a una tabla YA existente, ahi si hay que escribir su DROP a
            // mano, porque EF solo genera el Down de lo que el mismo produjo.
            migrationBuilder.DropTable(
                name: "plan_limite");

            migrationBuilder.DropTable(
                name: "suscripcion");

            migrationBuilder.DropTable(
                name: "usuario_plataforma");

            migrationBuilder.DropTable(
                name: "plan");

            migrationBuilder.DropTable(
                name: "tenant");
        }
    }
}
