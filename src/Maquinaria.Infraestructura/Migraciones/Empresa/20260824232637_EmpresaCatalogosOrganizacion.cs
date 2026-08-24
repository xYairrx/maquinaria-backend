using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maquinaria.Infraestructura.Migraciones.Empresa
{
    /// <inheritdoc />
    public partial class EmpresaCatalogosOrganizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categoria_equipo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "text", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categoria_equipo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "marca",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_marca", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "proveedor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "text", nullable: false),
                    razon_social = table.Column<string>(type: "text", nullable: false),
                    nombre_comercial = table.Column<string>(type: "text", nullable: true),
                    rfc = table.Column<string>(type: "text", nullable: true),
                    telefono = table.Column<string>(type: "text", nullable: true),
                    correo = table.Column<string>(type: "text", nullable: true),
                    domicilio = table.Column<string>(type: "text", nullable: true),
                    contacto = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proveedor", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "puesto",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "text", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_puesto", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sucursal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "text", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    domicilio = table.Column<string>(type: "text", nullable: true),
                    telefono = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sucursal", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tipo_equipo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "text", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipo_equipo", x => x.id);
                    table.ForeignKey(
                        name: "fk_tipo_equipo_categoria",
                        column: x => x.categoria_equipo_id,
                        principalTable: "categoria_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trabajador",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_empleado = table.Column<string>(type: "text", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    apellidos = table.Column<string>(type: "text", nullable: true),
                    puesto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    telefono = table.Column<string>(type: "text", nullable: true),
                    correo = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<short>(type: "smallint", nullable: false),
                    fecha_ingreso = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_baja = table.Column<DateOnly>(type: "date", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trabajador", x => x.id);
                    table.CheckConstraint("trabajador_baja_coherente", "(estado = 3) = (fecha_baja IS NOT NULL)");
                    table.CheckConstraint("trabajador_estado", "estado BETWEEN 1 AND 3");
                    table.CheckConstraint("trabajador_fechas", "fecha_baja IS NULL OR fecha_ingreso IS NULL OR fecha_baja >= fecha_ingreso");
                    table.ForeignKey(
                        name: "fk_trabajador_puesto",
                        column: x => x.puesto_id,
                        principalTable: "puesto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trabajador_sucursal",
                        column: x => x.sucursal_id,
                        principalTable: "sucursal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trabajador_usuario",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ubicacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "text", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    tipo = table.Column<short>(type: "smallint", nullable: false),
                    latitud = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    longitud = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ubicacion", x => x.id);
                    table.CheckConstraint("ubicacion_coordenadas", "(latitud IS NULL) = (longitud IS NULL)");
                    table.CheckConstraint("ubicacion_latitud", "latitud IS NULL OR latitud BETWEEN -90 AND 90");
                    table.CheckConstraint("ubicacion_longitud", "longitud IS NULL OR longitud BETWEEN -180 AND 180");
                    table.CheckConstraint("ubicacion_tipo", "tipo BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "fk_ubicacion_sucursal",
                        column: x => x.sucursal_id,
                        principalTable: "sucursal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "modelo_equipo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    marca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_equipo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    horas_entre_servicios = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modelo_equipo", x => x.id);
                    table.CheckConstraint("modelo_horas_servicio", "horas_entre_servicios IS NULL OR horas_entre_servicios > 0");
                    table.ForeignKey(
                        name: "fk_modelo_equipo_marca",
                        column: x => x.marca_id,
                        principalTable: "marca",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_modelo_equipo_tipo",
                        column: x => x.tipo_equipo_id,
                        principalTable: "tipo_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "categoria_equipo_codigo_unico",
                table: "categoria_equipo",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "marca_nombre_unico",
                table: "marca",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modelo_equipo_tipo_equipo_id",
                table: "modelo_equipo",
                column: "tipo_equipo_id");

            migrationBuilder.CreateIndex(
                name: "modelo_equipo_unico",
                table: "modelo_equipo",
                columns: new[] { "marca_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_proveedor_razon_social",
                table: "proveedor",
                column: "razon_social")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "proveedor_codigo_unico",
                table: "proveedor",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "puesto_codigo_unico",
                table: "puesto",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "sucursal_codigo_unico",
                table: "sucursal",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "tipo_equipo_codigo_unico",
                table: "tipo_equipo",
                columns: new[] { "categoria_equipo_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trabajador_estado",
                table: "trabajador",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_trabajador_puesto_id",
                table: "trabajador",
                column: "puesto_id");

            migrationBuilder.CreateIndex(
                name: "ix_trabajador_sucursal_id",
                table: "trabajador",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "trabajador_numero_unico",
                table: "trabajador",
                column: "numero_empleado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "trabajador_usuario_unico",
                table: "trabajador",
                column: "usuario_id",
                unique: true,
                filter: "usuario_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ubicacion_codigo_unico",
                table: "ubicacion",
                columns: new[] { "sucursal_id", "codigo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "modelo_equipo");

            migrationBuilder.DropTable(
                name: "proveedor");

            migrationBuilder.DropTable(
                name: "trabajador");

            migrationBuilder.DropTable(
                name: "ubicacion");

            migrationBuilder.DropTable(
                name: "marca");

            migrationBuilder.DropTable(
                name: "tipo_equipo");

            migrationBuilder.DropTable(
                name: "puesto");

            migrationBuilder.DropTable(
                name: "sucursal");

            migrationBuilder.DropTable(
                name: "categoria_equipo");
        }
    }
}
