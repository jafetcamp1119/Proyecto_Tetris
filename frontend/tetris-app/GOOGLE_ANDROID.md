# Integración de Google con Tetris

## Configuración pública ya aplicada

El Client ID web proporcionado está en `src/environments/configuracion.ts`, propiedad `configuracion.google.idClienteWeb`:

`652877629973-kt2nkjl56mvgv6udli3m92mf64jcfcrs.apps.googleusercontent.com`

Desarrollo y producción importan ese mismo archivo. `google.habilitado` controla si se ofrece la integración. Es un ID público, no un secreto.

En C#, `Backend/ProyectoTetris_PrograV/ProyectoTetris_PrograV/appsettings.json` contiene el mismo valor en `Google:IdClienteWeb`. Se puede reemplazar con la variable `Google__IdClienteWeb` o con secretos de usuario. No hace falta configurar un secreto OAuth para este flujo de ID tokens.

No se regeneró Android. Se conserva `com.proyectotetris.juegocr` y el nombre Tetris. SHA-1 debug verificada por Gradle:

`12:07:63:3D:AD:23:C0:56:6D:8F:E2:61:56:49:C5:71:A1:80:DC:3C`

El ID del cliente OAuth Android se registra en Google Cloud junto al paquete y a esa huella; no se copia al campo idClienteWeb.

## Flujo implementado

1. El jugador puede registrarse como invitado con un nickname único y jugar sin depender de Google.
2. En Menú > Mi perfil puede vincular Google. Android usa Credential Manager a través de @capgo/capacitor-social-login 8.7.5. El navegador usa el botón oficial Google Identity Services.
3. El backend genera un nonce criptográfico, válido durante diez minutos, ligado a la operación y, al vincular, al usuario autenticado.
4. C# verifica el ID token con Google.Apis.Auth: firma, emisor, audiencia, vencimiento y coincidencia del nonce de un solo uso.
5. La identidad se asocia por sub al mismo UsuarioId. No se cambian el nickname ni las relaciones de partidas y estadísticas. El correo no se utiliza para fusionar cuentas.
6. Si Google ya pertenece a otro jugador, se muestra un conflicto. No se fusiona ni se reemplaza ningún perfil. Se debe elegir Cambiar de jugador e ingresar con Google para abrir el otro perfil.
7. En otro teléfono, Continuar con Google recupera el jugador vinculado y crea una credencial de dispositivo nueva. Google sin vinculación previa muestra una indicación para crear o recuperar primero el invitado; no inventa un nickname ni crea duplicados automáticamente.
8. Google no sustituye los JWT de Tetris. Las llamadas al juego siguen usando los tokens propios y la validación de sesión existente.
9. Android guarda las credenciales duraderas de Tetris mediante AES/GCM y Android Keystore, con @aparajita/capacitor-secure-storage 8.0.1. El JWT vive en memoria. Después de obtener el ID token se limpia el estado local del plugin de Google antes de completar la operación con C#. No se guardan tokens Google desde el código Angular.

## Estado de las pruebas

- En la integración anterior compilaron Angular, C# y Android debug con los plugins nativos. La revisión posterior de audio y red compila Angular y C#, pero no compila Android ni genera APK. Los cambios nativos de red de esta revisión quedan pendientes de la compilación y prueba manual de la desarrolladora.
- Probados conflictos de vinculación, conservación de UsuarioId y récord, recuperación y consumo único de códigos privados.
- Pruebas SQL ejecutadas en TetrisValidacion_Google_20261023, separada de TetrisDB. Sus identidades externas están simuladas y se identifican como pruebas: no acreditan un acceso real a Google.
- Probado que un ID token inventado se rechaza mediante la biblioteca oficial y también por HTTP, y que no se puede vincular sin JWT de Tetris.
- Verificado visualmente el botón oficial web con el Client ID configurado.
- PENDIENTE: elegir una cuenta Google real, vincular, recuperar desde otro Android y probar cancelación, cierre de sesión y cambio de cuenta. No se declara Google Sign-In terminado sin estas verificaciones.

## Comprobaciones externas

En el cliente web Tetris Angular, registrar los orígenes exactos de desarrollo que se utilicen, por ejemplo `http://localhost:8100`, `http://localhost:8101` y `http://127.0.0.1:8100`. El botón web GIS no necesita un endpoint OAuth de redirección del backend para este flujo.

En el cliente Android Tetris Android - Desarrollo, comprobar paquete y SHA-1 anteriores. Si otra persona compila con otro certificado debug, necesita su propio cliente Android con esa huella.

Si la pantalla de consentimiento requiere usuarios de prueba, agregar las cuentas que realizarán la verificación. Probar con conexión a Internet y un Android con Google Play Services actualizado. Para producción registrar el certificado que firme la app distribuida, incluida la firma de Play si se usa Play App Signing.

No se necesita google-services.json ni Firebase para el flujo implementado. No copiar el secreto del cliente web a Ionic, Android, documentación ni GitHub.

## Google Play Games

No está integrado. Es un servicio diferente: necesita configuración de juego en Google Play Console y credenciales de Play Games Services v2. Los dos clientes OAuth de Google Sign-In no bastan para habilitar perfiles gamer. El progreso e identidad de Tetris siguen siendo propios del backend.

Referencias: https://capgo.app/docs/plugins/social-login/google/android/ ; https://developers.google.com/identity/gsi/web/guides/verify-google-id-token ; https://developers.google.com/android/guides/client-auth .
