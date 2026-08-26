namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Lo que hace falta para dar de alta una empresa.
/// </summary>
/// <param name="Slug">Lo que la gente escribira en el campo "Empresa" al entrar.</param>
/// <param name="CorreoAdministrador">A quien se le manda la invitacion.</param>
/// <param name="CodigoPlan">Que plan contrata. Determina sus modulos.</param>
public readonly record struct AltaDeEmpresa(
    string Slug,
    string RazonSocial,
    string? NombreComercial,
    string? Rfc,
    string? Telefono,
    string? CorreoContacto,
    string CorreoAdministrador,
    string NombreAdministrador,
    string CodigoPlan);

/// <summary>
/// Lo que hace falta para REINTENTAR un alta que quedo en Fallida.
///
/// Solo el administrador, porque todo lo demas ya esta guardado en la fila del tenant y
/// volver a aceptarlo abriria la puerta a cambiar la razon social o el plan por la puerta
/// de atras de un reintento.
///
/// Y el administrador si hace falta preguntarlo: la central no guarda a quien se invito
/// —ese usuario vive en la base de la empresa, que en un alta que fallo en el paso 2
/// todavia no existe—. Si la base ya tiene un administrador con acceso total, el sembrador
/// reusa ese y IGNORA lo que venga aqui, para que un reintento no pueda crear una segunda
/// cuenta con acceso total.
/// </summary>
public readonly record struct ReintentoDeAlta(
    string CorreoAdministrador,
    string NombreAdministrador);

/// <summary>Resultado del alta, para que el panel sepa que mostrar.</summary>
/// <param name="LigaInvitacion">
/// Solo se devuelve en desarrollo. En produccion la liga va unicamente por correo: si
/// se devolviera en la respuesta, cualquiera con acceso al panel podria tomar la sesion
/// del administrador de un cliente.
/// </param>
public readonly record struct EmpresaAprovisionada(
    Guid TenantId,
    string Slug,
    string NombreBd,
    string VersionEsquema,
    bool InvitacionEnviada,
    string? LigaInvitacion);
