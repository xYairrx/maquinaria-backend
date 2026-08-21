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
