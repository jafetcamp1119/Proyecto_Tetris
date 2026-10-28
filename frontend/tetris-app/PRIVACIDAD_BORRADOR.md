# Revisión de privacidad de Proyecto Tetris

La página pública preparada está en `src/paginas-publicas/privacidad/index.html`; sus estilos están en `privacidad.css`, en la misma carpeta. Este documento conserva el nombre del borrador original como registro de revisión; el texto que se publicará es el HTML, no este Markdown.

## Información confirmada por el responsable

- Responsable: **Equipo de desarrollo de Proyecto Tetris**.
- Contacto público: **jafetcamp1119@gmail.com**.
- Proyecto académico universitario dirigido principalmente a estudiantes y docentes.
- Sin edad mínima formalmente definida.
- Sin plazos generales de conservación definidos.

## Tratamiento comprobado en el código vigente

| Información | Funcionamiento comprobado | Evidencia principal |
| --- | --- | --- |
| Identidad de jugador | Nickname, normalización para unicidad, ID, fecha de registro y estado de cuenta. Registro actual sin contraseña ni correo obligatorio. | `LogicaNegocio/Implementacions/AccesoUsuarioLJ.cs`, `Dominio/Entidades/Usuario.cs` |
| Credencial de dispositivo | Secreto generado por el juego; no es un identificador de hardware. Android usa almacenamiento seguro; web recuerda ID, nickname y credencial en almacenamiento local. El servidor guarda su hash, relación con usuario y fechas de creación, vencimiento y revocación. | `autenticacion.servicio.ts`, `CredencialDispositivo.cs`, `AccesoUsuarioLJ.cs` |
| Sesión de acceso | JWT temporal en memoria; renovación con credencial del dispositivo y una sesión activa por cuenta. Un ingreso explícito desplaza el control anterior y conserva el perfil. | `autenticacion.servicio.ts`, `Seguridad/ServicioToken.cs`, `ConexionesSesion.cs` |
| Código de recuperación | Se guarda una huella protegida y vencimiento. Regenerar desde Perfil reemplaza el anterior; recuperar genera credencial y código pendiente, que reemplaza al anterior solo al confirmar que fue guardado. Si se pierde la respuesta, el anterior sigue disponible. No borra la cuenta. | `AccesoUsuarioLJ.cs` |
| Google vinculado | Se valida el ID token. Se guardan `GoogleSub` y, cuando es válida, la URL de `AvatarGoogle`. El flujo de vinculación no asigna `Correo` ni cambia el nickname. | `Controllers/GoogleController.cs`, `AccesoUsuarioLJ.VincularGoogle`, `google.servicio.ts` |
| Datos Google recibidos | El token puede incluir nombre/correo. Eso no demuestra que se copien al perfil. No se describen como campos almacenados por el flujo actual. El modelo tiene un campo Correo que podría contener datos de versiones anteriores; no se inspeccionó la base de datos real. | `Usuario.cs`, `GoogleController.cs` |
| Partidas e historial | Salas activas en memoria; persistencia de partidas, participación, fechas, modo, configuración, puntuaciones, líneas, nivel, posición y resultados. Se acumulan estadísticas de juego. | `LogicaNegocio/Servicios/RegistroPartidaLJ.cs` |
| Visibilidad | Nicknames/estado/resultados de sala para participantes. La API de ranking, protegida por autenticación, devuelve ID de jugador y estadísticas agregadas; no afirmar que publica emails o avatares. | `Controllers/ResultadosController.cs`, `Implementacions/ResultadoLJ.cs`, servicios de salas |
| Preferencias y navegación | Audio guardado por jugador en localStorage; referencia temporal de sala en sessionStorage. | `audio.servicio.ts`, `salas.servicio.ts` |
| Cierre y perfiles | Cambiar jugador cambia el activo; quitar perfil elimina la referencia local; cerrar sesión revoca esa credencial. Ninguna de esas acciones elimina cuentas/historial. | `autenticacion.servicio.ts`, `AccesoUsuarioLJ.Revocar` |
| Datos técnicos | IP usada en rate limiting; logs de fallos en servidor. Hosting y túnel reciben solicitudes de conexión. No se verificaron retención, región o copias de seguridad de las cuentas de los proveedores. | `ConfiguracionApi.cs`, configuración de logging, archivos de Hosting y URL pública existente |

Las rutas de backend de esta tabla son relativas a `Backend/ProyectoTetris_PrograV`; los servicios Angular están en `frontend/tetris-app/src/app/servicios`. No se modificó el backend ni se ejecutaron consultas SQL.

## Conservación real y decisiones pendientes

El código actual no ofrece una eliminación general automática de cuentas o estadísticas por antigüedad. Las credenciales y códigos tienen vencimiento de validez, que no equivale a depurar todos sus registros. Cerrar sesión registra la revocación; no elimina la cuenta. Al reiniciar el backend se cancelan partidas incompletas y pueden retirarse resultados provisionales: eso no constituye una política general de borrado del historial. Las salas en memoria tampoco son una copia persistente del estado completo de una partida.

Antes de considerar definitiva la política, el equipo debe decidir o confirmar:

1. Plazos y criterios de conservación de cuentas, historial y estadísticas, y su aplicación práctica.
2. Procedimiento de atención de solicitudes de acceso/corrección/eliminación, validación de identidad y tiempos de respuesta. Actualmente no hay una función de eliminación de cuenta/historial en la aplicación.
3. Edad mínima y condiciones para participación de menores.
4. Configuración real de registros técnicos, accesos administrativos, copias de seguridad y conservación en servicios/proveedores.
5. Si la base existente conserva correos u otros datos heredados de versiones anteriores; esta tarea revisó código, no datos personales reales.

El nombre del equipo y el correo fueron confirmados por el usuario e incorporados a la página. No se inventaron responsables, plazos, eliminación automática ni garantías sobre proveedores. Los puntos pendientes se muestran en la propia política, sin prometer una aprobación de Google.

## Publicación preparada

Angular copia `src/paginas-publicas` al directorio `www`. La política es HTML estático sin formularios, scripts, dependencias del backend ni guard de sesión. Hosting sirve `/privacidad` (redirección normal a `/privacidad/`) y `/privacidad/index.html`. Se conserva el fallback de Angular. Los enlaces de las pantallas inicial y de menú apuntan directamente al archivo estático para evitar depender del enrutador o la autenticación.

URL para Google Auth Platform > Branding, después de que publiques y compruebes la página:

**https://proyecto-tetris-512517.web.app/privacidad**

Página principal: **https://proyecto-tetris-512517.web.app/**. Se mantuvo el diseño de inicio y se añadió una explicación de las piezas, líneas, puntos, modos, nickname y Google opcional.

Desde `C:\Users\jafet\Documents\GitHub\Proyecto_Tetris\frontend\tetris-app`, cuando decidas publicar:

```powershell
npm run build
npm run verificar:publicacion
firebase deploy --only hosting --project proyecto-tetris-512517
```

Detenerse si falla alguno de los pasos. No se ejecutó deploy ni autenticación personal. La URL real del backend que ya estaba configurada no se cambió. Completar/confirmar las decisiones anteriores antes de presentar la política como definitiva.

La guía oficial de Google exige una página principal pública que describa la aplicación y enlace su política; crear esta página no acredita por sí solo verificación de marca o dominio: [Guía de Google](https://developers.google.com/identity/protocols/oauth2/production-readiness/policy-compliance).

## Comprobaciones de esta tarea

- Angular: 41 pruebas correctas en 13 archivos, incluyendo visibilidad del enlace de privacidad y descripción del juego en la pantalla inicial.
- Firebase Hosting Emulator con proyecto demo: `/privacidad`, `/privacidad/`, `/privacidad/index.html` y CSS responden correctamente; la política se entrega sin credenciales y contiene el texto completo sin ejecutar JavaScript. `/` y `/login` conservan la aplicación Angular.
- La comprobación predeploy se amplió para exigir los dos archivos de privacidad en `www`.
- No se hizo deploy, commit ni push. No se modificaron backend, Google Sign-In, reglas del juego, SQL, logos, QR ni inicio desde Visual Studio.

Para repetir la prueba de Hosting después de compilar:

```powershell
firebase emulators:exec --only hosting --project demo-proyecto-tetris "node scripts/VerificarPrivacidad.cjs"
```

Resultado final adicional: `npm run build` y `npm run verificar:publicacion` correctos. `npm run lint` termina con 0 errores y 6 advertencias previas sobre interfaces de ciclo de vida. La compilación mantiene el aviso previo de navegadores antiguos fuera del soporte de Angular. No quedaron advertencias nuevas de presupuesto CSS.

Archivos creados en esta tarea: `src/paginas-publicas/privacidad/index.html`, `src/paginas-publicas/privacidad/privacidad.css` y `scripts/VerificarPrivacidad.cjs`.
Archivos actualizados: `angular.json`, `firebase.json`, `scripts/VerificarPublicacion.cjs`, `src/app/login/login.page.html`, `src/app/login/login.page.spec.ts`, `src/app/home/home.page.html`, `src/app/home/home.page.scss`, `src/global.scss` y este documento.
