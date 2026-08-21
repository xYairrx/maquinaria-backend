# Convenciones y reglas duras

## Convenciones

| Tema | Regla |
|---|---|
| Solución | `Maquinaria.slnx` (formato .NET 10, **no** `.sln`) |
| Paquetes | Central Package Management: las versiones solo viven en `Directory.Packages.props`; los `.csproj` declaran el paquete **sin** atributo `Version` |
| Nombres en BD | `snake_case`, tablas en **singular** y sin prefijos: `usuario`, `sesion_refresh`, `rol_permiso` |
| Nombres en C# | `PascalCase`, singular, en español |
| Identificadores | Sin acentos ni `ñ`: `hash_contrasena`, `tamano_bytes` |
| Llaves primarias | `uuid` v7 generado en C# con `Guid.CreateVersion7()`, no en la base |
| Llaves de tablas puente | PK compuesta de las dos FK, sin columna `id` sintética |
| Dinero | `numeric(18,4)`, **nunca** `float`/`double`. Cuatro decimales por las tarifas por hora |
| Fechas | `timestamptz`, siempre UTC; la zona de presentación se guarda por tenant |
| Enums | `smallint` en BD + `enum` en C# con valores explícitos **empezando en 1** |
| Rangos | Dos columnas (`inicio`, `fin`), no una columna `tstzrange` |
| Borrado | Lógico, con `eliminado_en`, en entidades de negocio |
| Índices | Prefijo `ix_`, y parciales con `WHERE eliminado_en IS NULL` donde aplique |
| Casos de uso | Clases con un método `Ejecutar`, registradas en DI |
| Permisos | Cadenas `modulo.accion`: `equipos.editar`, `rentas.autorizar` |
| Auditoría | `SaveChangesInterceptor` de EF Core que escribe en `auditoria` con `jsonb` |
| Terminal | PowerShell |

Tres decisiones que parecen arbitrarias y no lo son:

- **Los enums empiezan en 1**, no en 0, porque los `CHECK` y los `EXCLUDE` parciales del esquema dependen de esos números literales (`WHERE estado IN (1, 2)`).
- **Los rangos son dos columnas** porque `tstzrange` solo se mapea en C# con `NpgsqlRange<T>`, de Npgsql, y `Maquinaria.Dominio` no puede depender de infraestructura. Los `EXCLUDE` aceptan la expresión `tstzrange(inicio, fin)`, que es equivalente.
- **Los uuid se generan en C#**, no con `gen_random_uuid()`, porque habilita IDs generados en cliente para la PWA offline de Fase 5.

### Organización por módulo

`Maquinaria.Aplicacion` se organiza por módulo, no por tipo técnico:

```
Aplicacion/
├── Equipos/           ← CrearEquipo.cs, ObtenerExpediente.cs, EquipoDto.cs
├── Rentas/
├── Disponibilidad/
└── Mantenimiento/
```

Con 26 módulos previstos, carpetas `Services/`, `Repositories/` y `Validators/` se vuelven inmanejables. Feature folders mantienen junto lo que cambia junto.

### Sin MediatR

Los casos de uso son clases con un método `Ejecutar`, registradas en el contenedor de DI. MediatR pasó a licencia comercial en 2025; menos magia y menos dependencias dan el mismo resultado.

---

## Reglas duras

Cinco invariantes que no se negocian caso por caso.

### 1. Nada de DDL manual en la base

Todo cambio de esquema pasa por una migración de EF Core versionada, extensiones incluidas. El SQL de [`05-esquema-fase0.md`](../05-esquema-fase0.md) es documento de diseño; la verdad son las clases de C#.

Las extensiones y las semillas van con `migrationBuilder.Sql(...)`, porque EF Core no sabe expresar extensiones ni datos semilla condicionales.

### 2. Las migraciones nunca se aplastan ni se reescriben

Después de un release, jamás. Una empresa puede estar dos versiones atrás y tiene que poder alcanzar. El historial es *append-only* para siempre.

Corolario operativo: como las migraciones de `ContextoEmpresa` se aplican **N veces**, una por base, hace falta `tenant.version_esquema`, el comando `migrar-empresas` resistente a fallos parciales, y un endpoint de salud que reporte quién quedó atrasado. Sin ese endpoint el desfase es invisible hasta que algo truena.

### 3. Una sola base de código

Nunca un fork "para on-premise". La diferencia entre modalidades es **configuración, no código**.

### 4. Toda dependencia de la nube va detrás de una abstracción

`IAlmacenamientoArchivos` existe por esto, con `AlmacenamientoDisco` para desarrollo y `AlmacenamientoS3` para producción. **Falta la abstracción equivalente para envío de correo**: un cliente on-premise usará su propio SMTP.

Ninguna instalación puede depender de un servicio central nuestro para funcionar. Esa es la razón definitiva por la que no hay un servicio de identidad central: un SSO hospedado por nosotros haría imposible el on-premise.

### 5. El multi-tenant se queda aunque haya un solo tenant

Una fila en `tenant`, y ya.

---

## Consecuencias del modelo multi-database

El aislamiento es **físico**, no por columna. Lo que eso elimina:

| Ya no existe | Por qué |
|---|---|
| `tenant_id` en cada tabla de negocio | Cada base tiene un solo cliente |
| Row-Level Security y `FORCE ROW LEVEL SECURITY` | El aislamiento es físico |
| Interceptor de `SET LOCAL app.tenant_id` | — |
| Rol de base de datos separado para la app | — |
| La prueba de fuga entre tenants | No hay fuga posible |
| Transacción explícita obligatoria por request | Era requisito del `SET LOCAL` |
| `UNIQUE (tenant_id, correo)` | Basta `UNIQUE (correo)` |

Única excepción: `suscripcion.tenant_id` en la base central, donde sí es una FK legítima.

Las transacciones se usan donde el negocio las necesita, no como requisito de infraestructura.

---

## Reglas que garantiza PostgreSQL, no el código

La principal: **un equipo no puede tener dos rentas traslapadas**, impuesto con `EXCLUDE USING gist` sobre `tstzrange(inicio, fin)` — de ahí que `btree_gist` sea una extensión bloqueante del proyecto. Validarlo con un `if` en C# falla bajo concurrencia.

---

## Archivos

Nunca se sirven a través de la API: se entregan **URLs firmadas de vigencia corta**. Las rutas van siempre prefijadas por tenant:

```
{tenantId}/equipos/{equipoId}/inspecciones/{inspeccionId}/{archivoId}.jpg
```

El consumo de almacenamiento por tenant se lleva en la tabla `archivo`, no consultando el bucket: R2 no da tamaño por prefijo de forma económica. La compresión y las miniaturas se hacen del lado del cliente antes de subir.

---

## Permisos

Matriz, no enum de roles:

```
Usuario ──< UsuarioRol >── Rol ──< RolPermiso >── Permiso
                                                     │
                                          "equipos.editar"
                                          "rentas.autorizar"
```

Seis acciones por módulo: consultar, crear, editar, eliminar, autorizar, exportar. Se resuelven al iniciar sesión y viajan en el JWT.

Cada tenant tiene sus propios roles: los 9 del catálogo son una **semilla** que se copia al crear la base, no un enum fijo. El rol administrador lleva `es_sistema = true` para que no se pueda borrar y dejar a la empresa sin acceso.

Los permisos son parte del **código**: existen porque hay un endpoint que los verifica. Cada migración que agrega un módulo agrega también sus permisos, y actualizar el catálogo implica propagar la semilla con una migración, no un `UPDATE` central.

No hay registro público. Los tenants los da de alta un superadministrador y los usuarios se crean por invitación con token de un solo uso.

---

## Seguridad del login

El login pide tres campos: **empresa (el slug), correo y contraseña**. Tres reglas obligatorias:

- **Un solo mensaje de error.** Nunca distinguir entre empresa inexistente, correo inexistente y contraseña incorrecta.
- **Tiempo de respuesta constante.** Ejecutar siempre un hash señuelo (~200 ms) aunque no haya usuario.
- **Límite de intentos** por combinación de slug + correo, y por IP.

El refresh token va en cookie `HttpOnly`, nunca en `localStorage`, donde cualquier XSS lo robaría.
