using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maquinaria.Infraestructura.Migraciones.Empresa
{
    /// <inheritdoc />
    public partial class EmpresaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "permiso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    clave = table.Column<string>(type: "text", nullable: false),
                    modulo = table.Column<string>(type: "text", nullable: false),
                    accion = table.Column<string>(type: "text", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permiso", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rol",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "text", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    es_sistema = table.Column<bool>(type: "boolean", nullable: false),
                    acceso_total = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rol", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correo = table.Column<string>(type: "text", nullable: false),
                    hash_contrasena = table.Column<string>(type: "text", nullable: true),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    apellidos = table.Column<string>(type: "text", nullable: true),
                    telefono = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<short>(type: "smallint", nullable: false),
                    debe_cambiar_contrasena = table.Column<bool>(type: "boolean", nullable: false),
                    ultimo_acceso_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario", x => x.id);
                    table.CheckConstraint("usuario_estado", "estado BETWEEN 1 AND 4");
                });

            migrationBuilder.CreateTable(
                name: "rol_permiso",
                columns: table => new
                {
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permiso_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rol_permiso", x => new { x.rol_id, x.permiso_id });
                    table.ForeignKey(
                        name: "fk_rol_permiso_permiso",
                        column: x => x.permiso_id,
                        principalTable: "permiso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rol_permiso_rol",
                        column: x => x.rol_id,
                        principalTable: "rol",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sesion_refresh",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash_token = table.Column<string>(type: "text", nullable: false),
                    expira_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revocado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reemplazado_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip = table.Column<IPAddress>(type: "inet", nullable: true),
                    agente_usuario = table.Column<string>(type: "text", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sesion_refresh", x => x.id);
                    table.CheckConstraint("sesion_refresh_vigencia", "expira_en > creado_en");
                    table.ForeignKey(
                        name: "fk_sesion_refresh_reemplazo",
                        column: x => x.reemplazado_por_id,
                        principalTable: "sesion_refresh",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sesion_refresh_usuario",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "token_acceso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposito = table.Column<short>(type: "smallint", nullable: false),
                    hash_token = table.Column<string>(type: "text", nullable: false),
                    expira_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    invalidado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    creado_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_token_acceso", x => x.id);
                    table.CheckConstraint("token_acceso_proposito", "proposito BETWEEN 1 AND 2");
                    table.CheckConstraint("token_acceso_vigencia", "expira_en > creado_en");
                    table.ForeignKey(
                        name: "fk_token_acceso_creado_por",
                        column: x => x.creado_por_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_token_acceso_usuario",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_rol",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_rol", x => new { x.usuario_id, x.rol_id });
                    table.ForeignKey(
                        name: "fk_usuario_rol_rol",
                        column: x => x.rol_id,
                        principalTable: "rol",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_usuario_rol_usuario",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_permiso_modulo",
                table: "permiso",
                column: "modulo");

            migrationBuilder.CreateIndex(
                name: "permiso_clave_unica",
                table: "permiso",
                column: "clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "rol_acceso_total_unico",
                table: "rol",
                column: "acceso_total",
                unique: true,
                filter: "acceso_total");

            migrationBuilder.CreateIndex(
                name: "rol_codigo_unico",
                table: "rol",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rol_permiso_permiso_id",
                table: "rol_permiso",
                column: "permiso_id");

            migrationBuilder.CreateIndex(
                name: "ix_sesion_refresh_reemplazado_por_id",
                table: "sesion_refresh",
                column: "reemplazado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_sesion_usuario_activa",
                table: "sesion_refresh",
                column: "usuario_id",
                filter: "revocado_en IS NULL");

            migrationBuilder.CreateIndex(
                name: "sesion_refresh_hash_unico",
                table: "sesion_refresh",
                column: "hash_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_token_acceso_creado_por_id",
                table: "token_acceso",
                column: "creado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_token_acceso_pendiente",
                table: "token_acceso",
                column: "usuario_id",
                filter: "usado_en IS NULL AND invalidado_en IS NULL");

            migrationBuilder.CreateIndex(
                name: "token_acceso_hash_unico",
                table: "token_acceso",
                column: "hash_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_estado",
                table: "usuario",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "usuario_correo_unico",
                table: "usuario",
                column: "correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_rol_rol_id",
                table: "usuario_rol",
                column: "rol_id");

            // --------------------------------------------------------------
            // ESCRITO A MANO. EF Core no sabe expresar triggers.
            // --------------------------------------------------------------
            //
            // El rol 'administrador' salta la verificacion de permisos por traer
            // acceso_total. Ese poder tiene que ser INMUTABLE, y garantizado por el
            // motor y no por la disciplina de quien escriba el siguiente caso de
            // uso: si se pudiera editar, alguien le apagaria el acceso y la empresa
            // quedaria encerrada por fuera; si se pudiera borrar, igual.
            //
            // Y como no se puede apagar, la regla "debe quedar al menos un rol con
            // acceso total" se cumple sola, sin necesidad de un constraint diferido
            // que la vigile.
            //
            // OJO AL WHEN: apunta a es_sistema AND acceso_total, no a es_sistema
            // solo. Los NUEVE roles semilla traen es_sistema, y el diseno dice que
            // cada empresa los renombra y ajusta. Bloquear los nueve romperia eso.
            //
            // Solo referencia OLD, nunca NEW: un trigger BEFORE DELETE no tiene NEW.
            //
            // Esto no protege de un superusuario de Postgres. Si protege de la
            // aplicacion, de un ExecuteUpdate distraido y del administrador de la
            // empresa, que son los tres casos reales.
            migrationBuilder.Sql("""
                CREATE FUNCTION rol_proteger_sistema() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION
                        'el rol de sistema con acceso total no se puede modificar ni borrar';
                END $$ LANGUAGE plpgsql;

                CREATE TRIGGER rol_sistema_inmutable
                    BEFORE UPDATE OR DELETE ON rol
                    FOR EACH ROW
                    WHEN (OLD.es_sistema AND OLD.acceso_total)
                    EXECUTE FUNCTION rol_proteger_sistema();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El trigger se va con la tabla rol, pero la FUNCION no: vive en el
            // esquema, no en la tabla. Hay que borrarla a mano o un Down seguido de
            // un Up fallaria con "function already exists".
            //
            // EF solo genera el Down de lo que el mismo produjo, y esto entro por
            // migrationBuilder.Sql.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS rol_proteger_sistema() CASCADE;");

            migrationBuilder.DropTable(
                name: "rol_permiso");

            migrationBuilder.DropTable(
                name: "sesion_refresh");

            migrationBuilder.DropTable(
                name: "token_acceso");

            migrationBuilder.DropTable(
                name: "usuario_rol");

            migrationBuilder.DropTable(
                name: "permiso");

            migrationBuilder.DropTable(
                name: "rol");

            migrationBuilder.DropTable(
                name: "usuario");
        }
    }
}
