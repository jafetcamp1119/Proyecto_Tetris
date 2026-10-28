# Preparación de publicación y conectividad

Estado: cambios preparados localmente. No se publicó Hosting, no se abrió un túnel y no se generó APK. No se hicieron commits, pushes ni operaciones SQL. Los cambios de juego que ya estaban en el árbol de trabajo se conservaron.

## Configuración implementada

- Desarrollo conserva `environment.ts`, `configuracion-red.ts` y `npm run red:local`. El navegador obtiene el host de la página; Android debug utiliza la dirección elegida por el script local. Tu compañera ejecuta ese script en su equipo y configura su propia conexión SQL.
- Producción utiliza `environment.prod.ts`. Web consulta `/assets/configuracion-publica.json`; Android consulta siempre `https://proyecto-tetris-512517.web.app/assets/configuracion-publica.json`, sin caché, cookies ni redirecciones. Se valida un único campo `urlBackend`, origen HTTPS de un subdominio Quick Tunnel permitido, y `/salud` antes de iniciar Angular. No contiene secretos ni existe fallback al túnel empaquetado.
- `npm run red:publica -- -UrlBackend DIRECCION_HTTPS_REAL` actualiza la fuente y `www` existente. Publicar ese JSON en Hosting requiere autorización separada. La APK de producción obtiene el nuevo túnel al iniciar o reintentar; no requiere recompilar por cada túnel. El encabezado CORS del JSON debe publicarse también para que Capacitor pueda leerlo. Consultar el [README principal](../../README.md).
- Hosting publica `www`, mantiene la reescritura de rutas a `index.html`, incluye manifest, iconos y audio. La comprobación predeploy rechaza una compilación local, configuración vacía o recursos ausentes. Se añadieron cabeceras de seguridad compatibles con la ventana de Google y se impide cachear la configuración.
- PWA: manifest, iconos PNG de 192/512, metadatos Apple y un service worker sin caché de datos. Requiere Internet; no ofrece partidas sin conexión. En Safari: Compartir > Añadir a pantalla de inicio. Las credenciales web pertenecen al origen del sitio; cambiar de dominio o abrir en otra instalación puede exigir recuperar la cuenta con Google/código privado. No se cambió su almacenamiento.
- Producción escucha en `127.0.0.1:5186`. El túnel debe ejecutarse en la misma computadora. Se procesan encabezados reenviados únicamente desde los proxies loopback conocidos, con un salto. Se rechaza HTTP público antes de autenticar. `/salud` queda disponible para diagnóstico sin datos privados.
- CORS en producción acepta únicamente los dos orígenes HTTPS derivados del ID Firebase indicado y `https://localhost`, que es el origen de Capacitor Android, no la dirección del backend. Esos sitios Firebase se han configurado como destinos previstos; no se verificó su publicación en la cuenta. CORS local queda limitado al entorno Development. Los handshakes del hub también validan Origin: CORS por sí solo no cubre WebSocket. Clientes nativos sin Origin conservan la autenticación JWT obligatoria.
- SignalR conserva autenticación, renovación de token, reconexión y autorización por jugador/sala; se excluye SSE porque Quick Tunnels no lo admite. Se mantienen WebSockets y long polling.
- Se conservan JWT con firma, emisor, audiencia, vencimiento, validación de sesión SQL y filtro de revocación en el hub. No se sustituyó la autenticación. El límite existente de 15 intentos/minuto por IP ahora también protege la consulta pública de nickname. No limita cada movimiento de juego.
- La cadena SQL integrada que estaba en la configuración común quedó en `appsettings.Development.json`; producción exige `ConnectionStrings__TetrisDB` y una clave JWT externa. No se detectó una contraseña SQL en esa cadena (utiliza identidad Windows). No se publican bases de datos ni se abren puertos SQL. No usar redirección de puertos del router.

## Inicio local

Desde PowerShell:

```powershell
Set-Location 'C:\Users\jafet\Documents\GitHub\Proyecto_Tetris\Backend\ProyectoTetris_PrograV'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --project ProyectoTetris_PrograV --launch-profile Tetris.API
```

En otra terminal:

```powershell
Set-Location 'C:\Users\jafet\Documents\GitHub\Proyecto_Tetris\frontend\tetris-app'
npm run red:local
npm start -- --host 0.0.0.0 --port 8100
```

Para compilar los recursos locales de Android: `npm run build:local`. Para recursos publicados: `npm run build` después de `red:publica`. El proyecto Capacitor y el ID `com.proyectotetris.juegocr` no cambian. No se ejecutó sync, Gradle ni generación de APK.

## Backend publicado: preparación de secretos e inicio

En una terminal PowerShell nueva, introducir la conexión de TU SQL (el script no muestra los valores). La identidad Windows del proceso debe tener permisos sobre TetrisDB. La clave JWT generada queda solo en esa terminal; para una presentación puede conservarse en un gestor de secretos o variables privadas del servicio. No guardarla en Git ni en el frontend. Un reinicio con otra clave obliga a renovar JWT, pero conserva las credenciales de dispositivo en SQL.

```powershell
Set-Location 'C:\Users\jafet\Documents\GitHub\Proyecto_Tetris\frontend\tetris-app'
$env:ConnectionStrings__TetrisDB = [System.Net.NetworkCredential]::new('', (Read-Host 'Cadena de conexión de tu SQL Server' -AsSecureString)).Password
$bytesClave = New-Object byte[] 64
$generadorClave = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$generadorClave.GetBytes($bytesClave)
$generadorClave.Dispose()
$env:Jwt__Clave = [Convert]::ToBase64String($bytesClave)
npm run backend:publicado
```

Este comando inicia únicamente el backend loopback, sin túnel. La configuración utiliza Production y omite launchSettings. No ejecutar este script contra Development para una exposición pública.

## Túnel y Hosting: comandos para ejecutar únicamente cuando autorices publicar

En otra terminal, con el backend anterior activo:

```powershell
cloudflared tunnel --url http://127.0.0.1:5186
```

Copiar el origen HTTPS REAL que muestre cloudflared. No se inició ese comando durante esta tarea. Si `cloudflared` no se reconoce, usar la ruta de su ejecutable instalado (no quedó disponible en el PATH de esta sesión).

```powershell
Set-Location 'C:\Users\jafet\Documents\GitHub\Proyecto_Tetris\frontend\tetris-app'
$direccionBackend = Read-Host 'Origen HTTPS real mostrado por Cloudflare'
npm run red:publica -- -UrlBackend $direccionBackend
npm run build
npm run verificar:publicacion
firebase login
firebase deploy --only hosting --project proyecto-tetris-512517
```

`firebase login` abre tu autenticación personal; debes completarla tú. No hace falta repetirlo si ya estás autenticado. No se ejecutó login ni deploy. Si falla `build` o `verificar:publicacion`, detenerse antes de publicar. Los scripts npm con PowerShell funcionan también desde PowerShell 7; no requieren cambiar la política global de ejecución.

Un Quick Tunnel cambia al reiniciarse, no garantiza disponibilidad y limita solicitudes simultáneas (la documentación indica 200 solicitudes en vuelo). No es una solución permanente. Para una dirección estable: un túnel con nombre en una cuenta Cloudflare y un hostname de un dominio que controles, apuntado exclusivamente al servicio HTTP loopback del backend. Se requiere configurar DNS y autenticar cloudflared. No se contrató ni activó ningún servicio. Si no hay dominio disponible, mantener el mismo proceso temporal durante la presentación y asumir sus limitaciones. La computadora, SQL y cloudflared deben permanecer activos; las salas viven en memoria y no sobreviven a un reinicio del backend.

## Google Cloud Console: pasos personales pendientes

No se accedió a tu cuenta ni se inspeccionaron clientes remotos. Solo se verificaron las referencias locales al ID web existente, el paquete Android y el flujo implementado.

1. En Google Cloud Console, seleccionar el proyecto que contiene el cliente `652877629973-kt2nkjl56mvgv6udli3m92mf64jcfcrs.apps.googleusercontent.com` (no asumir que coincide con el proyecto Firebase). Abrir **Google Auth Platform > Clients**, seleccionar ese cliente de tipo **Web application** y agregar en **Authorized JavaScript origins** los orígenes previstos:
   - `https://proyecto-tetris-512517.web.app`
   - `https://proyecto-tetris-512517.firebaseapp.com`
   Registrar únicamente los dominios desde los que realmente vayas a servir el frontend. Sin rutas, sin slash final y sin poner el túnel del backend. La dirección que importa para `origin_mismatch` es el origen de la página que ejecuta GIS. Conservar los orígenes localhost necesarios para desarrollo; una publicación final puede utilizar un cliente/proyecto separado para producción.
2. El flujo web existente usa el botón oficial Google Identity Services con callback de ID token; no requiere inventar un callback OAuth en la API ni agregar el túnel a Authorized redirect URIs. LAN con una IP privada HTTP sirve para jugar con nickname, pero no se considera un origen válido para Google web: usar localhost en PC o el sitio HTTPS en móviles.
3. **Google Auth Platform > Audience**: usar audiencia **External**, completar la configuración y pasar de **Testing** a **In production** mediante **Publish app**. Esa operación elimina la dependencia de agregar cada correo como usuario de prueba; queda sujeta a las políticas, límites, restricciones de cuentas y revisión que Google muestre. No se afirma que la publicación o verificación esté aprobada.
4. **Branding**: nombre Tetris, correo de asistencia que controles, contacto de desarrollador, página principal real y política de privacidad pública real. Completar/verificar los dominios que exija Google. No se inventó un dominio propio ni una verificación. Ver `PRIVACIDAD_BORRADOR.md` para el contenido que falta completar y publicar.
5. **Data Access**: limitar a identificación básica `openid`, `email`, `profile`; sin Drive, Gmail, contactos, acceso offline ni refresh tokens de Google. El navegador utiliza GIS y Android el plugin existente; el backend verifica firma, emisor, audiencia, vencimiento y nonce ligado a operación/jugador. Se conserva vinculación por `sub`, sin fusionar cuentas por correo ni crear usuarios duplicados. Primero crear/recuperar el nickname, luego vincular desde el perfil. Las pruebas en memoria verifican conservación de UsuarioId, credenciales y estadísticas; falta la prueba con una cuenta Google real.
6. En **Clients**, revisar el cliente de tipo Android: paquete `com.proyectotetris.juegocr` y SHA-1 del certificado que realmente firma tu APK. La huella documentada anteriormente en `GOOGLE_ANDROID.md` no se volvió a verificar aquí. Tu compañera necesita registrar su certificado si firma con otra clave. El ID Android no reemplaza el ID web usado como audiencia del token. No copiar secretos OAuth al frontend ni cambiar innecesariamente los clientes existentes.
7. En **Verification Center**, completar únicamente los pasos que Google solicite para tu marca, dominios y uso. No se promete acceso universal antes de completar lo que la consola indique. Si un dominio compartido de Hosting no permite la comprobación de propiedad exigida, resolverlo con Google o con un dominio que controles; no declarar verificación ficticia.

## Validación externa que aún se debe realizar

- Abrir el sitio publicado en Safari actualizado y Chrome Android desde Wi-Fi y datos móviles. El Angular instalado advierte que Safari/iOS 16.x queda fuera de su soporte; no se puede prometer compatibilidad con iPhone antiguos sin otra tarea de compatibilidad. No se cambiaron dependencias para ocultar esa limitación.
- Nickname > jugar Individual, Compartido y Competitivo; vincular Google en el perfil; comprobar mismo UsuarioId y estadísticas; recuperar en otro dispositivo. Probar Google desde Safari y desde pantalla de inicio por separado.
- Probar audio tras tocar un control, silencio, cambio de aplicación, pantalla segura y controles táctiles en retrato/pantallas pequeñas. El audio y controles existentes no se modificaron.
- Con varios dispositivos conectados a la misma sala, cortar brevemente la conexión y restaurarla dentro del plazo de 30 segundos que ya usa el juego. La reconexión mantiene el acceso de tokens. Si se agotan reintentos aparece el mensaje existente para volver a la partida. Una interrupción mayor puede causar derrota/cierre conforme a las reglas existentes, que no se cambiaron.
- Reiniciar el túnel temporal, ejecutar red:publica con el nuevo origen, volver a publicar y recargar. Probar HTTP, negociación SignalR y WebSocket a través de Cloudflare. La integración local no acredita esos resultados externos.

## Archivos de esta tarea

Backend: `Program.cs`, `Seguridad/ConfiguracionSeguridad.cs`, nuevo `Seguridad/ConexionPublicada.cs`, `Controllers/UsuariosController.cs`, `appsettings.json`, `appsettings.Development.json`, nuevo `appsettings.Production.json`; pruebas `Tetris.Pruebas/Program.cs` y nuevo `PruebasConexionPublicada.cs`.

Frontend: `angular.json`, `firebase.json`, `package.json`, `src/main.ts`, `src/index.html`, `src/environments/configuracion.ts`, `environment.prod.ts`, nuevos `configuracion-google.ts`, `configuracion-publicada.ts` y su spec; nuevos `src/assets/configuracion-publica.json`, iconos `tetris-192.png`/`tetris-512.png`, `src/manifest.webmanifest`, `src/sw.js`; servicios `api.servicio.ts` y `salas.servicio.ts`; nuevos scripts `ConfigurarPublicacion.ps1`, `IniciarBackendPublicado.ps1`, `VerificarPublicacion.cjs`; `GOOGLE_ANDROID.md`, este informe y el borrador de privacidad. Los archivos con cambios anteriores del usuario no enumerados aquí no se atribuyen a esta tarea.

## Referencias oficiales

- Google GIS y orígenes: https://developers.google.com/identity/gsi/web/guides/get-google-api-clientid
- Google, publicación y cumplimiento OAuth: https://developers.google.com/identity/protocols/oauth2/production-readiness/policy-compliance
- Cloudflare Quick Tunnels: https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/
- ASP.NET Core detrás de proxy: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0

## Pruebas ejecutadas el 26 de octubre de 2026

| Comprobación | Resultado |
| --- | --- |
| Compilación Angular producción (`npm run build`) | Correcta; sin generar APK |
| Compilación Angular desarrollo (`npm run build:local`) | Correcta |
| Backend/API y proyecto de pruebas (`dotnet build Tetris.Pruebas/Tetris.Pruebas.csproj --no-restore -m:1`) | Correcta, 0 advertencias y 0 errores |
| Suite de lógica y cuentas (`dotnet run --project Tetris.Pruebas --no-build`) | 177/177 correctas |
| Integración HTTP/SignalR (`--integracion-http`) | 254 comprobaciones correctas, varios WebSockets locales y datos en memoria |
| Seguridad de publicación (`--conexion-publicada`) | 31 comprobaciones correctas de HTTPS, proxy/IP, CORS, origen del hub, JWT obligatorio y rate limiting |
| Angular (`npm test -- --watch=false`) | 40/40 pruebas correctas en 13 archivos |
| Lint (`npm run lint`) | 0 errores; 6 advertencias anteriores sobre interfaces de ciclo de vida |
| Script de dirección pública | Rechaza HTTP privado; acepta y normaliza un origen HTTPS de ejemplo, actualiza fuente y www |
| Comprobación previa a Hosting | Rechaza configuración vacía; acepta artefactos de producción con URL de ejemplo, revisa rutas, iconos y archivos de audio |
| Inspección del bundle publicado | Sin la IP Android local ni el origen HTTP local del backend |
| Configuración de ejemplo usada para pruebas | Restaurada a vacía en fuente y www; no quedó una dirección ficticia configurada |
| Google real, dispositivos físicos, Firebase desplegado, Cloudflare público y SQL real | Pendientes; no se declaran verificados |

El primer intento de algunas pruebas encontró restricciones del sandbox (temporales de Vitest y sockets de loopback). Se completaron con los permisos aprobados. La compilación paralela inicial de la solución terminó sin diagnóstico; la compilación explícita del proyecto de pruebas y todas sus referencias pasó con `-m:1`.

Se corrigió únicamente una aserción de `src/app/servicios/audio.servicio.spec.ts`: la prueba sobre una sola pista musical estaba contando también efectos de resultado. Ahora cuenta las instancias de música. No se modificó `audio.servicio.ts` ni la reproducción del juego. Agregar ese archivo spec a la lista de archivos de esta tarea.

Para repetir las comprobaciones de C#, ejecutar desde `Backend/ProyectoTetris_PrograV`:

```powershell
dotnet build Tetris.Pruebas/Tetris.Pruebas.csproj --no-restore -m:1
dotnet run --project Tetris.Pruebas --no-build
dotnet run --project Tetris.Pruebas --no-build -- --integracion-http
dotnet run --project Tetris.Pruebas --no-build -- --conexion-publicada
```

Las tres suites de pruebas anteriores utilizan memoria y servidores de prueba; no requieren alterar TetrisDB. No ejecutar las variantes `--*-sql` sin revisar su alcance por separado.
