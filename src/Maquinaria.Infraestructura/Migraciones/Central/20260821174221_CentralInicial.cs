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
                name: "modulo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    clave = table.Column<string>(type: "text", nullable: false),
                    numero = table.Column<short>(type: "smallint", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modulo", x => x.id);
                    table.CheckConstraint("modulo_numero_rango", "numero BETWEEN 1 AND 99");
                });

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
                name: "tipo_limite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    clave = table.Column<string>(type: "text", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    unidad = table.Column<string>(type: "text", nullable: false),
                    valor_defecto = table.Column<int>(type: "integer", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipo_limite", x => x.id);
                    table.CheckConstraint("tipo_limite_defecto", "valor_defecto >= -1");
                });

            migrationBuilder.CreateTable(
                name: "usuario",
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
                    table.PrimaryKey("pk_usuario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plan_modulo",
                columns: table => new
                {
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_modulo", x => new { x.plan_id, x.modulo_id });
                    table.ForeignKey(
                        name: "fk_plan_modulo_modulo",
                        column: x => x.modulo_id,
                        principalTable: "modulo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plan_modulo_plan",
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

            migrationBuilder.CreateTable(
                name: "tenant_limite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_limite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_limite", x => x.id);
                    table.CheckConstraint("tenant_limite_valor", "valor >= -1");
                    table.ForeignKey(
                        name: "fk_tenant_limite_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tenant_limite_tipo",
                        column: x => x.tipo_limite_id,
                        principalTable: "tipo_limite",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "modulo_clave_unica",
                table: "modulo",
                column: "clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "modulo_numero_unico",
                table: "modulo",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "plan_codigo_unico",
                table: "plan",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plan_modulo_modulo_id",
                table: "plan_modulo",
                column: "modulo_id");

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
                name: "ix_tenant_limite_tipo_limite_id",
                table: "tenant_limite",
                column: "tipo_limite_id");

            migrationBuilder.CreateIndex(
                name: "tenant_limite_unico",
                table: "tenant_limite",
                columns: new[] { "tenant_id", "tipo_limite_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "tipo_limite_clave_unica",
                table: "tipo_limite",
                column: "clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "usuario_correo_unico",
                table: "usuario",
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
                name: "plan_modulo");

            migrationBuilder.DropTable(
                name: "suscripcion");

            migrationBuilder.DropTable(
                name: "tenant_limite");

            migrationBuilder.DropTable(
                name: "usuario");

            migrationBuilder.DropTable(
                name: "modulo");

            migrationBuilder.DropTable(
                name: "plan");

            migrationBuilder.DropTable(
                name: "tenant");

            migrationBuilder.DropTable(
                name: "tipo_limite");
        }
    }
}
