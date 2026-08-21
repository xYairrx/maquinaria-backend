using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Maquinaria.Infraestructura.Migraciones.Central
{
    /// <inheritdoc />
    public partial class CentralAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auditoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    correlacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_correo = table.Column<string>(type: "text", nullable: true),
                    roles = table.Column<string[]>(type: "text[]", nullable: false),
                    origen = table.Column<string>(type: "text", nullable: false),
                    ip = table.Column<IPAddress>(type: "inet", nullable: true),
                    accion = table.Column<short>(type: "smallint", nullable: false),
                    entidad = table.Column<string>(type: "text", nullable: false),
                    entidad_id = table.Column<string>(type: "text", nullable: false),
                    valores_anteriores = table.Column<string>(type: "jsonb", nullable: true),
                    valores_nuevos = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria", x => x.id);
                    table.CheckConstraint("auditoria_accion", "accion BETWEEN 1 AND 8");
                });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_correlacion",
                table: "auditoria",
                column: "correlacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_entidad",
                table: "auditoria",
                columns: new[] { "entidad", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_fecha",
                table: "auditoria",
                column: "fecha_utc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_usuario",
                table: "auditoria",
                columns: new[] { "usuario_id", "fecha_utc" },
                descending: new[] { false, true });

            // --------------------------------------------------------------
            // ESCRITO A MANO. EF Core no sabe expresar triggers.
            // --------------------------------------------------------------
            //
            // Con el rol 'administrador' saltando la verificacion de permisos, esta
            // tabla es EL UNICO registro de lo que hizo. Un registro que el propio
            // auditado puede borrar no es un registro.
            //
            // El modelo multi-database elimino el rol de base de datos separado para
            // la aplicacion, asi que la via es un trigger. Mismo criterio que el
            // EXCLUDE de suscripcion: lo garantiza el motor, no la disciplina de
            // quien escriba el siguiente caso de uso.
            //
            // FOR EACH STATEMENT y no FOR EACH ROW: no hace falta inspeccionar
            // filas para rechazar la sentencia completa, y asi un DELETE de un
            // millon de filas se rechaza una vez y no un millon de veces.
            //
            // TRUNCATE VA EN LA LISTA, y no es redundante: un trigger de UPDATE y
            // DELETE no lo intercepta, asi que sin el un TRUNCATE auditoria vaciaria
            // la bitacora entera sin tocar el trigger. Los triggers de TRUNCATE solo
            // existen a nivel de sentencia, que es justo lo que este ya es.
            //
            // Esto no protege de un superusuario de Postgres. Si protege de la
            // aplicacion, de un ExecuteDelete distraido y del administrador de la
            // empresa, que son los tres casos reales.
            migrationBuilder.Sql("""
                CREATE FUNCTION auditoria_solo_insercion() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'auditoria es append-only: % rechazado', TG_OP;
                END $$ LANGUAGE plpgsql;

                CREATE TRIGGER auditoria_inmutable
                    BEFORE UPDATE OR DELETE OR TRUNCATE ON auditoria
                    FOR EACH STATEMENT EXECUTE FUNCTION auditoria_solo_insercion();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El trigger se va con la tabla, pero la FUNCION no: vive en el esquema.
            // Sin este DROP, un Down seguido de un Up fallaria con
            // "function already exists".
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS auditoria_solo_insercion() CASCADE;");
            migrationBuilder.DropTable(
                name: "auditoria");
        }
    }
}
