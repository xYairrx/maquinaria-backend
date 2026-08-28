using System.Reflection;
using Maquinaria.Api.Arranque;
using Maquinaria.Api.Seguridad;
using Maquinaria.Aplicacion.Comun;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Tests.Seguridad;

/// <summary>
/// Las dos pruebas que vigilan la matriz de permisos desde fuera, por reflexion sobre los
/// controladores. La segunda es la que de verdad importa.
///
/// Existen porque el bucle de policies solo detecta una clave mal escrita cuando alguien
/// llega al endpoint, y un endpoint sin permiso no lo detecta nada en absoluto: contesta 200
/// a cualquiera con sesion. Con cincuenta endpoints por venir, eso es cuestion de tiempo.
/// </summary>
public class PermisosDeclaradosPruebas
{
    private static readonly Assembly Api = typeof(OrigenesPermitidos).Assembly;

    private static IEnumerable<Type> Controladores => Api.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

    private static IEnumerable<MethodInfo> Acciones(Type controlador) => controlador
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName);

    /// <summary>
    /// Toda clave exigida existe en el catalogo. Una que no exista no tiene policy
    /// registrada, asi que el endpoint responde con una excepcion en la primera peticion.
    /// </summary>
    [Fact]
    public void Cada_permiso_exigido_existe_en_el_catalogo()
    {
        var exigidos = Controladores
            .SelectMany(Acciones)
            .SelectMany(m => m.GetCustomAttributes<RequierePermisoAttribute>())
            .Select(a => a.Clave)
            .Concat(Controladores
                .SelectMany(c => c.GetCustomAttributes<RequierePermisoAttribute>())
                .Select(a => a.Clave))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Si no hay ninguno, la prueba pasaria en falso: en cuanto exista un controlador de
        // negocio tiene que haber al menos uno.
        Assert.NotEmpty(exigidos);

        var desconocidos = exigidos.Where(c => !ClavesPermiso.Existe(c)).ToList();

        Assert.True(
            desconocidos.Count == 0,
            "Estos permisos no existen en ClavesModulo x AccionesPermiso: "
            + string.Join(", ", desconocidos));
    }

    /// <summary>
    /// TODA ACCION DE UN CONTROLADOR DE EMPRESA EXIGE UN PERMISO, con una lista de
    /// excepciones explicita y corta.
    ///
    /// La lista es la parte importante: agregar una excepcion obliga a escribirla aqui, o
    /// sea a justificarla en una revision, en lugar de que un endpoint abierto pase
    /// inadvertido.
    /// </summary>
    [Fact]
    public void Cada_accion_de_empresa_exige_un_permiso()
    {
        // MiSesionController solo lee el token de quien ya esta autenticado y le devuelve su
        // propia identidad. No hay nada que autorizar: el permiso mas fino posible seria
        // «puedes verte a ti mismo».
        var excepciones = new HashSet<string>(StringComparer.Ordinal)
        {
            "MiSesionController.Obtener",
        };

        var abiertas = new List<string>();

        foreach (var controlador in Controladores)
        {
            // Solo los de ambito empresa: los de plataforma se protegen con su policy y no
            // usan la matriz de permisos, que vive en la base de cada empresa.
            var esDeEmpresa = controlador
                .GetCustomAttributes<AuthorizeAttribute>()
                .Any(a => a is not RequierePermisoAttribute
                       && a.Policy == PoliticasAutorizacion.Empresa);

            if (!esDeEmpresa)
            {
                continue;
            }

            var permisoEnLaClase = controlador
                .GetCustomAttributes<RequierePermisoAttribute>()
                .Any();

            foreach (var accion in Acciones(controlador))
            {
                var nombre = $"{controlador.Name}.{accion.Name}";

                if (permisoEnLaClase
                    || accion.GetCustomAttributes<RequierePermisoAttribute>().Any()
                    || excepciones.Contains(nombre))
                {
                    continue;
                }

                abiertas.Add(nombre);
            }
        }

        Assert.True(
            abiertas.Count == 0,
            "Estas acciones de empresa no exigen ningun permiso: "
            + string.Join(", ", abiertas));
    }
}
