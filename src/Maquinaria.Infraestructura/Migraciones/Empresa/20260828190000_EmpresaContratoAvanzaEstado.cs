using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maquinaria.Infraestructura.Migraciones.Empresa
{
    /// <summary>
    /// EL CONTRATO PODIA AUTORIZARSE Y YA NO PODIA MOVERSE MAS.
    ///
    /// <c>contrato_inmutable</c> se creo con <c>WHEN (OLD.estado &lt;&gt; 1)</c> y una funcion que
    /// SIEMPRE lanza. Eso rechaza cualquier UPDATE sobre un contrato fuera de Borrador **incluido
    /// el que solo mueve <c>estado</c>**, que es exactamente lo que hace
    /// <c>CambiarEstadoAsync</c>. Consecuencia: Borrador → Autorizado funcionaba y de ahi no se
    /// salia. Firmado y Terminado eran INALCANZABLES y <c>firmado_en</c> no podia tener valor
    /// nunca.
    ///
    /// Era una contradiccion dentro del propio backend: <c>ServicioContratosEf.Transiciones</c>
    /// declara <c>Autorizado → Firmado, Terminado</c> y <c>Firmado → Terminado</c>, tres filas que
    /// la base hacia imposibles. Se detecto el 2026-08-28 probando el ciclo desde la pantalla; no
    /// habia salido antes porque **el trigger no tenia prueba de esquema** y con la tabla vacia no
    /// se ejecuta.
    ///
    /// LO QUE CAMBIA: la comprobacion se mueve del <c>WHEN</c> al cuerpo de la funcion, para poder
    /// mirar QUE columna cambio. El contenido —folio, renta, cliente, fechas, deposito, notas—
    /// sigue congelado; lo unico que puede avanzar es el CICLO DE VIDA: <c>estado</c>,
    /// <c>firmado_en</c> y <c>actualizado_en</c>.
    ///
    /// LO QUE NO CAMBIA:
    /// - El DELETE sigue prohibido en cuanto el contrato sale de Borrador.
    /// - <c>contrato_clausula_inmutable</c> se queda igual: las clausulas SI estan congeladas por
    ///   completo fuera de Borrador, y eso era lo correcto desde el principio.
    /// - Que la transicion sea LEGAL lo sigue decidiendo <c>Transiciones</c> en el servicio. Este
    ///   trigger no valida el ciclo de vida, solo deja de bloquearlo: un salto invalido responde
    ///   409 antes de llegar a la base.
    /// </summary>
    public partial class EmpresaContratoAvanzaEstado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se reemplaza la FUNCION y se recrea el TRIGGER sin su `WHEN`, porque la condicion
            // ahora vive dentro. `CREATE OR REPLACE` no sirve para el trigger —no existe esa
            // forma en Postgres—, de ahi el DROP.
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS contrato_inmutable ON contrato;

                CREATE OR REPLACE FUNCTION contrato_proteger_autorizado() RETURNS trigger AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION
                            'El contrato % ya esta autorizado y no se puede borrar', OLD.folio;
                    END IF;

                    -- EL CONTENIDO SIGUE CONGELADO. Se comparan las columnas una por una en vez
                    -- de prohibir el UPDATE entero: asi el ciclo de vida puede avanzar sin abrir
                    -- la puerta a que alguien cambie el texto de un documento ya firmado, que es
                    -- lo que la garantia protege.
                    --
                    -- `IS DISTINCT FROM` y no `<>`: con `<>` una columna nula a los dos lados da
                    -- NULL, la condicion no se cumple y el cambio pasaria. `fecha_fin` y `notas`
                    -- son anulables.
                    IF (NEW.id, NEW.folio, NEW.renta_id, NEW.cliente_id,
                        NEW.fecha_inicio, NEW.fecha_fin, NEW.deposito, NEW.notas, NEW.creado_en)
                       IS DISTINCT FROM
                       (OLD.id, OLD.folio, OLD.renta_id, OLD.cliente_id,
                        OLD.fecha_inicio, OLD.fecha_fin, OLD.deposito, OLD.notas, OLD.creado_en)
                    THEN
                        RAISE EXCEPTION
                            'El contrato % ya esta autorizado: su contenido no se puede modificar',
                            OLD.folio;
                    END IF;

                    RETURN NEW;
                END $$ LANGUAGE plpgsql;

                -- EL `WHEN` SE QUEDA. Sigue filtrando por `OLD.estado <> 1`, que es correcto: un
                -- contrato en Borrador se edita libremente y la funcion no tiene nada que decir,
                -- asi que ni siquiera hace falta invocarla.
                --
                -- Lo que cambio no es a QUE FILAS aplica el trigger, sino que la funcion dejo de
                -- lanzar siempre y ahora mira QUE COLUMNA cambio.
                CREATE TRIGGER contrato_inmutable
                    BEFORE UPDATE OR DELETE ON contrato
                    FOR EACH ROW
                    WHEN (OLD.estado <> 1)
                    EXECUTE FUNCTION contrato_proteger_autorizado();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Se vuelve a la version que bloqueaba todo. Deja el esquema como estaba, con su
            // defecto incluido: eso es lo que un `Down` tiene que hacer.
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS contrato_inmutable ON contrato;

                CREATE OR REPLACE FUNCTION contrato_proteger_autorizado() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION
                        'El contrato % ya esta autorizado y no se puede modificar ni borrar',
                        OLD.folio;
                END $$ LANGUAGE plpgsql;

                CREATE TRIGGER contrato_inmutable
                    BEFORE UPDATE OR DELETE ON contrato
                    FOR EACH ROW
                    WHEN (OLD.estado <> 1)
                    EXECUTE FUNCTION contrato_proteger_autorizado();
                """);
        }
    }
}
