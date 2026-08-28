namespace Maquinaria.Aplicacion.Comun;

/// <summary>Los cinco documentos con folio unico por empresa.</summary>
public enum TipoDocumento : short
{
    Cotizacion = 1,
    Renta = 2,
    Contrato = 3,
    OrdenCompra = 4,
    OrdenVenta = 5,
}

/// <summary>
/// Genera el folio del siguiente documento: <c>COT-2026-00001</c>.
///
/// **SIN SECUENCIA DE POSTGRES, y es una limitacion consciente.** El esquema migrado no trae
/// secuencias —solo el `UNIQUE` sobre <c>folio</c>—, y esta fase no lo cambia. Asi que el
/// consecutivo se calcula leyendo el mayor folio del ano y sumando uno, lo que **no aguanta
/// concurrencia**: dos altas simultaneas leen el mismo maximo y la segunda choca con el
/// `UNIQUE`.
///
/// Eso no corrompe nada —el `UNIQUE` es la garantia— y quien crea el documento **reintenta**.
/// Con el volumen de un mostrador la colision es rara; con veinte capturistas a la vez, no. El
/// arreglo de verdad es una secuencia por tipo y esta anotado en el plan de la fase.
///
/// El folio NO es la llave: la llave es el uuid v7. El folio es el numero que la gente dice por
/// telefono, y por eso lleva el ano — buscar «la 47» sin ano no distingue la de este ano de la
/// del anterior.
/// </summary>
public interface IFolios
{
    Task<string> SiguienteAsync(TipoDocumento tipo, CancellationToken ct);
}
