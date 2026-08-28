namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// La empresa de la peticion en curso. De ambito de peticion.
///
/// Lo llena un middleware a partir del JWT y lo consume todo lo que necesite saber
/// contra que base trabajar. Los casos de uso lo reciben por inyeccion en lugar de
/// pasarse el tenant de parametro en parametro.
/// </summary>
public interface IContextoTenant
{
    /// <summary>
    /// Si esta peticion tiene una empresa resuelta. Falso en las anonimas y en las de
    /// plataforma, que no pertenecen a ninguna empresa.
    /// </summary>
    bool EstaResuelto { get; }

    /// <summary>
    /// La empresa en curso.
    ///
    /// LANZA si no hay ninguna resuelta, y eso es deliberado: NO EXISTE UNA BASE POR
    /// DEFECTO. Si un camino de codigo llega hasta aqui sin tenant, es un error de
    /// programacion y tiene que reventar ruidosamente. Devolver algo —la central, la
    /// plantilla, la ultima usada— seria una fuga de datos entre clientes esperando
    /// a que alguien la encuentre.
    /// </summary>
    TenantResuelto Actual { get; }

    /// <summary>Lo llama el middleware. Una sola vez por peticion.</summary>
    void Establecer(TenantResuelto tenant);
}
