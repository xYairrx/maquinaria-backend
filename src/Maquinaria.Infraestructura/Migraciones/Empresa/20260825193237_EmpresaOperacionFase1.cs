using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maquinaria.Infraestructura.Migraciones.Empresa
{
    /// <inheritdoc />
    public partial class EmpresaOperacionFase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cliente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "text", nullable: false),
                    razon_social = table.Column<string>(type: "text", nullable: false),
                    nombre_comercial = table.Column<string>(type: "text", nullable: true),
                    rfc = table.Column<string>(type: "text", nullable: true),
                    telefono = table.Column<string>(type: "text", nullable: true),
                    correo = table.Column<string>(type: "text", nullable: true),
                    contacto_nombre = table.Column<string>(type: "text", nullable: true),
                    contacto_puesto = table.Column<string>(type: "text", nullable: true),
                    contacto_telefono = table.Column<string>(type: "text", nullable: true),
                    contacto_correo = table.Column<string>(type: "text", nullable: true),
                    calle = table.Column<string>(type: "text", nullable: true),
                    colonia = table.Column<string>(type: "text", nullable: true),
                    municipio = table.Column<string>(type: "text", nullable: true),
                    estado_prov = table.Column<string>(type: "text", nullable: true),
                    codigo_postal = table.Column<string>(type: "text", nullable: true),
                    pais = table.Column<string>(type: "text", nullable: false, defaultValue: "MX"),
                    latitud = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    longitud = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    limite_credito = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    dias_credito = table.Column<int>(type: "integer", nullable: false),
                    deposito_requerido = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    condiciones = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<short>(type: "smallint", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cliente", x => x.id);
                    table.CheckConstraint("cliente_coordenadas", "(latitud IS NULL) = (longitud IS NULL)");
                    table.CheckConstraint("cliente_credito", "limite_credito >= 0 AND dias_credito >= 0");
                    table.CheckConstraint("cliente_deposito", "deposito_requerido >= 0");
                    table.CheckConstraint("cliente_estado", "estado BETWEEN 1 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "equipo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_interno = table.Column<string>(type: "text", nullable: false),
                    modelo_equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ubicacion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_serie = table.Column<string>(type: "text", nullable: true),
                    anio = table.Column<int>(type: "integer", nullable: true),
                    estado = table.Column<short>(type: "smallint", nullable: false),
                    proposito = table.Column<short>(type: "smallint", nullable: false),
                    origen = table.Column<short>(type: "smallint", nullable: false),
                    fecha_adquisicion = table.Column<DateOnly>(type: "date", nullable: true),
                    costo_adquisicion = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    valor_actual = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    horometro = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    kilometraje = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    token_qr = table.Column<string>(type: "text", nullable: true),
                    notas = table.Column<string>(type: "text", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    eliminado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipo", x => x.id);
                    table.CheckConstraint("equipo_anio", "anio IS NULL OR anio BETWEEN 1900 AND 2200");
                    table.CheckConstraint("equipo_estado", "estado BETWEEN 1 AND 8");
                    table.CheckConstraint("equipo_lecturas", "COALESCE(horometro, 0) >= 0 AND COALESCE(kilometraje, 0) >= 0");
                    table.CheckConstraint("equipo_montos", "COALESCE(costo_adquisicion, 0) >= 0 AND COALESCE(valor_actual, 0) >= 0");
                    table.CheckConstraint("equipo_origen", "origen BETWEEN 1 AND 2");
                    table.CheckConstraint("equipo_proposito", "proposito BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "fk_equipo_modelo",
                        column: x => x.modelo_equipo_id,
                        principalTable: "modelo_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_equipo_tipo",
                        column: x => x.tipo_equipo_id,
                        principalTable: "tipo_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_equipo_ubicacion",
                        column: x => x.ubicacion_id,
                        principalTable: "ubicacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orden_compra",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    folio = table.Column<string>(type: "text", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trabajador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "current_date"),
                    estado = table.Column<short>(type: "smallint", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    impuestos = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    autorizada_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finalizada_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notas = table.Column<string>(type: "text", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orden_compra", x => x.id);
                    table.CheckConstraint("orden_compra_estado", "estado BETWEEN 1 AND 4");
                    table.CheckConstraint("orden_compra_finalizacion", "(estado = 3) = (finalizada_en IS NOT NULL)");
                    table.CheckConstraint("orden_compra_montos", "subtotal >= 0 AND impuestos >= 0 AND total >= 0");
                    table.ForeignKey(
                        name: "fk_orden_compra_proveedor",
                        column: x => x.proveedor_id,
                        principalTable: "proveedor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orden_compra_trabajador",
                        column: x => x.trabajador_id,
                        principalTable: "trabajador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cotizacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    folio = table.Column<string>(type: "text", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ubicacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trabajador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "current_date"),
                    vigencia_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<short>(type: "smallint", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    descuento = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    impuestos = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    notas = table.Column<string>(type: "text", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotizacion", x => x.id);
                    table.CheckConstraint("cotizacion_estado", "estado BETWEEN 1 AND 7");
                    table.CheckConstraint("cotizacion_montos", "subtotal >= 0 AND descuento >= 0 AND impuestos >= 0 AND total >= 0");
                    table.ForeignKey(
                        name: "fk_cotizacion_cliente",
                        column: x => x.cliente_id,
                        principalTable: "cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cotizacion_trabajador",
                        column: x => x.trabajador_id,
                        principalTable: "trabajador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cotizacion_ubicacion",
                        column: x => x.ubicacion_id,
                        principalTable: "ubicacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orden_venta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    folio = table.Column<string>(type: "text", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trabajador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "current_date"),
                    estado = table.Column<short>(type: "smallint", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    descuento = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    impuestos = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    autorizada_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finalizada_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notas = table.Column<string>(type: "text", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orden_venta", x => x.id);
                    table.CheckConstraint("orden_venta_estado", "estado BETWEEN 1 AND 4");
                    table.CheckConstraint("orden_venta_finalizacion", "(estado = 3) = (finalizada_en IS NOT NULL)");
                    table.CheckConstraint("orden_venta_montos", "subtotal >= 0 AND descuento >= 0 AND impuestos >= 0 AND total >= 0");
                    table.ForeignKey(
                        name: "fk_orden_venta_cliente",
                        column: x => x.cliente_id,
                        principalTable: "cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orden_venta_trabajador",
                        column: x => x.trabajador_id,
                        principalTable: "trabajador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "equipo_archivo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    archivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<short>(type: "smallint", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipo_archivo", x => x.id);
                    table.CheckConstraint("equipo_archivo_tipo", "tipo BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "fk_equipo_archivo_archivo",
                        column: x => x.archivo_id,
                        principalTable: "archivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_equipo_archivo_equipo",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "equipo_tarifa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarifa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    precio = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    moneda = table.Column<string>(type: "text", nullable: false, defaultValue: "MXN"),
                    vigencia_desde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    vigencia_hasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipo_tarifa", x => x.id);
                    table.CheckConstraint("equipo_tarifa_moneda", "length(moneda) = 3");
                    table.CheckConstraint("equipo_tarifa_precio", "precio >= 0");
                    table.CheckConstraint("equipo_tarifa_vigencia", "vigencia_hasta IS NULL OR vigencia_hasta > vigencia_desde");
                    table.ForeignKey(
                        name: "fk_equipo_tarifa_cliente",
                        column: x => x.cliente_id,
                        principalTable: "cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_equipo_tarifa_equipo",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_equipo_tarifa_tarifa",
                        column: x => x.tarifa_id,
                        principalTable: "tarifa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ocupacion_equipo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo = table.Column<short>(type: "smallint", nullable: false),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nota = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ocupacion_equipo", x => x.id);
                    table.CheckConstraint("ocupacion_motivo", "motivo BETWEEN 1 AND 6");
                    table.CheckConstraint("ocupacion_periodo", "fin IS NULL OR fin > inicio");
                    table.ForeignKey(
                        name: "fk_ocupacion_equipo",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transferencia_equipo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trabajador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    motivo = table.Column<string>(type: "text", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transferencia_equipo", x => x.id);
                    table.CheckConstraint("transferencia_distinta", "origen_id <> destino_id");
                    table.ForeignKey(
                        name: "fk_transferencia_destino",
                        column: x => x.destino_id,
                        principalTable: "ubicacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transferencia_equipo",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transferencia_origen",
                        column: x => x.origen_id,
                        principalTable: "ubicacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transferencia_trabajador",
                        column: x => x.trabajador_id,
                        principalTable: "trabajador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orden_compra_detalle",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modelo_equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_serie = table.Column<string>(type: "text", nullable: true),
                    anio = table.Column<int>(type: "integer", nullable: true),
                    cantidad = table.Column<int>(type: "integer", nullable: false),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    importe = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orden_compra_detalle", x => x.id);
                    table.CheckConstraint("orden_compra_detalle_cantidad", "cantidad > 0");
                    table.CheckConstraint("orden_compra_detalle_montos", "costo_unitario >= 0 AND importe >= 0");
                    table.ForeignKey(
                        name: "fk_orden_compra_detalle_equipo",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orden_compra_detalle_modelo",
                        column: x => x.modelo_equipo_id,
                        principalTable: "modelo_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orden_compra_detalle_orden",
                        column: x => x.orden_compra_id,
                        principalTable: "orden_compra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cotizacion_linea",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cotizacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarifa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_equipo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    cantidad = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    importe = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotizacion_linea", x => x.id);
                    table.CheckConstraint("cotizacion_linea_cantidad", "cantidad > 0");
                    table.CheckConstraint("cotizacion_linea_montos", "precio_unitario >= 0 AND importe >= 0");
                    table.ForeignKey(
                        name: "fk_cotizacion_linea_cotizacion",
                        column: x => x.cotizacion_id,
                        principalTable: "cotizacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cotizacion_linea_equipo",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cotizacion_linea_tarifa",
                        column: x => x.tarifa_id,
                        principalTable: "tarifa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cotizacion_linea_tipo",
                        column: x => x.tipo_equipo_id,
                        principalTable: "tipo_equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "renta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    folio = table.Column<string>(type: "text", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cotizacion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trabajador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fin = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<short>(type: "smallint", nullable: false),
                    lugar_descripcion = table.Column<string>(type: "text", nullable: false),
                    lugar_calle = table.Column<string>(type: "text", nullable: true),
                    lugar_colonia = table.Column<string>(type: "text", nullable: true),
                    lugar_municipio = table.Column<string>(type: "text", nullable: true),
                    lugar_estado_prov = table.Column<string>(type: "text", nullable: true),
                    lugar_codigo_postal = table.Column<string>(type: "text", nullable: true),
                    lugar_latitud = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    lugar_longitud = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    lugar_contacto = table.Column<string>(type: "text", nullable: true),
                    lugar_telefono = table.Column<string>(type: "text", nullable: true),
                    deposito = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    anticipo = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    descuento = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    impuestos = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    saldo = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    notas = table.Column<string>(type: "text", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_renta", x => x.id);
                    table.CheckConstraint("renta_estado", "estado BETWEEN 1 AND 10");
                    table.CheckConstraint("renta_lugar_coordenadas", "(lugar_latitud IS NULL) = (lugar_longitud IS NULL)");
                    table.CheckConstraint("renta_lugar_no_vacio", "length(btrim(lugar_descripcion)) > 0");
                    table.CheckConstraint("renta_montos", "deposito >= 0 AND anticipo >= 0 AND subtotal >= 0 AND descuento >= 0 AND impuestos >= 0 AND total >= 0");
                    table.CheckConstraint("renta_periodo", "fin > inicio");
                    table.ForeignKey(
                        name: "fk_renta_cliente",
                        column: x => x.cliente_id,
                        principalTable: "cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_renta_cotizacion",
                        column: x => x.cotizacion_id,
                        principalTable: "cotizacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_renta_trabajador",
                        column: x => x.trabajador_id,
                        principalTable: "trabajador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orden_venta_detalle",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_venta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    importe = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orden_venta_detalle", x => x.id);
                    table.CheckConstraint("orden_venta_detalle_montos", "precio_unitario >= 0 AND importe >= 0");
                    table.ForeignKey(
                        name: "fk_orden_venta_detalle_equipo",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orden_venta_detalle_orden",
                        column: x => x.orden_venta_id,
                        principalTable: "orden_venta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contrato",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    folio = table.Column<string>(type: "text", nullable: false),
                    renta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    deposito = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    estado = table.Column<short>(type: "smallint", nullable: false),
                    firmado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notas = table.Column<string>(type: "text", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contrato", x => x.id);
                    table.CheckConstraint("contrato_deposito", "deposito >= 0");
                    table.CheckConstraint("contrato_estado", "estado BETWEEN 1 AND 4");
                    table.CheckConstraint("contrato_fechas", "fecha_fin IS NULL OR fecha_fin >= fecha_inicio");
                    table.ForeignKey(
                        name: "fk_contrato_cliente",
                        column: x => x.cliente_id,
                        principalTable: "cliente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_contrato_renta",
                        column: x => x.renta_id,
                        principalTable: "renta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "extension_renta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    renta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fin_anterior = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fin_nuevo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    motivo = table.Column<string>(type: "text", nullable: true),
                    trabajador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_extension_renta", x => x.id);
                    table.CheckConstraint("extension_avanza", "fin_nuevo > fin_anterior");
                    table.ForeignKey(
                        name: "fk_extension_renta",
                        column: x => x.renta_id,
                        principalTable: "renta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_extension_trabajador",
                        column: x => x.trabajador_id,
                        principalTable: "trabajador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "renta_concepto",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    renta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarifa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trabajador_id = table.Column<Guid>(type: "uuid", nullable: true),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    cantidad = table.Column<decimal>(type: "numeric(12,2)", nullable: false, defaultValue: 1m),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    costo = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    importe = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_renta_concepto", x => x.id);
                    table.CheckConstraint("renta_concepto_cantidad", "cantidad > 0");
                    table.CheckConstraint("renta_concepto_montos", "precio_unitario >= 0 AND importe >= 0 AND COALESCE(costo, 0) >= 0");
                    table.ForeignKey(
                        name: "fk_renta_concepto_renta",
                        column: x => x.renta_id,
                        principalTable: "renta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_renta_concepto_tarifa",
                        column: x => x.tarifa_id,
                        principalTable: "tarifa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_renta_concepto_trabajador",
                        column: x => x.trabajador_id,
                        principalTable: "trabajador",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "renta_linea",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    renta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarifa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    horas_incluidas = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    importe = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    horometro_salida = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    horometro_devolucion = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_renta_linea", x => x.id);
                    table.CheckConstraint("renta_linea_cantidad", "cantidad > 0");
                    table.CheckConstraint("renta_linea_montos", "precio_unitario >= 0 AND importe >= 0");
                    table.ForeignKey(
                        name: "fk_renta_linea_equipo",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_renta_linea_renta",
                        column: x => x.renta_id,
                        principalTable: "renta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_renta_linea_tarifa",
                        column: x => x.tarifa_id,
                        principalTable: "tarifa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contrato_clausula",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clausula_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "text", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contrato_clausula", x => x.id);
                    table.CheckConstraint("contrato_clausula_texto", "length(btrim(texto)) > 0");
                    table.ForeignKey(
                        name: "fk_contrato_clausula_clausula",
                        column: x => x.clausula_id,
                        principalTable: "clausula",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_contrato_clausula_contrato",
                        column: x => x.contrato_id,
                        principalTable: "contrato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "cliente_codigo_unico",
                table: "cliente",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cliente_estado",
                table: "cliente",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_cliente_razon_social",
                table: "cliente",
                column: "razon_social")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "contrato_folio_unico",
                table: "contrato",
                column: "folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "contrato_renta_unica",
                table: "contrato",
                column: "renta_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contrato_cliente",
                table: "contrato",
                columns: new[] { "cliente_id", "fecha_inicio" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "contrato_clausula_orden_unico",
                table: "contrato_clausula",
                columns: new[] { "contrato_id", "orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contrato_clausula_clausula_id",
                table: "contrato_clausula",
                column: "clausula_id");

            migrationBuilder.CreateIndex(
                name: "cotizacion_folio_unico",
                table: "cotizacion",
                column: "folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cotizacion_cliente",
                table: "cotizacion",
                columns: new[] { "cliente_id", "fecha" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_cotizacion_estado",
                table: "cotizacion",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_cotizacion_trabajador_id",
                table: "cotizacion",
                column: "trabajador_id");

            migrationBuilder.CreateIndex(
                name: "ix_cotizacion_ubicacion_id",
                table: "cotizacion",
                column: "ubicacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_cotizacion_linea_cotizacion",
                table: "cotizacion_linea",
                column: "cotizacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_cotizacion_linea_equipo_id",
                table: "cotizacion_linea",
                column: "equipo_id");

            migrationBuilder.CreateIndex(
                name: "ix_cotizacion_linea_tarifa_id",
                table: "cotizacion_linea",
                column: "tarifa_id");

            migrationBuilder.CreateIndex(
                name: "ix_cotizacion_linea_tipo_equipo_id",
                table: "cotizacion_linea",
                column: "tipo_equipo_id");

            migrationBuilder.CreateIndex(
                name: "equipo_codigo_unico",
                table: "equipo",
                column: "codigo_interno",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "equipo_token_qr_unico",
                table: "equipo",
                column: "token_qr",
                unique: true,
                filter: "token_qr IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_equipo_estado",
                table: "equipo",
                column: "estado",
                filter: "eliminado_en IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_equipo_modelo",
                table: "equipo",
                column: "modelo_equipo_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipo_serie",
                table: "equipo",
                column: "numero_serie")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_equipo_tipo_equipo_id",
                table: "equipo",
                column: "tipo_equipo_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipo_ubicacion",
                table: "equipo",
                column: "ubicacion_id",
                filter: "eliminado_en IS NULL");

            migrationBuilder.CreateIndex(
                name: "equipo_archivo_unico",
                table: "equipo_archivo",
                columns: new[] { "equipo_id", "archivo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipo_archivo_archivo_id",
                table: "equipo_archivo",
                column: "archivo_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipo_tarifa_cliente_id",
                table: "equipo_tarifa",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipo_tarifa_equipo_id",
                table: "equipo_tarifa",
                column: "equipo_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipo_tarifa_tarifa_id",
                table: "equipo_tarifa",
                column: "tarifa_id");

            migrationBuilder.CreateIndex(
                name: "ix_extension_renta_renta_id",
                table: "extension_renta",
                column: "renta_id");

            migrationBuilder.CreateIndex(
                name: "ix_extension_renta_trabajador_id",
                table: "extension_renta",
                column: "trabajador_id");

            migrationBuilder.CreateIndex(
                name: "ix_ocupacion_equipo",
                table: "ocupacion_equipo",
                columns: new[] { "equipo_id", "inicio" },
                filter: "activo");

            migrationBuilder.CreateIndex(
                name: "ix_orden_compra_estado",
                table: "orden_compra",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_orden_compra_proveedor_id",
                table: "orden_compra",
                column: "proveedor_id");

            migrationBuilder.CreateIndex(
                name: "ix_orden_compra_trabajador_id",
                table: "orden_compra",
                column: "trabajador_id");

            migrationBuilder.CreateIndex(
                name: "orden_compra_folio_unico",
                table: "orden_compra",
                column: "folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orden_compra_detalle_modelo_equipo_id",
                table: "orden_compra_detalle",
                column: "modelo_equipo_id");

            migrationBuilder.CreateIndex(
                name: "ix_orden_compra_detalle_orden",
                table: "orden_compra_detalle",
                column: "orden_compra_id");

            migrationBuilder.CreateIndex(
                name: "orden_compra_detalle_equipo_unico",
                table: "orden_compra_detalle",
                column: "equipo_id",
                unique: true,
                filter: "equipo_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_orden_venta_cliente_id",
                table: "orden_venta",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_orden_venta_estado",
                table: "orden_venta",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_orden_venta_trabajador_id",
                table: "orden_venta",
                column: "trabajador_id");

            migrationBuilder.CreateIndex(
                name: "orden_venta_folio_unico",
                table: "orden_venta",
                column: "folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orden_venta_detalle_equipo_id",
                table: "orden_venta_detalle",
                column: "equipo_id");

            migrationBuilder.CreateIndex(
                name: "ix_orden_venta_detalle_orden",
                table: "orden_venta_detalle",
                column: "orden_venta_id");

            migrationBuilder.CreateIndex(
                name: "orden_venta_detalle_unico",
                table: "orden_venta_detalle",
                columns: new[] { "orden_venta_id", "equipo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_renta_cliente",
                table: "renta",
                columns: new[] { "cliente_id", "inicio" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_renta_cotizacion_id",
                table: "renta",
                column: "cotizacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_renta_estado",
                table: "renta",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_renta_trabajador_id",
                table: "renta",
                column: "trabajador_id");

            migrationBuilder.CreateIndex(
                name: "renta_folio_unico",
                table: "renta",
                column: "folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_renta_concepto_renta",
                table: "renta_concepto",
                column: "renta_id");

            migrationBuilder.CreateIndex(
                name: "ix_renta_concepto_tarifa_id",
                table: "renta_concepto",
                column: "tarifa_id");

            migrationBuilder.CreateIndex(
                name: "ix_renta_concepto_trabajador_id",
                table: "renta_concepto",
                column: "trabajador_id");

            migrationBuilder.CreateIndex(
                name: "ix_renta_linea_equipo",
                table: "renta_linea",
                column: "equipo_id");

            migrationBuilder.CreateIndex(
                name: "ix_renta_linea_renta",
                table: "renta_linea",
                column: "renta_id");

            migrationBuilder.CreateIndex(
                name: "ix_renta_linea_tarifa_id",
                table: "renta_linea",
                column: "tarifa_id");

            migrationBuilder.CreateIndex(
                name: "renta_linea_unica",
                table: "renta_linea",
                columns: new[] { "renta_id", "equipo_id", "tarifa_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transferencia_equipo",
                table: "transferencia_equipo",
                columns: new[] { "equipo_id", "fecha" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_transferencia_equipo_destino_id",
                table: "transferencia_equipo",
                column: "destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_transferencia_equipo_origen_id",
                table: "transferencia_equipo",
                column: "origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_transferencia_equipo_trabajador_id",
                table: "transferencia_equipo",
                column: "trabajador_id");

            // ================================================================
            // Lo que EF Core no sabe expresar, y que es justo donde viven las
            // garantias que pediste. Todo esto va en SQL crudo A PROPOSITO: no es
            // que falte soporte y se parchee, es que estas reglas tienen que vivir
            // en el motor y no en la aplicacion.
            // ================================================================

            // NO RENTAR LA MISMA MAQUINA EN FECHAS QUE SE TRASLAPAN.
            //
            // Es tu requisito literal, y esta es la linea que lo cumple. Postgres
            // rechaza cualquier fila cuyo periodo se cruce con otra ACTIVA del mismo
            // equipo.
            //
            // POR QUE NO SE VALIDA EN C#: dos usuarios que reservan la misma maquina
            // en el mismo instante pasan los dos la comprobacion "esta libre?" y los
            // dos insertan. Es una carrera, y no se arregla con mas codigo. Aqui el
            // arbitro es el motor, y no hay dos peticiones que puedan colarse.
            //
            // El WHERE (activo) es lo que permite cancelar: una ocupacion inactiva
            // deja de competir por el calendario sin borrar el rastro de que existio.
            migrationBuilder.Sql("""
                ALTER TABLE ocupacion_equipo
                    ADD CONSTRAINT ocupacion_sin_traslape
                    EXCLUDE USING gist (
                        equipo_id WITH =,
                        tstzrange(inicio, fin) WITH &&
                    ) WHERE (activo);
                """);

            // UN SOLO PRECIO VIGENTE por concepto, maquina y cliente.
            //
            // El COALESCE convierte el cliente nulo —el precio de lista— en un uuid
            // de ceros, porque en una restriccion EXCLUDE dos nulos NO se consideran
            // iguales y sin esto se podrian meter dos precios de lista solapados.
            migrationBuilder.Sql("""
                ALTER TABLE equipo_tarifa
                    ADD CONSTRAINT equipo_tarifa_sin_traslape
                    EXCLUDE USING gist (
                        equipo_id WITH =,
                        tarifa_id WITH =,
                        COALESCE(cliente_id, '00000000-0000-0000-0000-000000000000'::uuid) WITH =,
                        tstzrange(vigencia_desde, vigencia_hasta) WITH &&
                    );
                """);

            // Dos funciones que leen las columnas GENERADAS de ubicacion.
            //
            // Existen para no repetir la subconsulta en cada disparador. STABLE y no
            // VOLATILE: dentro de una misma sentencia el resultado no cambia, y eso
            // le permite al planificador no llamarlas fila por fila.
            migrationBuilder.Sql("""
                CREATE FUNCTION ubicacion_almacena(p_id uuid) RETURNS boolean AS $$
                    SELECT COALESCE(
                        (SELECT almacena_equipo FROM ubicacion WHERE id = p_id), false);
                $$ LANGUAGE sql STABLE;

                CREATE FUNCTION ubicacion_administra(p_id uuid) RETURNS boolean AS $$
                    SELECT COALESCE(
                        (SELECT es_administrativa FROM ubicacion WHERE id = p_id), false);
                $$ LANGUAGE sql STABLE;
                """);

            // EL EQUIPO SOLO SE RESGUARDA EN BODEGA O PATIO, nunca en una sucursal.
            //
            // Es tu regla: "bodega unicamente se guardan las maquinas, sucursal es
            // para administracion, y patios son una combinacion de ambas".
            //
            // POR QUE UN DISPARADOR Y NO UN CHECK: la regla depende del TIPO DE OTRA
            // FILA —la ubicacion—, y un CHECK solo puede mirar la fila que se esta
            // escribiendo. Es la unica herramienta que alcanza.
            migrationBuilder.Sql("""
                CREATE FUNCTION equipo_exigir_almacen() RETURNS trigger AS $$
                BEGIN
                    IF NEW.ubicacion_id IS NOT NULL
                       AND NOT ubicacion_almacena(NEW.ubicacion_id) THEN
                        RAISE EXCEPTION
                            'El equipo solo puede resguardarse en una bodega o un patio (ubicacion %)',
                            NEW.ubicacion_id;
                    END IF;

                    RETURN NEW;
                END $$ LANGUAGE plpgsql;

                CREATE TRIGGER equipo_ubicacion_almacen
                    BEFORE INSERT OR UPDATE OF ubicacion_id ON equipo
                    FOR EACH ROW EXECUTE FUNCTION equipo_exigir_almacen();
                """);

            // LOS TRASPASOS VAN ENTRE SITIOS QUE RESGUARDAN EQUIPO.
            // "Se deben permitir traspasos de equipos entre bodegas y patios": ni el
            // origen ni el destino pueden ser una sucursal.
            migrationBuilder.Sql("""
                CREATE FUNCTION transferencia_exigir_almacenes() RETURNS trigger AS $$
                BEGIN
                    IF NOT ubicacion_almacena(NEW.origen_id) THEN
                        RAISE EXCEPTION 'El origen % no resguarda equipo', NEW.origen_id;
                    END IF;

                    IF NOT ubicacion_almacena(NEW.destino_id) THEN
                        RAISE EXCEPTION 'El destino % no resguarda equipo', NEW.destino_id;
                    END IF;

                    RETURN NEW;
                END $$ LANGUAGE plpgsql;

                CREATE TRIGGER transferencia_ubicaciones_almacen
                    BEFORE INSERT OR UPDATE OF origen_id, destino_id ON transferencia_equipo
                    FOR EACH ROW EXECUTE FUNCTION transferencia_exigir_almacenes();
                """);

            // SE COTIZA DESDE UNA SUCURSAL O UN PATIO, nunca desde una bodega.
            // La otra mitad de la misma regla, en el otro sentido.
            migrationBuilder.Sql("""
                CREATE FUNCTION cotizacion_exigir_administrativa() RETURNS trigger AS $$
                BEGIN
                    IF NOT ubicacion_administra(NEW.ubicacion_id) THEN
                        RAISE EXCEPTION
                            'Una cotizacion solo puede emitirse desde una sucursal o un patio (ubicacion %)',
                            NEW.ubicacion_id;
                    END IF;

                    RETURN NEW;
                END $$ LANGUAGE plpgsql;

                CREATE TRIGGER cotizacion_ubicacion_administrativa
                    BEFORE INSERT OR UPDATE OF ubicacion_id ON cotizacion
                    FOR EACH ROW EXECUTE FUNCTION cotizacion_exigir_administrativa();
                """);

            // UN CONTRATO AUTORIZADO NO SE TOCA. Lo pediste asi: "una vez el contrato
            // es autorizado ya no se puede editar".
            //
            // Cubre UPDATE y DELETE, y el WHEN deja pasar los borradores. Un contrato
            // que cambia despues de firmado no es un contrato, y esa garantia no puede
            // depender de que ningun caso de uso se equivoque.
            migrationBuilder.Sql("""
                CREATE FUNCTION contrato_proteger_autorizado() RETURNS trigger AS $$
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

            // Y SUS CLAUSULAS TAMPOCO.
            //
            // Sin esto, la proteccion del contrato seria una fachada: el contrato
            // quedaria intacto mientras alguien le cambia el texto de las clausulas,
            // que es precisamente lo que obliga.
            //
            // Cubre tambien INSERT —no se agregan clausulas a un contrato ya
            // autorizado— y por eso mira NEW y OLD con COALESCE.
            migrationBuilder.Sql("""
                CREATE FUNCTION contrato_clausula_proteger() RETURNS trigger AS $$
                DECLARE
                    v_estado   smallint;
                    v_contrato uuid;
                BEGIN
                    v_contrato := COALESCE(NEW.contrato_id, OLD.contrato_id);

                    SELECT estado INTO v_estado FROM contrato WHERE id = v_contrato;

                    IF v_estado IS NOT NULL AND v_estado <> 1 THEN
                        RAISE EXCEPTION
                            'Las clausulas de un contrato autorizado no se pueden modificar';
                    END IF;

                    RETURN COALESCE(NEW, OLD);
                END $$ LANGUAGE plpgsql;

                CREATE TRIGGER contrato_clausula_inmutable
                    BEFORE INSERT OR UPDATE OR DELETE ON contrato_clausula
                    FOR EACH ROW EXECUTE FUNCTION contrato_clausula_proteger();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // LAS FUNCIONES SI HAY QUE BORRARLAS A MANO. Las restricciones EXCLUDE y
            // los disparadores pertenecen a sus tablas y se van con el DROP TABLE; una
            // FUNCION vive en el esquema, no en la tabla, y sobreviviria. Volver a
            // aplicar la migracion fallaria entonces con "function already exists".
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS contrato_clausula_proteger();
                DROP FUNCTION IF EXISTS contrato_proteger_autorizado();
                DROP FUNCTION IF EXISTS cotizacion_exigir_administrativa();
                DROP FUNCTION IF EXISTS transferencia_exigir_almacenes();
                DROP FUNCTION IF EXISTS equipo_exigir_almacen();
                DROP FUNCTION IF EXISTS ubicacion_administra(uuid);
                DROP FUNCTION IF EXISTS ubicacion_almacena(uuid);
                """);

            migrationBuilder.DropTable(
                name: "contrato_clausula");

            migrationBuilder.DropTable(
                name: "cotizacion_linea");

            migrationBuilder.DropTable(
                name: "equipo_archivo");

            migrationBuilder.DropTable(
                name: "equipo_tarifa");

            migrationBuilder.DropTable(
                name: "extension_renta");

            migrationBuilder.DropTable(
                name: "ocupacion_equipo");

            migrationBuilder.DropTable(
                name: "orden_compra_detalle");

            migrationBuilder.DropTable(
                name: "orden_venta_detalle");

            migrationBuilder.DropTable(
                name: "renta_concepto");

            migrationBuilder.DropTable(
                name: "renta_linea");

            migrationBuilder.DropTable(
                name: "transferencia_equipo");

            migrationBuilder.DropTable(
                name: "contrato");

            migrationBuilder.DropTable(
                name: "orden_compra");

            migrationBuilder.DropTable(
                name: "orden_venta");

            migrationBuilder.DropTable(
                name: "equipo");

            migrationBuilder.DropTable(
                name: "renta");

            migrationBuilder.DropTable(
                name: "cotizacion");

            migrationBuilder.DropTable(
                name: "cliente");
        }
    }
}
