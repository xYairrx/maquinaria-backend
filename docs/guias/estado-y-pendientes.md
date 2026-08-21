# Estado y pendientes

Última verificación: 2026-08-20.

## Estado actual

**Solo andamiaje.** `dotnet build` en verde con 0 advertencias, sin paquetes vulnerables, y `/openapi/v1.json` responde con `"paths": { }` — todavía no hay un solo endpoint de negocio ni una sola entidad.

### Hecho

- [x] Neon: proyecto `maquinaria`, rama `dev`, región N. Virginia confirmada, extensiones verificadas
- [x] Verificado que Neon permite `CREATE DATABASE` — bloqueante del modelo multi-database
- [x] Solución `Maquinaria.slnx` con los 6 proyectos y sus referencias
- [x] Central Package Management con transitive pinning
- [x] Paquetes de EF Core, Npgsql y OpenAPI
- [x] `dotnet user-secrets init` en `Maquinaria.Api`

### Pendiente de Fase 0

- [ ] `Dockerfile` para el despliegue en Railway
- [ ] `ContextoCentral` + sus 5 entidades + primera migración
- [ ] `ContextoEmpresa` + sus 10 entidades + su migración
- [ ] Servicio de aprovisionamiento y comando `migrar-empresas`
- [ ] Resolución de conexión por empresa + interceptor de auditoría
- [ ] Auth completo: login por empresa/correo/contraseña, JWT, refresh rotativo, invitaciones
- [ ] Manejo global de errores, logging estructurado, health checks
- [ ] Abstracción de almacenamiento de archivos con implementación en disco
- [ ] Remotos de GitHub y convenciones de equipo: ramas, commits, revisión, acceso a Neon

### Criterio de salida de Fase 0

Un superadministrador da de alta una empresa desde el panel, el sistema le crea y migra su base automáticamente, se envía la invitación al primer administrador, esa persona define su contraseña e inicia sesión con `empresa / correo / contraseña`. Y el comando `migrar-empresas` aplica una migración nueva a todas las bases existentes reportando el resultado por empresa.

### Orden de trabajo

El siguiente paso es el **7** del plan: `ContextoCentral` con sus 5 entidades (`plan`, `plan_limite`, `tenant`, `suscripcion`, `usuario_plataforma`) y su primera migración. El DDL de referencia está en [`05-esquema-fase0.md`](../05-esquema-fase0.md) §3.

El método es por **rebanadas verticales**: `Entidad → Migración → Caso de uso → Endpoint → Pruebas → Pantalla Angular → Funciona`. No "todo el backend y luego todo el frontend".

---

## Decisiones abiertas

Cuatro huecos que los documentos de diseño no cierran y que conviene resolver **antes** de la primera migración, porque la regla *append-only* los vuelve irreversibles.

### 1. Carpeta y nomenclatura de las migraciones

Con dos `DbContext` en el mismo assembly hay que separarlas físicamente. La forma sería:

```bash
dotnet ef migrations add <Nombre> --context ContextoCentral --output-dir Migraciones/Central --project src/Maquinaria.Infraestructura --startup-project src/Maquinaria.Api
```

Ningún documento fija la carpeta ni un prefijo de nombres. Renombrarlas después viola la regla *append-only*.

### 2. Mapeo PascalCase → snake_case

No está definido si se usa `EFCore.NamingConventions` (con `UseSnakeCaseNamingConvention()`), una convención global propia, o `HasColumnName` explícito por propiedad. El paquete no aparece en la lista de dependencias de los documentos.

### 3. `IDesignTimeDbContextFactory` para `ContextoEmpresa`

Ese contexto no tiene cadena fija — se resuelve por petición — así que `dotnet ef migrations add` no puede instanciarlo solo. Ningún documento lo menciona, pero hace falta.

### 4. Nombre de la base central

Ningún documento lo fija; queda implícito en la cadena de conexión, y en Neon el default es `neondb`. Las bases de empresa sí tienen patrón definido: `maquinaria_<slug>`.

---

## Restricciones del aprovisionamiento

Cuando se implemente el alta de empresas, la secuencia es:

```
1. INSERT en tenant                    → estado_aprovisionamiento = Pendiente
2. CREATE DATABASE maquinaria_<slug>   → Creando
3. Migraciones de ContextoEmpresa en esa base
4. Semillas: permisos, los 9 roles, parametros por defecto
5. Crear el primer usuario administrador (sin contrasena)
6. Emitir su token de invitacion y enviarlo
7. estado_aprovisionamiento = Lista, version_esquema = <ultima migracion>
```

Cuatro restricciones técnicas que el código debe respetar desde el día uno:

1. **`CREATE DATABASE` no corre dentro de una transacción** y EF Core envuelve en transacción por defecto. Hay que abrir una `NpgsqlConnection` directa contra la central y ejecutar el comando fuera de transacción.
2. **El nombre de la base no se puede parametrizar** en SQL, así que se concatena. Revalidar el formato con regex en C# antes de concatenar es control de seguridad, no cosmética.
3. **Los pasos 1 y 2 no son atómicos.** `estado_aprovisionamiento = Fallida` deja el registro reintentable en lugar de un huérfano.
4. **Es lento.** Al inicio va en línea; después conviene moverlo a un `BackgroundService` y que la UI consulte `estado_aprovisionamiento`.

---

## Divergencias con los documentos de diseño

Los documentos de [`docs/`](../) son especificación, no inventario. Diferencias detectadas al 2026-08-20:

| Documento dice | Realidad |
|---|---|
| Repos `maquinaria_back` y `maquinaria_front` | `maquinaria-backend` y `maquinaria-frontend` |
| Contenedor en `Documents/Maquinaria/` | `OneDrive/Desktop/maquinaria/` |
| Frontend en Angular 22 / CLI 22.1.4 | Angular 21.2.21 |
| Checklist marca el andamiaje del backend como hecho | Se creó el 2026-08-20 |

Verifica siempre contra el repo antes de asumir que algo está hecho.
