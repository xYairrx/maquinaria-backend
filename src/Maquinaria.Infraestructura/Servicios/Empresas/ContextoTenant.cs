using Maquinaria.Aplicacion.Empresas;

namespace Maquinaria.Infraestructura.Empresas;

/// <summary>
/// Portador de ambito de peticion del tenant en curso. Lo llena el middleware.
/// </summary>
internal sealed class ContextoTenant : IContextoTenant
{
    private TenantResuelto? _tenant;

    public bool EstaResuelto => _tenant is not null;

    public TenantResuelto Actual => _tenant ?? throw new InvalidOperationException(
        "No hay empresa resuelta en esta peticion. NO EXISTE UNA BASE POR DEFECTO: si se "
        + "llego aqui sin tenant, es un error de programacion. Devolver la central o la "
        + "plantilla seria una fuga de datos entre clientes esperando a ser encontrada.");

    public void Establecer(TenantResuelto tenant)
    {
        if (_tenant is not null)
        {
            // Reasignar significaria que una peticion cambio de empresa a medio camino.
            // Es un error grave y silencioso si se permite.
            throw new InvalidOperationException(
                "La empresa de esta peticion ya estaba resuelta y no se puede cambiar.");
        }

        _tenant = tenant;
    }
}
