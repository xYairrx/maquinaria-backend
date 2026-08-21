namespace Maquinaria.Dominio.Seguridad;

/// <summary>
/// Un rol de la empresa. Los nueve del modulo 25 son semilla que cada empresa
/// renombra y ajusta.
/// </summary>
public class Rol
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Codigo { get; set; }

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>
    /// Marca los roles SEMILLA: los nueve. Impide borrarlos y dejar a la empresa
    /// sin su estructura de roles.
    ///
    /// OJO: es_sistema por si solo NO concede nada. Los nueve lo traen en true, y
    /// si tambien significara "salta la verificacion", ventas, operador y cliente
    /// saltarian la verificacion. Ese poder vive en <see cref="AccesoTotal"/>, en
    /// una columna aparte, por eso mismo.
    /// </summary>
    public bool EsSistema { get; set; }

    /// <summary>
    /// SALTA LA VERIFICACION DE PERMISOS. True solo en 'administrador'.
    ///
    /// Es una columna y no una comparacion contra el codigo del rol porque las
    /// empresas renombran los roles: si la verificacion preguntara por la cadena
    /// 'administrador', un rename legitimo dejaria a la empresa sin quien
    /// administre, y alguien podria crear un rol con ese nombre y ganarse el poder.
    ///
    /// Tres garantias en la base, no en codigo:
    ///
    /// 1. Un indice unico parcial permite COMO MAXIMO UNA fila con AccesoTotal, asi
    ///    que no se puede crear un segundo rol con acceso total.
    /// 2. Un trigger rechaza UPDATE y DELETE sobre la fila que trae EsSistema y
    ///    AccesoTotal, asi que ese rol no se puede editar, borrar, ni apagarle el
    ///    acceso. Y como no se puede apagar, la regla "debe quedar al menos un rol
    ///    con acceso total" se cumple sola.
    /// 3. El trigger apunta a EsSistema AND AccesoTotal, no a EsSistema solo: los
    ///    otros ocho lo traen y tienen que seguir siendo renombrables.
    ///
    /// La contrapartida de este diseno, asumida: la empresa tiene exactamente UNA
    /// persona con acceso total, la que se crea al aprovisionar. Si esa persona se
    /// va, solo la plataforma puede nombrar otra, y esa operacion se audita con
    /// origen = 'plataforma'.
    /// </summary>
    public bool AccesoTotal { get; set; }

    public DateTime CreadoEn { get; set; }

    public ICollection<RolPermiso> Permisos { get; } = [];

    public ICollection<UsuarioRol> Usuarios { get; } = [];
}
