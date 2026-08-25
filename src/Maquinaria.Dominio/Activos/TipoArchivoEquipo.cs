namespace Maquinaria.Dominio.Activos;

/// <summary>Que es el documento que se adjunta a una maquina.</summary>
public enum TipoArchivoEquipo : short
{
    Foto = 1,

    Factura = 2,

    /// <summary>Poliza de seguro.</summary>
    Poliza = 3,

    Manual = 4,

    /// <summary>Certificado de calibracion, verificacion, emisiones.</summary>
    Certificado = 5,

    Otro = 6,
}
