# Plan de desarrollo

---

## 1. Método: rebanadas verticales

Cada módulo se termina **de punta a punta** antes de pasar al siguiente:

```
Entidad → Migración → Caso de uso → Endpoint → Pruebas → Pantalla Angular → Funciona
```

**No** "todo el backend y luego todo el frontend". Con 30 módulos, esa separación significa seis meses sin nada demostrable, y descubrir en el mes siete que la API no da lo que la pantalla necesita.

Cada rebanada terminada es software que se puede mostrar y usar.

---

## 2. Fases

La especificación propone seis fases. Se conservan, pero **hay que insertar una Fase 0** que el documento no contempla, porque fue escrito para una empresa y no para un producto SaaS.

### Fase 0 — Fundación *(nueva)*

Sin esto, todo lo demás se construye sobre arena. Es la fase más importante y la que no se ve.

- Scaffolding de la solución .NET y del workspace Angular
- Neon: base central creada, verificado que permite `CREATE DATABASE`, extensiones disponibles
- Entidades de plataforma: `Tenant`, `Plan`, `PlanLimite`, `Suscripcion`
- Multi-tenancy: `ContextoCentral` y `ContextoEmpresa`, resolución de conexión por empresa, **aprovisionamiento automático** (`CREATE DATABASE` + migraciones + semillas)
- Auth: JWT + refresh, matriz de permisos, semilla de los 9 roles
- Interceptor de auditoría
- Abstracción de almacenamiento de archivos (implementación en disco)
- Manejo global de errores, logging estructurado, health checks
- Shell de Angular: layout, login, guards, interceptores, navegación
- Migración inicial y datos semilla de catálogos globales

**Criterio de salida:** un superadministrador da de alta una empresa desde el panel, el sistema le crea y migra su base automáticamente, se envía la invitación al primer administrador, esa persona define su contraseña e inicia sesión con `empresa / correo / contraseña`. Y el comando `migrar-empresas` aplica una migración nueva a todas las bases existentes reportando el resultado por empresa.

### Fase 1 — Núcleo *(M2, M3, M4, M5, M7, M24, M25)*

Equipos con expediente, clientes, obras, tarifas, sucursales y patios, disponibilidad con el constraint de exclusión, cotizaciones y rentas.

**Criterio de salida:** el ciclo completo cotizar → aprobar → rentar → cerrar funciona, y es imposible rentar dos veces el mismo equipo en fechas traslapadas.

Al terminar esta fase el sistema **ya es vendible** como producto mínimo. Todo lo demás aumenta su valor, pero esto es lo que resuelve el dolor principal del cliente.

### Fase 2 — Operación *(M6, M8, M9, M10, M11, M12)*

Contratos, logística y fletes, inspecciones de salida y devolución, evidencias, horómetros y kilometraje, daños.

### Fase 3 — Taller *(M13 a M18)*

Mantenimiento preventivo y correctivo, órdenes de trabajo, próximo servicio, inventario de refacciones, compras, proveedores.

### Fase 4 — Finanzas *(M19, M20, M27 parcial)*

Pagos y cobranza, saldos, `MovimientoCosto`, integración con PAC para CFDI, reportes de rentabilidad.

### Fase 5 — Campo *(M29 y PWA)*

PWA con offline, sincronización, GPS, firmas en pantalla, QR de equipos. Es la fase técnicamente más difícil (resolución de conflictos offline) y la que más diferencia al producto en el mercado.

### Fase 6 — Inteligencia

Predicción de fallas, pricing dinámico, recomendaciones, analítica avanzada. Requiere datos históricos reales, así que no puede adelantarse.

### Transversales

`Dashboard` (M1), `Notificaciones` (M26) y `Reportes` (M27) **no son fases**: cada fase agrega sus propios indicadores, alertas y reportes al cerrar. Un dashboard construido en la Fase 1 no tendría de dónde leer.

---

## 3. Sobre el tamaño real de esto

Conviene decirlo con claridad: **75 entidades y 30 módulos es un ERP vertical.** Comparable en alcance a un sistema como Odoo o SAP Business One en su nicho.

Las Fases 0 y 1 son el hito que importa — producen un producto vendible. Las fases 2 a 4 son donde vive la mayor parte del trabajo. La 5 y la 6 son diferenciación.

Dos consecuencias prácticas:

1. **La Fase 1 debe cerrarse y ponerse frente a un cliente real antes de empezar la Fase 2.** El documento tiene huecos importantes (ver `04-pendientes.md`) que solo el uso real resuelve. Construir las seis fases antes del primer usuario es la forma más eficiente de construir seis fases equivocadas.
2. **No hay que empezar los 30 módulos.** Hay que terminar 8 y venderlos.

---

## 4. Fase 0 — desglose de arranque

Orden concreto de los primeros pasos:

| # | Paso | Notas |
|---|---|---|
| 1 | Cuenta de Neon, proyecto `maquinaria` en `us-east`, rama `dev` | Guardar **ambas** cadenas: pooled y directa |
| 2 | Repo `maquinaria_back`: `git init`, `.gitignore`, mover `docs/` | |
| 3 | Solución .NET y los 4 proyectos + 2 de pruebas | Estructura de `01-arquitectura.md` §4 |
| 4 | Instalar Angular CLI y `dotnet-ef` | `npm i -g @angular/cli` |
| 5 | Repo `maquinaria_front`: workspace Angular | `ng new` inicializa su propio git |
| 6 | Verificar `btree_gist` en Neon | Bloqueante: de ahí depende toda la regla de no-traslape |
| 7 | `ContextoCentral` + sus 5 entidades + primera migración | `05-esquema-fase0.md` §3 |
| 8 | `ContextoEmpresa` + sus 10 entidades + su primera migración | `05-esquema-fase0.md` §4 |
| 9 | Aprovisionamiento: `CREATE DATABASE`, migrar, sembrar permisos y roles | §5 — ojo con los cuatro problemas de la secuencia |
| 10 | Comando `migrar-empresas` + `version_esquema` + endpoint de salud | Resistente a fallos parciales |
| 11 | Resolución de conexión por empresa + interceptor de auditoría | |
| 12 | Auth completo | Login con empresa/correo/contraseña, JWT, refresh rotativo, invitaciones |
| 13 | Shell de Angular + login funcionando | Primer extremo a extremo |

Ya **no** se instala PostgreSQL local: desarrollamos contra una rama de Neon, para que el entorno de desarrollo se comporte como producción.

Los pasos 1 y 4 requieren instalar o registrar cosas fuera del código. Los demás son desarrollo.
