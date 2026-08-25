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

### Compilar con la API corriendo, sin matarla

El `MSB3027`/`MSB3021` de "no se puede copiar el archivo" con un proceso de `Maquinaria.Api` vivo ya está descrito en la [bitácora](estado-y-pendientes.md#trampa-de-operación-dos-instancias-de-la-api-a-la-vez). Lo que faltaba escrito es la salida, que no obliga a detener el proceso:

```bash
dotnet build Maquinaria.slnx --nologo -p:BaseOutputPath="<carpeta temporal>\"
```

La salida se redirige a otra carpeta, así que nadie intenta sobrescribir las DLL que el proceso vivo tiene tomadas. Los `obj` se quedan en su sitio y no hay bloqueo.

**No pases `BaseIntermediateOutputPath`.** Es lo primero que uno intenta —mover también el `obj`— y produce `CS0579`: atributos de ensamblado duplicados. Los `.AssemblyInfo.cs` generados terminan contándose dos veces porque los proyectos comparten la carpeta intermedia. El `obj` se queda donde está; lo que se mueve es solo el `bin`.

La barra invertida final del valor no es cosmética: MSBuild concatena la ruta y sin ella pega el nombre del proyecto directamente al último segmento.

**Para las pruebas, compilar primero y luego correrlas sin build:**

```bash
dotnet build Maquinaria.slnx --nologo
dotnet test tests/Maquinaria.Api.Tests --no-build
```

Un `BaseOutputPath` único mete a **todos** los proyectos en la misma carpeta, los dos de prueba incluidos, y ahí un `testhost` vivo bloquea las DLL del otro. Fuera de ese caso el motivo es más simple: `dotnet test` reconstruye por su cuenta y vuelve a chocar con el proceso de la API. Compilar una sola vez y correr con `--no-build` evita las dos cosas.

**Y aplica igual al comando `migrar-empresas`**, que se pisa más seguido porque uno lo lanza sin cerrar la API que tenía levantada. `dotnet run` compila antes de ejecutar, así que el fallo aparece en el build y **no** en la migración, aunque el mensaje no lo aclare:

```bash
dotnet build Maquinaria.slnx --nologo
dotnet run --project src/Maquinaria.Api --no-build -- migrar-empresas
```

Ver [§9 de puesta en marcha](../00-puesta-en-marcha.md#9-el-comando-migrar-empresas).

---

## Ejecución y navegador

### `UseHttpsRedirection` rompe todas las llamadas del navegador en desarrollo

El síntoma engaña, y por eso costó tiempo real. La pantalla de Angular decía *«no se pudo contactar al servidor»* **mientras la API respondía perfectamente a `curl` y a PowerShell**.

Lo que pasaba:

1. El preflight `OPTIONS` salía por `http://localhost:5123` y devolvía **204** — el CORS estaba bien y no era el problema.
2. La petición real se redirigía a `https://localhost:7020`.
3. Ahí el navegador cortaba con **`ERR_CERT_AUTHORITY_INVALID`**, porque el certificado de desarrollo de .NET no está en el almacén de confianza.
4. Angular solo ve un error de red genérico, sin nada que mencione certificados.

`curl` y PowerShell no validan el certificado del mismo modo, así que confirman que la API está sana y refuerzan la sospecha equivocada de que el problema es CORS o el frontend.

**Solución adoptada:** no redirigir en desarrollo. En `Program.cs`, `app.UseHttpsRedirection()` va dentro de un `if (!app.Environment.IsDevelopment())`. En producción la redirección sigue activa, que es donde importa.

**Alternativa igual de válida, descartada por un motivo concreto:** `dotnet dev-certs https --trust` y apuntar el frontend al 7020. Es más parecido a producción, pero exige un paso manual por máquina que nada verifica — y quien lo olvide pierde la tarde con un error que no habla de certificados.

### Dos refrescos simultáneos cierran la sesión entera

El refresco de la sesión de empresa es **rotativo y sin ventana de gracia**: al canjear un token, el viejo queda marcado con `reemplazado_por_id`. Si ese token vuelve a llegar, se lee como **reuso** y se revocan **todas** las sesiones del usuario, que es exactamente lo que debe pasar con una copia robada.

El problema es que dos peticiones concurrentes con el mismo token producen la misma señal. La segunda llega cuando la primera ya lo canjeó. Basta con dos pestañas que despiertan a la vez, o con un reintento automático sobre un `timeout`, y el usuario aparece deslogueado sin ninguna explicación en la pantalla.

**No es un defecto que se vaya a arreglar en el servidor:** una ventana de gracia volvería ambigua la única señal fiable de token robado. La obligación es del cliente, y es **serializar los refrescos** — un solo vuelo en curso y los demás esperando su resultado (*single-flight*). Ya está resuelto en el frontend; quien escriba otro cliente —una PWA, un script— tiene que hacer lo mismo.

Y un detalle que ahorra media hora de diagnóstico: **el rechazo es siempre un 401 con el mismo texto** para seis motivos distintos, a propósito. El log del servidor sí distingue, y el reuso deja un `LogWarning` explícito. Si un cliente se desloguea sin razón aparente, ahí está la respuesta, no en el cuerpo del 401.

### Un subdominio nuevo no necesita tocar el archivo `hosts`

Chrome y Edge resuelven `*.localhost` a `127.0.0.1` de forma nativa, así que `bajio.localhost:4200` funciona sin configurar nada. Lo que sí hace falta es `Cors:DominioBase` en `localhost`; ver [configuración](configuracion.md#corsdominiobase-y-por-qué-no-es-una-lista).

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
