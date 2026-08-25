CREATE TABLE categoria_equipo (
    id          uuid        PRIMARY KEY,
    codigo      text        NOT NULL,
    nombre      text        NOT NULL,
    descripcion text        NULL,
    activo      boolean     NOT NULL,
    creado_en   timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT categoria_equipo_codigo_unico UNIQUE (codigo)
);

CREATE TABLE tipo_equipo (
    id                  uuid        PRIMARY KEY,
    categoria_equipo_id uuid        NOT NULL,
    codigo              text        NOT NULL,
    nombre              text        NOT NULL,
    activo              boolean     NOT NULL,
    creado_en           timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT tipo_equipo_codigo_unico UNIQUE (categoria_equipo_id, codigo),
    CONSTRAINT fk_tipo_equipo_categoria FOREIGN KEY (categoria_equipo_id)
        REFERENCES categoria_equipo (id)
);

CREATE TABLE marca (
    id        uuid        PRIMARY KEY,
    nombre    text        NOT NULL,
    activo    boolean     NOT NULL,
    creado_en timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT marca_nombre_unico UNIQUE (nombre)
);

CREATE TABLE modelo_equipo (
    id                    uuid        PRIMARY KEY,
    marca_id              uuid        NOT NULL,
    tipo_equipo_id        uuid        NULL,
    nombre                text        NOT NULL,
    descripcion           text        NULL,
    horas_entre_servicios int         NULL,
    activo                boolean     NOT NULL,
    creado_en             timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT modelo_equipo_unico UNIQUE (marca_id, nombre),
    CONSTRAINT modelo_horas_servicio
        CHECK (horas_entre_servicios IS NULL OR horas_entre_servicios > 0),
    CONSTRAINT fk_modelo_equipo_marca FOREIGN KEY (marca_id)
        REFERENCES marca (id),
    CONSTRAINT fk_modelo_equipo_tipo FOREIGN KEY (tipo_equipo_id)
        REFERENCES tipo_equipo (id)
);

CREATE TABLE ubicacion (
    id        uuid         PRIMARY KEY,
    codigo    text         NOT NULL,
    nombre    text         NOT NULL,
    tipo      smallint     NOT NULL,
    domicilio text         NULL,
    telefono  text         NULL,
    latitud   numeric(9,6) NULL,
    longitud  numeric(9,6) NULL,
    activo    boolean      NOT NULL,
    creado_en timestamptz  NOT NULL DEFAULT now(),

    almacena_equipo   boolean NOT NULL GENERATED ALWAYS AS (tipo IN (1, 3)) STORED,
    es_administrativa boolean NOT NULL GENERATED ALWAYS AS (tipo IN (2, 3)) STORED,

    CONSTRAINT ubicacion_codigo_unico UNIQUE (codigo),
    CONSTRAINT ubicacion_tipo CHECK (tipo BETWEEN 1 AND 3),
    CONSTRAINT ubicacion_coordenadas CHECK ((latitud IS NULL) = (longitud IS NULL)),
    CONSTRAINT ubicacion_latitud CHECK (latitud IS NULL OR latitud BETWEEN -90 AND 90),
    CONSTRAINT ubicacion_longitud CHECK (longitud IS NULL OR longitud BETWEEN -180 AND 180)
);

CREATE INDEX ix_ubicacion_tipo ON ubicacion (tipo);
CREATE INDEX ix_ubicacion_almacena ON ubicacion (nombre) WHERE almacena_equipo AND activo;
CREATE INDEX ix_ubicacion_administrativa ON ubicacion (nombre) WHERE es_administrativa AND activo;

CREATE TABLE puesto (
    id          uuid        PRIMARY KEY,
    codigo      text        NOT NULL,
    nombre      text        NOT NULL,
    descripcion text        NULL,
    activo      boolean     NOT NULL,
    creado_en   timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT puesto_codigo_unico UNIQUE (codigo)
);

CREATE TABLE trabajador (
    id              uuid        PRIMARY KEY,
    numero_empleado text        NOT NULL,
    nombre          text        NOT NULL,
    apellidos       text        NULL,
    puesto_id       uuid        NOT NULL,
    ubicacion_id    uuid        NULL,
    usuario_id      uuid        NULL,
    telefono        text        NULL,
    correo          text        NULL,
    estado          smallint    NOT NULL,
    fecha_ingreso   date        NULL,
    fecha_baja      date        NULL,
    creado_en       timestamptz NOT NULL DEFAULT now(),
    actualizado_en  timestamptz NULL,

    CONSTRAINT trabajador_numero_unico UNIQUE (numero_empleado),
    CONSTRAINT trabajador_estado CHECK (estado BETWEEN 1 AND 3),
    CONSTRAINT trabajador_baja_coherente CHECK ((estado = 3) = (fecha_baja IS NOT NULL)),
    CONSTRAINT trabajador_fechas
        CHECK (fecha_baja IS NULL OR fecha_ingreso IS NULL OR fecha_baja >= fecha_ingreso),
    CONSTRAINT fk_trabajador_puesto FOREIGN KEY (puesto_id)
        REFERENCES puesto (id),
    CONSTRAINT fk_trabajador_ubicacion FOREIGN KEY (ubicacion_id)
        REFERENCES ubicacion (id),
    CONSTRAINT fk_trabajador_usuario FOREIGN KEY (usuario_id)
        REFERENCES usuario (id)
);

CREATE INDEX ix_trabajador_estado ON trabajador (estado);
CREATE UNIQUE INDEX trabajador_usuario_unico ON trabajador (usuario_id)
    WHERE usuario_id IS NOT NULL;

CREATE TABLE proveedor (
    id               uuid        PRIMARY KEY,
    codigo           text        NOT NULL,
    razon_social     text        NOT NULL,
    nombre_comercial text        NULL,
    rfc              text        NULL,
    telefono         text        NULL,
    correo           text        NULL,
    domicilio        text        NULL,
    contacto         text        NULL,
    activo           boolean     NOT NULL,
    creado_en        timestamptz NOT NULL DEFAULT now(),
    actualizado_en   timestamptz NULL,

    CONSTRAINT proveedor_codigo_unico UNIQUE (codigo)
);

CREATE INDEX ix_proveedor_razon_social ON proveedor USING gin (razon_social gin_trgm_ops);

CREATE TABLE cliente (
    id                 uuid          PRIMARY KEY,
    codigo             text          NOT NULL,
    razon_social       text          NOT NULL,
    nombre_comercial   text          NULL,
    rfc                text          NULL,
    telefono           text          NULL,
    correo             text          NULL,

    contacto_nombre    text          NULL,
    contacto_puesto    text          NULL,
    contacto_telefono  text          NULL,
    contacto_correo    text          NULL,

    calle              text          NULL,
    colonia            text          NULL,
    municipio          text          NULL,
    estado_prov        text          NULL,
    codigo_postal      text          NULL,
    pais               text          NOT NULL DEFAULT 'MX',
    latitud            numeric(9,6)  NULL,
    longitud           numeric(9,6)  NULL,

    limite_credito     numeric(18,4) NOT NULL DEFAULT 0,
    dias_credito       int           NOT NULL DEFAULT 0,
    deposito_requerido numeric(18,4) NOT NULL DEFAULT 0,
    condiciones        text          NULL,
    estado             smallint      NOT NULL,
    creado_en          timestamptz   NOT NULL DEFAULT now(),
    actualizado_en     timestamptz   NULL,

    CONSTRAINT cliente_codigo_unico UNIQUE (codigo),
    CONSTRAINT cliente_estado CHECK (estado BETWEEN 1 AND 3),
    CONSTRAINT cliente_credito CHECK (limite_credito >= 0 AND dias_credito >= 0),
    CONSTRAINT cliente_deposito CHECK (deposito_requerido >= 0),
    CONSTRAINT cliente_coordenadas CHECK ((latitud IS NULL) = (longitud IS NULL))
);

CREATE INDEX ix_cliente_estado ON cliente (estado);
CREATE INDEX ix_cliente_razon_social ON cliente USING gin (razon_social gin_trgm_ops);

CREATE TABLE tarifa (
    id           uuid        PRIMARY KEY,
    codigo       text        NOT NULL,
    nombre       text        NOT NULL,
    descripcion  text        NULL,
    unidad       smallint    NOT NULL,
    aplica_renta boolean     NOT NULL,
    aplica_venta boolean     NOT NULL,
    activo       boolean     NOT NULL,
    creado_en    timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT tarifa_codigo_unico UNIQUE (codigo),
    CONSTRAINT tarifa_unidad CHECK (unidad BETWEEN 1 AND 6),
    CONSTRAINT tarifa_aplica_en_algo CHECK (aplica_renta OR aplica_venta)
);

CREATE TABLE clausula (
    id             uuid        PRIMARY KEY,
    codigo         text        NOT NULL,
    titulo         text        NOT NULL,
    texto          text        NOT NULL,
    orden          int         NOT NULL,
    obligatoria    boolean     NOT NULL,
    activo         boolean     NOT NULL,
    creado_en      timestamptz NOT NULL DEFAULT now(),
    actualizado_en timestamptz NULL,

    CONSTRAINT clausula_codigo_unico UNIQUE (codigo),
    CONSTRAINT clausula_texto_no_vacio CHECK (length(btrim(texto)) > 0)
);

CREATE INDEX ix_clausula_obligatorias ON clausula (orden) WHERE obligatoria AND activo;

CREATE TABLE equipo (
    id                uuid          PRIMARY KEY,
    codigo_interno    text          NOT NULL,
    modelo_equipo_id  uuid          NOT NULL,
    tipo_equipo_id    uuid          NOT NULL,
    ubicacion_id      uuid          NULL,
    numero_serie      text          NULL,
    anio              int           NULL,
    estado            smallint      NOT NULL,
    proposito         smallint      NOT NULL DEFAULT 1,
    origen            smallint      NOT NULL DEFAULT 1,
    fecha_adquisicion date          NULL,
    costo_adquisicion numeric(18,4) NULL,
    valor_actual      numeric(18,4) NULL,
    horometro         numeric(12,2) NULL,
    kilometraje       numeric(12,2) NULL,
    token_qr          text          NULL,
    notas             text          NULL,
    creado_en         timestamptz   NOT NULL DEFAULT now(),
    actualizado_en    timestamptz   NULL,
    eliminado_en      timestamptz   NULL,

    CONSTRAINT equipo_codigo_unico UNIQUE (codigo_interno),
    CONSTRAINT equipo_token_qr_unico UNIQUE (token_qr),
    CONSTRAINT equipo_estado CHECK (estado BETWEEN 1 AND 8),
    CONSTRAINT equipo_proposito CHECK (proposito BETWEEN 1 AND 3),
    CONSTRAINT equipo_origen CHECK (origen BETWEEN 1 AND 2),
    CONSTRAINT equipo_anio CHECK (anio IS NULL OR anio BETWEEN 1900 AND 2200),
    CONSTRAINT equipo_montos
        CHECK (COALESCE(costo_adquisicion, 0) >= 0 AND COALESCE(valor_actual, 0) >= 0),
    CONSTRAINT equipo_lecturas
        CHECK (COALESCE(horometro, 0) >= 0 AND COALESCE(kilometraje, 0) >= 0),
    CONSTRAINT fk_equipo_modelo FOREIGN KEY (modelo_equipo_id)
        REFERENCES modelo_equipo (id),
    CONSTRAINT fk_equipo_tipo FOREIGN KEY (tipo_equipo_id)
        REFERENCES tipo_equipo (id),
    CONSTRAINT fk_equipo_ubicacion FOREIGN KEY (ubicacion_id)
        REFERENCES ubicacion (id)
);

CREATE INDEX ix_equipo_estado ON equipo (estado) WHERE eliminado_en IS NULL;
CREATE INDEX ix_equipo_ubicacion ON equipo (ubicacion_id) WHERE eliminado_en IS NULL;
CREATE INDEX ix_equipo_modelo ON equipo (modelo_equipo_id);
CREATE INDEX ix_equipo_serie ON equipo USING gin (numero_serie gin_trgm_ops);

CREATE TABLE equipo_archivo (
    id          uuid        PRIMARY KEY,
    equipo_id   uuid        NOT NULL,
    archivo_id  uuid        NOT NULL,
    tipo        smallint    NOT NULL,
    descripcion text        NULL,
    creado_en   timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT equipo_archivo_tipo CHECK (tipo BETWEEN 1 AND 6),
    CONSTRAINT equipo_archivo_unico UNIQUE (equipo_id, archivo_id),
    CONSTRAINT fk_equipo_archivo_equipo FOREIGN KEY (equipo_id)
        REFERENCES equipo (id),
    CONSTRAINT fk_equipo_archivo_archivo FOREIGN KEY (archivo_id)
        REFERENCES archivo (id)
);

CREATE TABLE equipo_tarifa (
    id             uuid          PRIMARY KEY,
    equipo_id      uuid          NOT NULL,
    tarifa_id      uuid          NOT NULL,
    cliente_id     uuid          NULL,
    precio         numeric(18,4) NOT NULL,
    moneda         text          NOT NULL DEFAULT 'MXN',
    vigencia_desde timestamptz   NOT NULL,
    vigencia_hasta timestamptz   NULL,
    creado_en      timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT equipo_tarifa_precio CHECK (precio >= 0),
    CONSTRAINT equipo_tarifa_moneda CHECK (length(moneda) = 3),
    CONSTRAINT equipo_tarifa_vigencia
        CHECK (vigencia_hasta IS NULL OR vigencia_hasta > vigencia_desde),
    CONSTRAINT fk_equipo_tarifa_equipo FOREIGN KEY (equipo_id)
        REFERENCES equipo (id),
    CONSTRAINT fk_equipo_tarifa_tarifa FOREIGN KEY (tarifa_id)
        REFERENCES tarifa (id),
    CONSTRAINT fk_equipo_tarifa_cliente FOREIGN KEY (cliente_id)
        REFERENCES cliente (id)
);

ALTER TABLE equipo_tarifa
    ADD CONSTRAINT equipo_tarifa_sin_traslape
    EXCLUDE USING gist (
        equipo_id WITH =,
        tarifa_id WITH =,
        COALESCE(cliente_id, '00000000-0000-0000-0000-000000000000'::uuid) WITH =,
        tstzrange(vigencia_desde, vigencia_hasta) WITH &&
    );

CREATE TABLE transferencia_equipo (
    id            uuid        PRIMARY KEY,
    equipo_id     uuid        NOT NULL,
    origen_id     uuid        NOT NULL,
    destino_id    uuid        NOT NULL,
    trabajador_id uuid        NOT NULL,
    fecha         timestamptz NOT NULL DEFAULT now(),
    motivo        text        NULL,
    creado_en     timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT transferencia_distinta CHECK (origen_id <> destino_id),
    CONSTRAINT fk_transferencia_equipo FOREIGN KEY (equipo_id)
        REFERENCES equipo (id),
    CONSTRAINT fk_transferencia_origen FOREIGN KEY (origen_id)
        REFERENCES ubicacion (id),
    CONSTRAINT fk_transferencia_destino FOREIGN KEY (destino_id)
        REFERENCES ubicacion (id),
    CONSTRAINT fk_transferencia_trabajador FOREIGN KEY (trabajador_id)
        REFERENCES trabajador (id)
);

CREATE INDEX ix_transferencia_equipo ON transferencia_equipo (equipo_id, fecha DESC);

CREATE TABLE ocupacion_equipo (
    id            uuid        PRIMARY KEY,
    equipo_id     uuid        NOT NULL,
    inicio        timestamptz NOT NULL,
    fin           timestamptz NULL,
    motivo        smallint    NOT NULL,
    referencia_id uuid        NULL,
    nota          text        NULL,
    activo        boolean     NOT NULL DEFAULT true,
    creado_en     timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ocupacion_motivo CHECK (motivo BETWEEN 1 AND 6),
    CONSTRAINT ocupacion_periodo CHECK (fin IS NULL OR fin > inicio),
    CONSTRAINT fk_ocupacion_equipo FOREIGN KEY (equipo_id)
        REFERENCES equipo (id)
);

ALTER TABLE ocupacion_equipo
    ADD CONSTRAINT ocupacion_sin_traslape
    EXCLUDE USING gist (
        equipo_id WITH =,
        tstzrange(inicio, fin) WITH &&
    ) WHERE (activo);

CREATE INDEX ix_ocupacion_equipo ON ocupacion_equipo (equipo_id, inicio) WHERE activo;

CREATE TABLE cotizacion (
    id             uuid          PRIMARY KEY,
    folio          text          NOT NULL,
    cliente_id     uuid          NOT NULL,
    ubicacion_id   uuid          NOT NULL,
    trabajador_id  uuid          NOT NULL,
    fecha          date          NOT NULL DEFAULT current_date,
    vigencia_hasta date          NULL,
    estado         smallint      NOT NULL,
    subtotal       numeric(18,4) NOT NULL DEFAULT 0,
    descuento      numeric(18,4) NOT NULL DEFAULT 0,
    impuestos      numeric(18,4) NOT NULL DEFAULT 0,
    total          numeric(18,4) NOT NULL DEFAULT 0,
    notas          text          NULL,
    creado_en      timestamptz   NOT NULL DEFAULT now(),
    actualizado_en timestamptz   NULL,

    CONSTRAINT cotizacion_folio_unico UNIQUE (folio),
    CONSTRAINT cotizacion_estado CHECK (estado BETWEEN 1 AND 7),
    CONSTRAINT cotizacion_montos
        CHECK (subtotal >= 0 AND descuento >= 0 AND impuestos >= 0 AND total >= 0),
    CONSTRAINT fk_cotizacion_cliente FOREIGN KEY (cliente_id)
        REFERENCES cliente (id),
    CONSTRAINT fk_cotizacion_ubicacion FOREIGN KEY (ubicacion_id)
        REFERENCES ubicacion (id),
    CONSTRAINT fk_cotizacion_trabajador FOREIGN KEY (trabajador_id)
        REFERENCES trabajador (id)
);

CREATE INDEX ix_cotizacion_cliente ON cotizacion (cliente_id, fecha DESC);
CREATE INDEX ix_cotizacion_estado ON cotizacion (estado);

CREATE TABLE cotizacion_linea (
    id              uuid          PRIMARY KEY,
    cotizacion_id   uuid          NOT NULL,
    tarifa_id       uuid          NOT NULL,
    equipo_id       uuid          NULL,
    tipo_equipo_id  uuid          NULL,
    descripcion     text          NULL,
    cantidad        numeric(12,2) NOT NULL,
    precio_unitario numeric(18,4) NOT NULL,
    importe         numeric(18,4) NOT NULL,
    orden           int           NOT NULL DEFAULT 0,

    CONSTRAINT cotizacion_linea_cantidad CHECK (cantidad > 0),
    CONSTRAINT cotizacion_linea_montos CHECK (precio_unitario >= 0 AND importe >= 0),
    CONSTRAINT fk_cotizacion_linea_cotizacion FOREIGN KEY (cotizacion_id)
        REFERENCES cotizacion (id) ON DELETE CASCADE,
    CONSTRAINT fk_cotizacion_linea_tarifa FOREIGN KEY (tarifa_id)
        REFERENCES tarifa (id),
    CONSTRAINT fk_cotizacion_linea_equipo FOREIGN KEY (equipo_id)
        REFERENCES equipo (id),
    CONSTRAINT fk_cotizacion_linea_tipo FOREIGN KEY (tipo_equipo_id)
        REFERENCES tipo_equipo (id)
);

CREATE INDEX ix_cotizacion_linea_cotizacion ON cotizacion_linea (cotizacion_id);

CREATE TABLE renta (
    id             uuid          PRIMARY KEY,
    folio          text          NOT NULL,
    cliente_id     uuid          NOT NULL,
    cotizacion_id  uuid          NULL,
    trabajador_id  uuid          NOT NULL,
    inicio         timestamptz   NOT NULL,
    fin            timestamptz   NOT NULL,
    estado         smallint      NOT NULL,

    lugar_descripcion text       NOT NULL,
    lugar_calle       text       NULL,
    lugar_colonia     text       NULL,
    lugar_municipio   text       NULL,
    lugar_estado_prov text       NULL,
    lugar_codigo_postal text     NULL,
    lugar_latitud     numeric(9,6) NULL,
    lugar_longitud    numeric(9,6) NULL,
    lugar_contacto    text       NULL,
    lugar_telefono    text       NULL,

    deposito       numeric(18,4) NOT NULL DEFAULT 0,
    anticipo       numeric(18,4) NOT NULL DEFAULT 0,
    subtotal       numeric(18,4) NOT NULL DEFAULT 0,
    descuento      numeric(18,4) NOT NULL DEFAULT 0,
    impuestos      numeric(18,4) NOT NULL DEFAULT 0,
    total          numeric(18,4) NOT NULL DEFAULT 0,
    saldo          numeric(18,4) NOT NULL DEFAULT 0,
    notas          text          NULL,
    creado_en      timestamptz   NOT NULL DEFAULT now(),
    actualizado_en timestamptz   NULL,

    CONSTRAINT renta_folio_unico UNIQUE (folio),
    CONSTRAINT renta_estado CHECK (estado BETWEEN 1 AND 10),
    CONSTRAINT renta_periodo CHECK (fin > inicio),
    CONSTRAINT renta_montos
        CHECK (deposito >= 0 AND anticipo >= 0 AND subtotal >= 0
               AND descuento >= 0 AND impuestos >= 0 AND total >= 0),
    CONSTRAINT renta_lugar_no_vacio CHECK (length(btrim(lugar_descripcion)) > 0),
    CONSTRAINT renta_lugar_coordenadas
        CHECK ((lugar_latitud IS NULL) = (lugar_longitud IS NULL)),
    CONSTRAINT fk_renta_cliente FOREIGN KEY (cliente_id)
        REFERENCES cliente (id),
    CONSTRAINT fk_renta_cotizacion FOREIGN KEY (cotizacion_id)
        REFERENCES cotizacion (id),
    CONSTRAINT fk_renta_trabajador FOREIGN KEY (trabajador_id)
        REFERENCES trabajador (id)
);

CREATE INDEX ix_renta_cliente ON renta (cliente_id, inicio DESC);
CREATE INDEX ix_renta_estado ON renta (estado);

CREATE TABLE renta_linea (
    id                   uuid          PRIMARY KEY,
    renta_id             uuid          NOT NULL,
    equipo_id            uuid          NOT NULL,
    tarifa_id            uuid          NOT NULL,
    cantidad             numeric(12,2) NOT NULL,
    precio_unitario      numeric(18,4) NOT NULL,
    horas_incluidas      numeric(12,2) NULL,
    importe              numeric(18,4) NOT NULL,
    horometro_salida     numeric(12,2) NULL,
    horometro_devolucion numeric(12,2) NULL,
    orden                int           NOT NULL DEFAULT 0,

    CONSTRAINT renta_linea_unica UNIQUE (renta_id, equipo_id, tarifa_id),
    CONSTRAINT renta_linea_cantidad CHECK (cantidad > 0),
    CONSTRAINT renta_linea_montos CHECK (precio_unitario >= 0 AND importe >= 0),
    CONSTRAINT fk_renta_linea_renta FOREIGN KEY (renta_id)
        REFERENCES renta (id) ON DELETE CASCADE,
    CONSTRAINT fk_renta_linea_equipo FOREIGN KEY (equipo_id)
        REFERENCES equipo (id),
    CONSTRAINT fk_renta_linea_tarifa FOREIGN KEY (tarifa_id)
        REFERENCES tarifa (id)
);

CREATE INDEX ix_renta_linea_renta ON renta_linea (renta_id);
CREATE INDEX ix_renta_linea_equipo ON renta_linea (equipo_id);

CREATE TABLE renta_concepto (
    id              uuid          PRIMARY KEY,
    renta_id        uuid          NOT NULL,
    tarifa_id       uuid          NOT NULL,
    trabajador_id   uuid          NULL,
    descripcion     text          NULL,
    cantidad        numeric(12,2) NOT NULL DEFAULT 1,
    precio_unitario numeric(18,4) NOT NULL,
    costo           numeric(18,4) NULL,
    importe         numeric(18,4) NOT NULL,
    creado_en       timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT renta_concepto_cantidad CHECK (cantidad > 0),
    CONSTRAINT renta_concepto_montos
        CHECK (precio_unitario >= 0 AND importe >= 0 AND COALESCE(costo, 0) >= 0),
    CONSTRAINT fk_renta_concepto_renta FOREIGN KEY (renta_id)
        REFERENCES renta (id) ON DELETE CASCADE,
    CONSTRAINT fk_renta_concepto_tarifa FOREIGN KEY (tarifa_id)
        REFERENCES tarifa (id),
    CONSTRAINT fk_renta_concepto_trabajador FOREIGN KEY (trabajador_id)
        REFERENCES trabajador (id)
);

CREATE INDEX ix_renta_concepto_renta ON renta_concepto (renta_id);

CREATE TABLE extension_renta (
    id            uuid        PRIMARY KEY,
    renta_id      uuid        NOT NULL,
    fin_anterior  timestamptz NOT NULL,
    fin_nuevo     timestamptz NOT NULL,
    motivo        text        NULL,
    trabajador_id uuid        NOT NULL,
    creado_en     timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT extension_avanza CHECK (fin_nuevo > fin_anterior),
    CONSTRAINT fk_extension_renta FOREIGN KEY (renta_id)
        REFERENCES renta (id) ON DELETE CASCADE,
    CONSTRAINT fk_extension_trabajador FOREIGN KEY (trabajador_id)
        REFERENCES trabajador (id)
);

CREATE TABLE contrato (
    id             uuid          PRIMARY KEY,
    folio          text          NOT NULL,
    renta_id       uuid          NOT NULL,
    cliente_id     uuid          NOT NULL,
    fecha_inicio   date          NOT NULL,
    fecha_fin      date          NULL,
    deposito       numeric(18,4) NOT NULL DEFAULT 0,
    estado         smallint      NOT NULL,
    firmado_en     timestamptz   NULL,
    notas          text          NULL,
    creado_en      timestamptz   NOT NULL DEFAULT now(),
    actualizado_en timestamptz   NULL,

    CONSTRAINT contrato_folio_unico UNIQUE (folio),
    CONSTRAINT contrato_renta_unica UNIQUE (renta_id),
    CONSTRAINT contrato_estado CHECK (estado BETWEEN 1 AND 4),
    CONSTRAINT contrato_deposito CHECK (deposito >= 0),
    CONSTRAINT contrato_fechas CHECK (fecha_fin IS NULL OR fecha_fin >= fecha_inicio),
    CONSTRAINT fk_contrato_renta FOREIGN KEY (renta_id)
        REFERENCES renta (id),
    CONSTRAINT fk_contrato_cliente FOREIGN KEY (cliente_id)
        REFERENCES cliente (id)
);

CREATE INDEX ix_contrato_cliente ON contrato (cliente_id, fecha_inicio DESC);

CREATE TABLE contrato_clausula (
    id          uuid        PRIMARY KEY,
    contrato_id uuid        NOT NULL,
    clausula_id uuid        NULL,
    orden       int         NOT NULL,
    titulo      text        NOT NULL,
    texto       text        NOT NULL,
    creado_en   timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT contrato_clausula_orden_unico UNIQUE (contrato_id, orden),
    CONSTRAINT contrato_clausula_texto CHECK (length(btrim(texto)) > 0),
    CONSTRAINT fk_contrato_clausula_contrato FOREIGN KEY (contrato_id)
        REFERENCES contrato (id) ON DELETE CASCADE,
    CONSTRAINT fk_contrato_clausula_clausula FOREIGN KEY (clausula_id)
        REFERENCES clausula (id)
);

CREATE TABLE orden_compra (
    id            uuid          PRIMARY KEY,
    folio         text          NOT NULL,
    proveedor_id  uuid          NOT NULL,
    trabajador_id uuid          NOT NULL,
    fecha         date          NOT NULL DEFAULT current_date,
    estado        smallint      NOT NULL,
    subtotal      numeric(18,4) NOT NULL DEFAULT 0,
    impuestos     numeric(18,4) NOT NULL DEFAULT 0,
    total         numeric(18,4) NOT NULL DEFAULT 0,
    autorizada_en timestamptz   NULL,
    finalizada_en timestamptz   NULL,
    notas         text          NULL,
    creado_en     timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT orden_compra_folio_unico UNIQUE (folio),
    CONSTRAINT orden_compra_estado CHECK (estado BETWEEN 1 AND 4),
    CONSTRAINT orden_compra_montos CHECK (subtotal >= 0 AND impuestos >= 0 AND total >= 0),
    CONSTRAINT orden_compra_finalizacion CHECK ((estado = 3) = (finalizada_en IS NOT NULL)),
    CONSTRAINT fk_orden_compra_proveedor FOREIGN KEY (proveedor_id)
        REFERENCES proveedor (id),
    CONSTRAINT fk_orden_compra_trabajador FOREIGN KEY (trabajador_id)
        REFERENCES trabajador (id)
);

CREATE INDEX ix_orden_compra_estado ON orden_compra (estado);

CREATE TABLE orden_compra_detalle (
    id               uuid          PRIMARY KEY,
    orden_compra_id  uuid          NOT NULL,
    modelo_equipo_id uuid          NOT NULL,
    equipo_id        uuid          NULL,
    numero_serie     text          NULL,
    anio             int           NULL,
    cantidad         int           NOT NULL DEFAULT 1,
    costo_unitario   numeric(18,4) NOT NULL,
    importe          numeric(18,4) NOT NULL,
    orden            int           NOT NULL DEFAULT 0,

    CONSTRAINT orden_compra_detalle_cantidad CHECK (cantidad > 0),
    CONSTRAINT orden_compra_detalle_montos CHECK (costo_unitario >= 0 AND importe >= 0),
    CONSTRAINT orden_compra_detalle_equipo_unico UNIQUE (equipo_id),
    CONSTRAINT fk_orden_compra_detalle_orden FOREIGN KEY (orden_compra_id)
        REFERENCES orden_compra (id) ON DELETE CASCADE,
    CONSTRAINT fk_orden_compra_detalle_modelo FOREIGN KEY (modelo_equipo_id)
        REFERENCES modelo_equipo (id),
    CONSTRAINT fk_orden_compra_detalle_equipo FOREIGN KEY (equipo_id)
        REFERENCES equipo (id)
);

CREATE INDEX ix_orden_compra_detalle_orden ON orden_compra_detalle (orden_compra_id);

CREATE TABLE orden_venta (
    id            uuid          PRIMARY KEY,
    folio         text          NOT NULL,
    cliente_id    uuid          NOT NULL,
    trabajador_id uuid          NOT NULL,
    fecha         date          NOT NULL DEFAULT current_date,
    estado        smallint      NOT NULL,
    subtotal      numeric(18,4) NOT NULL DEFAULT 0,
    descuento     numeric(18,4) NOT NULL DEFAULT 0,
    impuestos     numeric(18,4) NOT NULL DEFAULT 0,
    total         numeric(18,4) NOT NULL DEFAULT 0,
    autorizada_en timestamptz   NULL,
    finalizada_en timestamptz   NULL,
    notas         text          NULL,
    creado_en     timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT orden_venta_folio_unico UNIQUE (folio),
    CONSTRAINT orden_venta_estado CHECK (estado BETWEEN 1 AND 4),
    CONSTRAINT orden_venta_montos
        CHECK (subtotal >= 0 AND descuento >= 0 AND impuestos >= 0 AND total >= 0),
    CONSTRAINT orden_venta_finalizacion CHECK ((estado = 3) = (finalizada_en IS NOT NULL)),
    CONSTRAINT fk_orden_venta_cliente FOREIGN KEY (cliente_id)
        REFERENCES cliente (id),
    CONSTRAINT fk_orden_venta_trabajador FOREIGN KEY (trabajador_id)
        REFERENCES trabajador (id)
);

CREATE INDEX ix_orden_venta_estado ON orden_venta (estado);

CREATE TABLE orden_venta_detalle (
    id              uuid          PRIMARY KEY,
    orden_venta_id  uuid          NOT NULL,
    equipo_id       uuid          NOT NULL,
    precio_unitario numeric(18,4) NOT NULL,
    importe         numeric(18,4) NOT NULL,
    orden           int           NOT NULL DEFAULT 0,

    CONSTRAINT orden_venta_detalle_montos CHECK (precio_unitario >= 0 AND importe >= 0),
    CONSTRAINT orden_venta_detalle_unico UNIQUE (orden_venta_id, equipo_id),
    CONSTRAINT fk_orden_venta_detalle_orden FOREIGN KEY (orden_venta_id)
        REFERENCES orden_venta (id) ON DELETE CASCADE,
    CONSTRAINT fk_orden_venta_detalle_equipo FOREIGN KEY (equipo_id)
        REFERENCES equipo (id)
);

CREATE INDEX ix_orden_venta_detalle_orden ON orden_venta_detalle (orden_venta_id);

CREATE FUNCTION ubicacion_almacena(p_id uuid) RETURNS boolean AS $$
    SELECT COALESCE((SELECT almacena_equipo FROM ubicacion WHERE id = p_id), false);
$$ LANGUAGE sql STABLE;

CREATE FUNCTION ubicacion_administra(p_id uuid) RETURNS boolean AS $$
    SELECT COALESCE((SELECT es_administrativa FROM ubicacion WHERE id = p_id), false);
$$ LANGUAGE sql STABLE;

CREATE FUNCTION equipo_exigir_almacen() RETURNS trigger AS $$
BEGIN
    IF NEW.ubicacion_id IS NOT NULL AND NOT ubicacion_almacena(NEW.ubicacion_id) THEN
        RAISE EXCEPTION
            'El equipo solo puede resguardarse en una bodega o un patio (ubicacion %)',
            NEW.ubicacion_id;
    END IF;

    RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER equipo_ubicacion_almacen
    BEFORE INSERT OR UPDATE OF ubicacion_id ON equipo
    FOR EACH ROW EXECUTE FUNCTION equipo_exigir_almacen();

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

CREATE FUNCTION contrato_clausula_proteger() RETURNS trigger AS $$
DECLARE
    v_estado   smallint;
    v_contrato uuid;
BEGIN
    v_contrato := COALESCE(NEW.contrato_id, OLD.contrato_id);

    SELECT estado INTO v_estado FROM contrato WHERE id = v_contrato;

    IF v_estado IS NOT NULL AND v_estado <> 1 THEN
        RAISE EXCEPTION 'Las clausulas de un contrato autorizado no se pueden modificar';
    END IF;

    RETURN COALESCE(NEW, OLD);
END $$ LANGUAGE plpgsql;

CREATE TRIGGER contrato_clausula_inmutable
    BEFORE INSERT OR UPDATE OR DELETE ON contrato_clausula
    FOR EACH ROW EXECUTE FUNCTION contrato_clausula_proteger();
