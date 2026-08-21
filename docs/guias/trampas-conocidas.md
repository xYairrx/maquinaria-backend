# Trampas conocidas

Errores ya pagados en este proyecto. Cada uno costó tiempo la primera vez.

## Tooling y build

### `dotnet new sln` genera `.slnx`, no `.sln`

En .NET 10 el formato por defecto cambió. Cualquier comando que diga `Maquinaria.sln` falla con:

```
MSBUILD : error MSB1009
```

Usa siempre `Maquinaria.slnx`. Visual Studio 2026 lo abre nativamente.

### `dotnet sln add` solo busca la solución en el directorio actual

Corre desde la raíz del repo, no desde un subdirectorio.

### `Get-ChildItem` pierde la ruta al pasarse a un ejecutable externo

```powershell
# FALLA: "No se encuentra el proyecto o directorio Maquinaria.Api.csproj"
dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj)

# CORRECTO
dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj | Select-Object -ExpandProperty FullName)
```

### cmd no entiende cmdlets de PowerShell

Un `.ps1` sí corre desde cmd:

```
powershell -ExecutionPolicy Bypass -File .\scaffold.ps1
```

### `Microsoft.OpenApi` 2.0.0 tiene una vulnerabilidad alta

```
warning NU1903: El paquete "Microsoft.OpenApi" 2.0.0 tiene una vulnerabilidad
de gravedad alta conocida  (GHSA-v5pm-xwqc-g5wc)
```

Llega como dependencia transitiva de `Microsoft.AspNetCore.OpenApi`. Está resuelto fijándola en 2.12.0 en `Directory.Packages.props` con transitive pinning; **no la bajes**.

Corre la auditoría antes de cada despliegue, y con `--include-transitive`, que es la parte que importa:

```bash
dotnet list package --vulnerable --include-transitive
```

### Central Package Management rechaza `Version` en los `.csproj`

Con `ManagePackageVersionsCentrally` activo, los proyectos declaran el paquete sin versión:

```xml
<!-- antes -->
<PackageReference Include="xunit" Version="2.9.3" />

<!-- ahora -->
<PackageReference Include="xunit" />
```

Los paquetes nuevos se agregan editando el `.csproj` y registrando la versión en `Directory.Packages.props`.

---

## Base de datos

### Npgsql no acepta cadenas en formato URI

El `postgresql://usuario:password@host/base` que Neon muestra por defecto produce un error de parseo que no menciona el formato y parece un problema de credenciales. Ver [configuración](configuracion.md#formato-de-la-cadena).

### Invertir las dos cadenas de conexión

La API funciona perfecto y el error aparece semanas después, al correr la primera migración o dar de alta la primera empresa, porque PgBouncer en modo transacción no soporta DDL. Ver [configuración](configuracion.md#las-dos-cadenas).

### `CREATE DATABASE` no corre dentro de una transacción

Es una limitación de PostgreSQL, y EF Core envuelve en transacción por defecto. Hay que abrir una `NpgsqlConnection` directa y ejecutar el comando fuera de transacción.

### El nombre de la base no se puede parametrizar

Los identificadores SQL no aceptan parámetros, así que la sentencia se arma concatenando. Sin validar el formato con regex en C# antes de concatenar, el nombre es un vector de inyección. Los `CHECK` de formato en la tabla `tenant` son control de seguridad, no cosmética — y **no basta con la validación de la base**.

### La región de Neon no se puede cambiar

Se elige al crear el proyecto y es definitiva. Railway US East está en Virginia, no en Ohio. Ver [configuración](configuracion.md#neon).

### La rama de Neon se autoborra

El default de auto-delete es *After 1 day*. Una rama de desarrollo necesita **Never**.

### Neon suspende el cómputo en plan gratuito

Tras unos minutos de inactividad. La primera consulta después de la pausa tarda. Aceptable en desarrollo, no para una demo con cliente.

---

## Git

### Dos raíces sin ancestro común

Si el repo local se inicializa con `git init` y el remoto de GitHub ya tenía su propio commit inicial, los historiales no comparten ancestro. El push se rechaza con `fetch first`, y el `git pull` que sugiere el hint **también falla**, con `refusing to merge unrelated histories`.

Con un solo commit local, replantarlo deja historial lineal sin merge commit ni `--force`:

```bash
git rebase --onto origin/<rama> --root <rama>
```

### `.gitignore` de .NET no excluye `appsettings`

Ni `appsettings.json` ni `appsettings.Development.json`. Se commitean por defecto, así que nunca pongas secretos ahí. Ver [configuración](configuracion.md#dónde-van-los-secretos).
