namespace Maquinaria.Api.Seguridad;

/// <summary>
/// Nombres de las politicas de autorizacion, para no repetir cadenas.
///
/// Vivian al final de Program.cs, en el namespace global. Se mudaron aqui con el paso a
/// controladores: ahora las nombra un atributo en cada clase, asi que dejaron de ser un
/// detalle del arranque y pasaron a ser parte del contrato que lee todo el que escribe un
/// controlador.
/// </summary>
internal static class PoliticasAutorizacion
{
    /// <summary>Exige un token cuyo ambito sea la plataforma, no una empresa.</summary>
    public const string Plataforma = "plataforma";

    /// <summary>Exige un token de empresa. Un token de plataforma NO sirve aqui.</summary>
    public const string Empresa = "empresa";
}
