BEGIN;

CREATE TEMP TABLE resultado (n int, prueba text, esperado text, obtenido text);

CREATE OR REPLACE FUNCTION pg_temp.intentar(
    p_n int, p_prueba text, p_esperado text, p_sql text) RETURNS void AS $fn$
BEGIN
    BEGIN
        EXECUTE p_sql;
        INSERT INTO resultado VALUES (
            p_n, p_prueba, p_esperado,
            CASE WHEN p_esperado = 'acepta' THEN 'acepta' ELSE 'ACEPTO -- MAL' END);
    EXCEPTION WHEN others THEN
        INSERT INTO resultado VALUES (
            p_n, p_prueba, p_esperado,
            CASE WHEN p_esperado = 'rechaza'
                 THEN 'rechaza' ELSE 'RECHAZO -- MAL: ' || left(SQLERRM, 60) END);
    END;
END $fn$ LANGUAGE plpgsql;

INSERT INTO categoria_equipo (id, codigo, nombre, activo)
VALUES ('11111111-0000-0000-0000-000000000001', 'CAT', 'Excavacion', true);

INSERT INTO tipo_equipo (id, categoria_equipo_id, codigo, nombre, activo)
VALUES ('11111111-0000-0000-0000-000000000002',
        '11111111-0000-0000-0000-000000000001', 'TIP', 'Retroexcavadora', true);

INSERT INTO marca (id, nombre, activo)
VALUES ('11111111-0000-0000-0000-000000000003', 'Caterpillar', true);

INSERT INTO modelo_equipo (id, marca_id, nombre, activo)
VALUES ('11111111-0000-0000-0000-000000000004',
        '11111111-0000-0000-0000-000000000003', '320D', true);

INSERT INTO ubicacion (id, codigo, nombre, tipo, activo) VALUES
    ('22222222-0000-0000-0000-000000000001', 'BOD', 'Bodega Centro',   1, true),
    ('22222222-0000-0000-0000-000000000002', 'SUC', 'Sucursal Centro', 2, true),
    ('22222222-0000-0000-0000-000000000003', 'PAT', 'Patio Norte',     3, true);

INSERT INTO puesto (id, codigo, nombre, activo)
VALUES ('33333333-0000-0000-0000-000000000001', 'OPE', 'Operador', true);

INSERT INTO trabajador (id, numero_empleado, nombre, puesto_id, estado)
VALUES ('44444444-0000-0000-0000-000000000001', 'E001', 'Juan Perez',
        '33333333-0000-0000-0000-000000000001', 1);

INSERT INTO cliente (id, codigo, razon_social, limite_credito, dias_credito,
                     deposito_requerido, estado)
VALUES ('55555555-0000-0000-0000-000000000001', 'CLI001', 'Constructora del Norte',
        0, 0, 0, 1);

INSERT INTO tarifa (id, codigo, nombre, unidad, aplica_renta, aplica_venta, activo)
VALUES ('66666666-0000-0000-0000-000000000001', 'REN-DIA', 'Renta por dia', 2,
        true, false, true);

INSERT INTO equipo (id, codigo_interno, modelo_equipo_id, tipo_equipo_id, ubicacion_id,
                    estado, proposito, origen)
VALUES ('77777777-0000-0000-0000-000000000001', 'EQ-001',
        '11111111-0000-0000-0000-000000000004', '11111111-0000-0000-0000-000000000002',
        '22222222-0000-0000-0000-000000000001', 1, 1, 1);

-- ---------------------------------------------------------------------------
-- NO RENTAR LA MISMA MAQUINA EN FECHAS QUE SE TRASLAPAN
-- ---------------------------------------------------------------------------

SELECT pg_temp.intentar(1, 'ocupacion del 1 al 10', 'acepta', $q$
    INSERT INTO ocupacion_equipo (id, equipo_id, inicio, fin, motivo)
    VALUES ('88888888-0000-0000-0000-000000000001',
            '77777777-0000-0000-0000-000000000001',
            '2026-09-01', '2026-09-10', 1);
$q$);

SELECT pg_temp.intentar(2, 'traslape del 5 al 15 (mismo equipo)', 'rechaza', $q$
    INSERT INTO ocupacion_equipo (id, equipo_id, inicio, fin, motivo)
    VALUES ('88888888-0000-0000-0000-000000000002',
            '77777777-0000-0000-0000-000000000001',
            '2026-09-05', '2026-09-15', 1);
$q$);

SELECT pg_temp.intentar(3, 'traslape de un solo dia', 'rechaza', $q$
    INSERT INTO ocupacion_equipo (id, equipo_id, inicio, fin, motivo)
    VALUES ('88888888-0000-0000-0000-000000000003',
            '77777777-0000-0000-0000-000000000001',
            '2026-09-09', '2026-09-20', 1);
$q$);

SELECT pg_temp.intentar(4, 'sin traslape: del 10 al 20', 'acepta', $q$
    INSERT INTO ocupacion_equipo (id, equipo_id, inicio, fin, motivo)
    VALUES ('88888888-0000-0000-0000-000000000004',
            '77777777-0000-0000-0000-000000000001',
            '2026-09-10', '2026-09-20', 2);
$q$);

SELECT pg_temp.intentar(5, 'mantenimiento tambien bloquea la renta', 'rechaza', $q$
    INSERT INTO ocupacion_equipo (id, equipo_id, inicio, fin, motivo)
    VALUES ('88888888-0000-0000-0000-000000000005',
            '77777777-0000-0000-0000-000000000001',
            '2026-09-03', '2026-09-04', 3);
$q$);

SELECT pg_temp.intentar(6, 'abierta que empieza dentro de otra', 'rechaza', $q$
    INSERT INTO ocupacion_equipo (id, equipo_id, inicio, fin, motivo)
    VALUES ('88888888-0000-0000-0000-000000000006',
            '77777777-0000-0000-0000-000000000001',
            '2026-09-15', NULL, 1);
$q$);

SELECT pg_temp.intentar(7, 'cancelar libera el periodo', 'acepta', $q$
    UPDATE ocupacion_equipo SET activo = false
    WHERE id = '88888888-0000-0000-0000-000000000001';

    INSERT INTO ocupacion_equipo (id, equipo_id, inicio, fin, motivo)
    VALUES ('88888888-0000-0000-0000-000000000007',
            '77777777-0000-0000-0000-000000000001',
            '2026-09-01', '2026-09-08', 1);
$q$);

-- ---------------------------------------------------------------------------
-- BODEGA GUARDA, SUCURSAL COTIZA, PATIO LAS DOS COSAS
-- ---------------------------------------------------------------------------

SELECT pg_temp.intentar(8, 'equipo en una sucursal', 'rechaza', $q$
    UPDATE equipo SET ubicacion_id = '22222222-0000-0000-0000-000000000002'
    WHERE id = '77777777-0000-0000-0000-000000000001';
$q$);

SELECT pg_temp.intentar(9, 'equipo en un patio', 'acepta', $q$
    UPDATE equipo SET ubicacion_id = '22222222-0000-0000-0000-000000000003'
    WHERE id = '77777777-0000-0000-0000-000000000001';
$q$);

SELECT pg_temp.intentar(10, 'traspaso con destino sucursal', 'rechaza', $q$
    INSERT INTO transferencia_equipo (id, equipo_id, origen_id, destino_id, trabajador_id)
    VALUES ('99999999-0000-0000-0000-000000000001',
            '77777777-0000-0000-0000-000000000001',
            '22222222-0000-0000-0000-000000000001',
            '22222222-0000-0000-0000-000000000002',
            '44444444-0000-0000-0000-000000000001');
$q$);

SELECT pg_temp.intentar(11, 'traspaso bodega a patio', 'acepta', $q$
    INSERT INTO transferencia_equipo (id, equipo_id, origen_id, destino_id, trabajador_id)
    VALUES ('99999999-0000-0000-0000-000000000002',
            '77777777-0000-0000-0000-000000000001',
            '22222222-0000-0000-0000-000000000001',
            '22222222-0000-0000-0000-000000000003',
            '44444444-0000-0000-0000-000000000001');
$q$);

SELECT pg_temp.intentar(12, 'cotizar desde una bodega', 'rechaza', $q$
    INSERT INTO cotizacion (id, folio, cliente_id, ubicacion_id, trabajador_id, estado,
                            subtotal, descuento, impuestos, total)
    VALUES ('aaaaaaaa-0000-0000-0000-000000000001', 'COT-001',
            '55555555-0000-0000-0000-000000000001',
            '22222222-0000-0000-0000-000000000001',
            '44444444-0000-0000-0000-000000000001', 1, 0, 0, 0, 0);
$q$);

SELECT pg_temp.intentar(13, 'cotizar desde una sucursal', 'acepta', $q$
    INSERT INTO cotizacion (id, folio, cliente_id, ubicacion_id, trabajador_id, estado,
                            subtotal, descuento, impuestos, total)
    VALUES ('aaaaaaaa-0000-0000-0000-000000000002', 'COT-002',
            '55555555-0000-0000-0000-000000000001',
            '22222222-0000-0000-0000-000000000002',
            '44444444-0000-0000-0000-000000000001', 1, 0, 0, 0, 0);
$q$);

-- ---------------------------------------------------------------------------
-- UN CONTRATO AUTORIZADO NO SE TOCA
-- ---------------------------------------------------------------------------

INSERT INTO renta (id, folio, cliente_id, trabajador_id, inicio, fin, estado,
                   lugar_descripcion, deposito, anticipo, subtotal, descuento,
                   impuestos, total, saldo)
VALUES ('bbbbbbbb-0000-0000-0000-000000000001', 'REN-001',
        '55555555-0000-0000-0000-000000000001',
        '44444444-0000-0000-0000-000000000001',
        '2026-10-01', '2026-10-15', 2, 'Obra Torre Norte', 0, 0, 0, 0, 0, 0, 0);

INSERT INTO contrato (id, folio, renta_id, cliente_id, fecha_inicio, deposito, estado)
VALUES ('cccccccc-0000-0000-0000-000000000001', 'CON-001',
        'bbbbbbbb-0000-0000-0000-000000000001',
        '55555555-0000-0000-0000-000000000001', '2026-10-01', 0, 1);

SELECT pg_temp.intentar(14, 'editar un contrato en borrador', 'acepta', $q$
    UPDATE contrato SET notas = 'ajuste antes de autorizar'
    WHERE id = 'cccccccc-0000-0000-0000-000000000001';
$q$);

SELECT pg_temp.intentar(15, 'agregar clausula en borrador', 'acepta', $q$
    INSERT INTO contrato_clausula (id, contrato_id, orden, titulo, texto)
    VALUES ('dddddddd-0000-0000-0000-000000000001',
            'cccccccc-0000-0000-0000-000000000001', 1, 'Deposito',
            'El arrendatario entrega un deposito en garantia.');
$q$);

SELECT pg_temp.intentar(16, 'autorizar el contrato', 'acepta', $q$
    UPDATE contrato SET estado = 2
    WHERE id = 'cccccccc-0000-0000-0000-000000000001';
$q$);

SELECT pg_temp.intentar(17, 'editar el contrato AUTORIZADO', 'rechaza', $q$
    UPDATE contrato SET notas = 'cambiando a escondidas'
    WHERE id = 'cccccccc-0000-0000-0000-000000000001';
$q$);

SELECT pg_temp.intentar(18, 'borrar el contrato AUTORIZADO', 'rechaza', $q$
    DELETE FROM contrato WHERE id = 'cccccccc-0000-0000-0000-000000000001';
$q$);

SELECT pg_temp.intentar(19, 'cambiar el TEXTO de una clausula autorizada', 'rechaza', $q$
    UPDATE contrato_clausula SET texto = 'sin deposito'
    WHERE id = 'dddddddd-0000-0000-0000-000000000001';
$q$);

SELECT pg_temp.intentar(20, 'agregar clausula a un contrato autorizado', 'rechaza', $q$
    INSERT INTO contrato_clausula (id, contrato_id, orden, titulo, texto)
    VALUES ('dddddddd-0000-0000-0000-000000000002',
            'cccccccc-0000-0000-0000-000000000001', 2, 'Colada', 'Texto nuevo.');
$q$);

SELECT pg_temp.intentar(21, 'borrar clausula de un contrato autorizado', 'rechaza', $q$
    DELETE FROM contrato_clausula
    WHERE id = 'dddddddd-0000-0000-0000-000000000001';
$q$);

SELECT pg_temp.intentar(22, 'segundo contrato para la misma renta', 'rechaza', $q$
    INSERT INTO contrato (id, folio, renta_id, cliente_id, fecha_inicio, deposito, estado)
    VALUES ('cccccccc-0000-0000-0000-000000000002', 'CON-002',
            'bbbbbbbb-0000-0000-0000-000000000001',
            '55555555-0000-0000-0000-000000000001', '2026-10-01', 0, 1);
$q$);

-- ---------------------------------------------------------------------------
-- ...PERO SU CICLO DE VIDA SI AVANZA
--
-- EL HUECO QUE ESTAS PRUEBAS LLENAN. Las de arriba comprueban que el CONTENIDO de un
-- contrato autorizado esta congelado —17, 18, 19, 20, 21— y ninguna comprobaba que el
-- ESTADO pueda seguir avanzando. Con esa mitad sin probar, `contrato_inmutable` bloqueaba
-- tambien el UPDATE que solo mueve `estado`: Firmado y Terminado eran inalcanzables y
-- `firmado_en` no podia tener valor nunca, mientras las 22 pruebas pasaban en verde.
--
-- Se detecto el 2026-08-28 usando la pantalla, no leyendo el esquema. Corregido en la
-- migracion EmpresaContratoAvanzaEstado.
--
-- Van numeradas 31-35 y no 22.x porque `intentar` recibe `p_n int`: un 22.1 se redondearia
-- a 22 y las cinco saldrian con el mismo numero en el reporte.
-- ---------------------------------------------------------------------------

SELECT pg_temp.intentar(31, 'FIRMAR un contrato autorizado', 'acepta', $q$
    UPDATE contrato SET estado = 3, firmado_en = now()
    WHERE id = 'cccccccc-0000-0000-0000-000000000001';
$q$);

SELECT pg_temp.intentar(32, 'TERMINAR un contrato firmado', 'acepta', $q$
    UPDATE contrato SET estado = 4
    WHERE id = 'cccccccc-0000-0000-0000-000000000001';
$q$);

-- Y el contenido sigue congelado DESPUES de firmar, que es lo que la garantia protege de
-- verdad: si el texto pudiera cambiar despues de la firma, la firma no significaria nada.
SELECT pg_temp.intentar(33, 'cambiar el deposito de un contrato firmado', 'rechaza', $q$
    UPDATE contrato SET deposito = 99999
    WHERE id = 'cccccccc-0000-0000-0000-000000000001';
$q$);

SELECT pg_temp.intentar(34, 'cambiar la fecha de un contrato firmado', 'rechaza', $q$
    UPDATE contrato SET fecha_fin = '2027-01-01'
    WHERE id = 'cccccccc-0000-0000-0000-000000000001';
$q$);

-- Mover el estado Y el contenido en el mismo UPDATE tampoco pasa: se comparan las columnas
-- una por una, asi que colar un cambio de contenido junto a uno de estado se rechaza igual.
SELECT pg_temp.intentar(35, 'colar un cambio de notas junto al de estado', 'rechaza', $q$
    UPDATE contrato SET estado = 4, notas = 'colado'
    WHERE id = 'cccccccc-0000-0000-0000-000000000001';
$q$);

-- ---------------------------------------------------------------------------
-- UN SOLO PRECIO VIGENTE
-- ---------------------------------------------------------------------------

SELECT pg_temp.intentar(23, 'precio de lista vigente', 'acepta', $q$
    INSERT INTO equipo_tarifa (id, equipo_id, tarifa_id, precio, vigencia_desde)
    VALUES ('eeeeeeee-0000-0000-0000-000000000001',
            '77777777-0000-0000-0000-000000000001',
            '66666666-0000-0000-0000-000000000001', 4500, '2026-01-01');
$q$);

SELECT pg_temp.intentar(24, 'segundo precio de lista solapado', 'rechaza', $q$
    INSERT INTO equipo_tarifa (id, equipo_id, tarifa_id, precio, vigencia_desde)
    VALUES ('eeeeeeee-0000-0000-0000-000000000002',
            '77777777-0000-0000-0000-000000000001',
            '66666666-0000-0000-0000-000000000001', 5000, '2026-06-01');
$q$);

SELECT pg_temp.intentar(25, 'precio especial para un cliente', 'acepta', $q$
    INSERT INTO equipo_tarifa (id, equipo_id, tarifa_id, cliente_id, precio, vigencia_desde)
    VALUES ('eeeeeeee-0000-0000-0000-000000000003',
            '77777777-0000-0000-0000-000000000001',
            '66666666-0000-0000-0000-000000000001',
            '55555555-0000-0000-0000-000000000001', 4200, '2026-01-01');
$q$);

-- ---------------------------------------------------------------------------
-- LO QUE MI PRIMERA VERSION ROMPIA
-- ---------------------------------------------------------------------------

SELECT pg_temp.intentar(26, 'cotizar un flete sin equipo ni tipo', 'acepta', $q$
    INSERT INTO cotizacion_linea (id, cotizacion_id, tarifa_id, descripcion,
                                  cantidad, precio_unitario, importe)
    VALUES ('ffffffff-0000-0000-0000-000000000001',
            'aaaaaaaa-0000-0000-0000-000000000002',
            '66666666-0000-0000-0000-000000000001',
            'Flete de ida y vuelta', 1, 3000, 3000);
$q$);

SELECT pg_temp.intentar(27, 'orden de venta finalizada SIN fecha', 'rechaza', $q$
    INSERT INTO orden_venta (id, folio, cliente_id, trabajador_id, estado,
                             subtotal, descuento, impuestos, total)
    VALUES ('a1a1a1a1-0000-0000-0000-000000000001', 'OV-001',
            '55555555-0000-0000-0000-000000000001',
            '44444444-0000-0000-0000-000000000001', 3, 0, 0, 0, 0);
$q$);

SELECT pg_temp.intentar(28, 'ocupacion abierta a futuro', 'acepta', $q$
    INSERT INTO ocupacion_equipo (id, equipo_id, inicio, fin, motivo)
    VALUES ('88888888-0000-0000-0000-000000000008',
            '77777777-0000-0000-0000-000000000001',
            '2026-12-01', NULL, 1);
$q$);

SELECT pg_temp.intentar(29, 'una abierta bloquea todo lo posterior', 'rechaza', $q$
    INSERT INTO ocupacion_equipo (id, equipo_id, inicio, fin, motivo)
    VALUES ('88888888-0000-0000-0000-000000000009',
            '77777777-0000-0000-0000-000000000001',
            '2027-06-01', '2027-06-30', 1);
$q$);

SELECT pg_temp.intentar(30, 'otro equipo, mismas fechas', 'acepta', $q$
    INSERT INTO equipo (id, codigo_interno, modelo_equipo_id, tipo_equipo_id,
                        estado, proposito, origen)
    VALUES ('77777777-0000-0000-0000-000000000002', 'EQ-002',
            '11111111-0000-0000-0000-000000000004',
            '11111111-0000-0000-0000-000000000002', 1, 1, 1);

    INSERT INTO ocupacion_equipo (id, equipo_id, inicio, fin, motivo)
    VALUES ('88888888-0000-0000-0000-00000000000a',
            '77777777-0000-0000-0000-000000000002',
            '2026-09-05', '2026-09-15', 1);
$q$);

SELECT n, prueba, esperado, obtenido FROM resultado ORDER BY n;

SELECT count(*) FILTER (WHERE obtenido LIKE '%MAL%') AS fallos,
       count(*) AS total
FROM resultado;

ROLLBACK;
