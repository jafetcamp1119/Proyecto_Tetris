# Proyecto Tetris

Videojuego académico universitario con Ionic/Angular, Capacitor Android, ASP.NET Core .NET 10, SignalR y SQL Server Express. Firebase Hosting sirve el frontend y Cloudflare Tunnel conecta con la API de la computadora. El servidor conserva la autoridad sobre reglas, puntuaciones, turnos, pausas y resultados.

## Estructura y reglas

- `Backend/ProyectoTetris_PrograV`: solución con Dominio, AccesoDatos, LogicaNegocio, API, Utilitarios, cliente C#, pruebas y presentación.
- `frontend/tetris-app`: aplicación Ionic, Android existente, scripts y panel de presentación.
- Individual, Compartido y Competitivo conservan sus reglas. **Compartido y Competitivo mantienen mínimo dos jugadores**, por decisión del responsable. Se conservan capacidades de dos a cuatro, reconexión de treinta segundos y cierre por abandono voluntario del creador.
- Las salas y tableros activos viven en memoria del servidor. Transferir dispositivo conserva el estado; **reiniciar la API no restaura una partida activa desde SQL**. Los resultados persistidos son independientes.

## Preparación pendiente antes de usar la API con TetrisDB

Esta revisión no ejecutó SQL ni publicó servicios. El modelo nuevo necesita dos columnas nullable en `Usuarios`: `SesionActivaId` y `HashRecuperacionPendiente`, además del índice del hash pendiente. El script idempotente [06_sesion_unica.sql](Backend/ProyectoTetris_PrograV/docs/sql/06_sesion_unica.sql) no borra datos, comprueba la base y usa una transacción. Revisarlo y **autorizar su aplicación manual** sobre una copia respaldada de TetrisDB antes de ejecutar la versión nueva. No se aplica automáticamente. El iniciador detecta columnas ausentes mediante una consulta de solo lectura y explica el requisito antes de iniciar la API.

Conservar los scripts anteriores de `docs/sql`: este script complementa el esquema existente de cuentas, Google y recuperación. Las pruebas automatizadas descritas abajo usan memoria; no acreditan que el esquema real ya esté actualizado.

Producción requiere `ConnectionStrings__TetrisDB` y una `Jwt__Clave` privada de al menos 32 bytes, además de emisor y audiencia válidos. La presentación conserva su configuración privada con DPAPI del usuario Windows. No copiar cadenas privadas ni claves al frontend o al repositorio.

Los códigos cortos usan HMAC-SHA256. Se puede definir `Recuperacion__Clave` privada y estable, de al menos 32 bytes. Si falta, se usa la clave privada JWT. **Todas las instancias y entornos que compartan cuentas deben conservar la misma clave de recuperación**; cambiarla invalida los códigos cortos anteriores. Development genera una clave JWT efímera si no se configura: para conservar códigos entre reinicios, configurar una clave de recuperación estable mediante secretos de usuario o entorno. No publicar esa clave ni incluirla en JSON público. Los códigos legados de 64 caracteres siguen verificándose con su hash anterior.

## Sesión única y recuperación

Un ingreso explícito con credencial de dispositivo, Google o código crea un nuevo identificador activo. Los JWT anteriores dejan de autorizar HTTP y acciones SignalR. Se retiran los grupos privados/públicos de sus conexiones y se notifica el reemplazo antes de cerrarlas. La cuenta y sus estadísticas permanecen.

La renovación y reconexión normales conservan el identificador activo y no toman control de otro dispositivo. Una sesión desplazada no se recupera automáticamente; conserva el perfil recordado para un ingreso deliberado. Las credenciales antiguas que no estén revocadas o vencidas todavía permiten un ingreso explícito, que vuelve a desplazar la sesión actual. Cerrar sesión revoca la credencial activa; quitar un perfil local no borra la cuenta del servidor.

`VigilanciaSesionServicio` mantiene un único observador SignalR de cuenta, incluso fuera de Partida; no entra a salas. La conexión de juego continúa encargándose de la partida. Ante `SesionReemplazada`, un cierre central cancela solicitudes HTTP de ese token, limpia el estado privado y las conexiones, y navega con `NavController.navigateRoot('/login', { replaceUrl: true })`. No envía Abandonar ni logout y conserva credenciales recordadas. Login muestra una sola tarjeta «Sesión cerrada» con el mensaje específico y la recomendación de recuperar la cuenta. El guard de salida omite la confirmación de abandono cuando la sesión ya terminó; el guard de entrada sigue impidiendo volver a vistas protegidas con Atrás.

El backend distingue `SesionReemplazada`, `SesionInvalida` y `SesionVencida` sin interpretar mensajes. Registra conexiones bajo la misma exclusión del cambio de sesión para cubrir una negociación tardía. Un logout solo cierra conexiones del identificador que cerró sesión, sin barrer otro ingreso concurrente. Los eventos no contienen credenciales. La validación del servidor sigue bloqueando acciones si la notificación falla.

Como alternativa a la notificación, se comprueba `/api/usuarios/yo` al recuperar foco, visibilidad o Internet, al reactivar Capacitor y una vez por minuto mientras la página está visible. Las comprobaciones concurrentes se agrupan y se espacian al menos cinco segundos; los eventos cercanos dejan una única comprobación diferida. Los fallos de Internet, backend o SignalR mantienen la sesión y ofrecen reintentar. La comprobación usa renovación normal del JWT si corresponde; un vencimiento sin renovación válida muestra su propio aviso. Las respuestas y eventos de conexiones antiguas no se aplican a un ingreso posterior.

Se serializan cambios de sesión y acciones por usuario mediante un conjunto acotado de exclusiones; EF usa comprobaciones de concurrencia en la fila de usuario. El publicador verifica nuevamente la sesión antes de retirar conexiones para que un ingreso retrasado no cierre al ganador de una carrera. Las operaciones protegidas vuelven a verificar la sesión dentro de la exclusión.

Al ingresar se informa la sala activa y se ofrece volver a ella. El nuevo cliente recibe el mismo tablero, pieza activa, reserva privada, puntuación, turno, configuración, efectos y pausa autorizados por el servidor, sin volver a crear ni duplicar participantes. Se recupera también la última secuencia de movimientos. El reloj sigue corriendo según las reglas; cambiar de dispositivo u orientación no introduce una pausa adicional. La reconexión conserva su plazo existente.

### Código privado

- Formato de doce símbolos criptográficos, como `K7PM-42QX-8R2D`, sin I, O, 0 o 1. Se aceptan minúsculas, espacios exteriores y la forma sin guiones. El código de sala es distinto.
- Se entrega al crear la cuenta y al generar otro desde Perfil. Solo se muestra en ese momento, con botón para copiar. No puede consultarse luego porque el servidor guarda únicamente una huella protegida. No se guarda el código en Web Storage ni en registros.
- Regenerarlo deliberadamente desde Perfil reemplaza el anterior. Vence al año, conforme al comportamiento existente.
- Recuperar genera una credencial de dispositivo y muestra un código nuevo **pendiente**. El anterior sigue válido hasta presionar «Ya lo guardé» y confirmar en el servidor. El nuevo no sirve para recuperar hasta esa confirmación. La confirmación es idempotente: si se pierde su respuesta, puede repetirse. Si se pierde la respuesta de recuperación, se puede reintentar con el anterior. Otro ingreso desplaza la sesión que intentaba confirmar.
- Los códigos antiguos de 64 caracteres conservan compatibilidad y pasan al nuevo formato mediante el mismo procedimiento de confirmación.
- Límites: recuperación cinco intentos/minuto/IP; accesos sensibles quince/minuto/IP; regeneración tres/diez minutos/cuenta; creación de salas sesenta/minuto/cuenta. Acciones del hub tienen un límite de abuso de ciento veinte/segundo/cuenta, sin cola; no cambia la gravedad ni las reglas.

El JWT vive en memoria. Android conserva credenciales duraderas en almacenamiento seguro del plugin respaldado por Keystore, sin fallback sin cifrar. La web conserva perfiles/credenciales en localStorage: un acceso local al navegador o una vulnerabilidad XSS del origen podría obtenerlos. Usar equipos confiables, no compartir códigos y cerrar sesión cuando corresponda.

## Seguridad revisada

Las consultas productivas usan LINQ/EF parametrizado; la prueba SQL maliciosa inspecciona SQL generado sin abrir una base. Angular interpola nicknames como texto y la regresión XSS comprueba que HTML malicioso no crea elementos ejecutables. No se encontró necesidad de omitir la sanitización Angular. Los endpoints de juego obtienen la identidad del JWT y no permiten escribir puntuaciones o ganadores suministrados por el cliente.

Se mantienen firma HS256, algoritmo permitido, emisor, audiencia, vencimiento sin tolerancia y verificación de usuario/credencial/sesión. El hub revisa también sesión y vencimiento en cada acción. Las pruebas incluyen JWT falsificado, expirado, revocado, acciones fuera de turno, secuencias repetidas, permisos de creador y aislamiento de reservas privadas.

Producción exige HTTPS detrás de proxy loopback conocido; no se confía en encabezados de cualquier IP. CORS y Origin de WebSocket se limitan a los sitios Firebase configurados y `https://localhost` de Capacitor. Development conserva sus orígenes LAN. No se expone SQL ni se desactiva TLS. `/salud` es público y no devuelve datos privados. Los errores de concurrencia se traducen a un conflicto sin detalles internos.

Se actualizaron parches Angular/CLI/build a 22.2.3, Vitest a 4.1.12 y dependencias transitivas compatibles, con lockfile reproducible. La auditoría npm completa y la de paquetes NuGet no reportaron vulnerabilidades conocidas al verificarse. Esto no equivale a una garantía de ausencia de vulnerabilidades ni reemplaza futuras auditorías.

## Conexión de producción y Firebase

Web consulta `/assets/configuracion-publica.json`. Android de producción consulta siempre:

`https://proyecto-tetris-512517.web.app/assets/configuracion-publica.json`

El JSON solo admite `urlBackend`, con un origen HTTPS sin usuario, puerto, ruta, query ni fragmento, cuyo host pertenece a un Quick Tunnel `*.trycloudflare.com`. No se aceptan dominios arbitrarios, redirecciones ni campos adicionales. Se usa `no-store`, sin cookies, con plazo de diez segundos y después una comprobación `/salud` de ocho segundos. No existe fallback al túnel empaquetado si falla la consulta. HTTP y SignalR utilizan el mismo origen validado.

Si la computadora o el túnel están apagados, aparece una explicación y «Reintentar conexión». El reintento no se duplica y la opción Reconectar de una partida puede actualizar la configuración. Cambiar de túnel no restaura salas si también se reinició la API. La política `/privacidad` permanece pública sin depender de la API.

`firebase.json` prepara CORS `*` únicamente para ese JSON público, sin credenciales, y `no-store`. **Esas cabeceras deben publicarse después con autorización para que una instalación Android pueda consultar la configuración.** El emulador local verificó su aplicación, incluida compatibilidad con separadores de Windows. Una edición local no cambia Hosting. Las mismas reglas evitan caché del index, worker y política.

La confianza se apoya en HTTPS, un archivo fijo administrado en el proyecto Firebase y la lista de hosts. No es una firma independiente: quien controle Hosting podría sustituir la URL por otro túnel permitido. Proteger los permisos de Firebase; no añadir acceso de escritura de usuarios a esta configuración. Los túneles temporales dependen de un tercero y no garantizan disponibilidad.

El flujo de [Tetris.Presentacion](frontend/tetris-app/PRESENTACION_VISUAL_STUDIO.md) se conserva: salud local, túnel registrado, DNS, API HTTPS, validación, confirmación explícita `PUBLICAR` una sola vez cuando hace falta, comprobación del sitio publicado y entonces QR disponible. No hay deploy automático. Las pruebas de este trabajo simularon la publicación y no iniciaron túneles públicos. Panel, QR, logos y estilos se conservan.

## Google y Android

Se conserva el flujo web Google Identity Services y el nativo Credential Manager mediante `@capgo/capacitor-social-login`, sin reemplazarlo por Firebase Auth. El backend usa la biblioteca oficial para firma, emisor, audiencia y vencimiento, más un nonce propio de un solo uso. Vincula por `sub` a la identidad existente; no fusiona por correo ni nickname. Google también respeta la sesión única.

«Continuar con Google» ahora permite registrarse: si el `sub` verificado todavía no tiene cuenta, el servidor entrega un comprobante criptográfico opaco que dura cinco minutos y solo sirve para elegir nickname. El servidor guarda su hash y la identidad verificada en memoria; el cliente conserva el comprobante en memoria, sin guardar el ID token ni confiar en un correo/`sub` enviado por el formulario. Un nombre ocupado permite reintentar dentro del plazo; el éxito consume el comprobante. Usuario, vínculo Google, código de recuperación y credencial se insertan con un único SaveChanges. Se serializan altas/vinculaciones locales y los índices únicos existentes de GoogleSub/nickname protegen otras instancias. Si otra operación ya vinculó ese Google, se recupera esa identidad legítima. No se apropia un invitado por coincidir su nombre. La vinculación desde Perfil conserva su flujo autenticado. Reiniciar la API elimina comprobantes pendientes: basta volver a seleccionar Google.

### Progreso de Compartido

Las filas normales se almacenan en `SalaMemoria.Compartido.Lineas`, el único tablero de la sala, sin un contador paralelo que pueda divergir. El nivel es `1 + filas / 10`; el DTO `progresoCompartido` publica total, nivel, resto para el próximo nivel y el intervalo base. Se corrigió que `JugadorSala.Nivel` solo se actualizaba para el actor del turno: ahora todos reciben el nivel común, también en resultados/persistencia. Cada jugador conserva sus puntos y sus filas individuales; la interfaz muestra sus puntos y el progreso global de forma separada.

La velocidad conserva la fórmula existente: `max(100, intervaloBase - (nivel - 1) * 60)` ms. Los efectos temporales existentes siguen aplicando su factor cuando corresponde, sin cambiar duración ni probabilidades. Las líneas de la limpieza habitual contribuyen una vez; Barredora, bomba y otras limpiezas artificiales no incrementan Lineas por sí mismas. El multiplicador de puntos no multiplica filas y los puntos se calculan con el nivel anterior a esa limpieza. Turnos, reconexión y transferencia reutilizan el mismo tablero. Individual y Competitivo mantienen nivel/velocidad individuales.

El plugin limpia su estado local después de obtener el ID token para evitar persistencia propia sin cifrar. Esto no cierra la cuenta Google del sistema operativo ni del navegador. El Client ID web es público; no se utiliza un secreto OAuth en la app. Ver [configuración Google](frontend/tetris-app/GOOGLE_ANDROID.md).

Paquete conservado: `com.proyectotetris.juegocr`. Gradle verificó la SHA-1 debug existente:

`12:07:63:3D:AD:23:C0:56:6D:8F:E2:61:56:49:C5:71:A1:80:DC:3C`

Comprobar en Google Cloud que el cliente Android registra exactamente ese paquete/huella y que el cliente web coincide con Ionic y C#. No se editó Cloud Console. **Release no tiene certificado configurado en Gradle**: elegir la firma final en Android Studio y registrar su SHA-1 en el cliente Android correspondiente; si se usa Play App Signing, registrar también la huella de distribución. No reutilizar la huella debug como si fuera release ni subir el keystore al repositorio.

Se conserva portrait en Android, manifest PWA y aviso de orientación para Safari. El cambio no reinicia partidas, turnos ni timers. Release prohíbe HTTP y contenido mixto y deshabilita depuración WebView; debug mantiene solo la excepción LAN configurada. No se cambió el panel QR. Android minSdk 24 no garantiza un WebView antiguo compatible: requiere un WebView actualizado. La compilación Angular declara Chrome/Edge/Firefox 119+ y Safari/iOS 17+; probar versiones reales antes de distribuir.

## Compilación y pruebas reproducibles

Desde `Backend/ProyectoTetris_PrograV`, con SDK .NET 10:

```powershell
dotnet restore ProyectoTetris_PrograV.slnx
dotnet build ProyectoTetris_PrograV.slnx -c Release
dotnet run --project Tetris.Pruebas -c Release --no-build
dotnet run --project Tetris.Pruebas -c Release --no-build -- --integracion-http
dotnet run --project Tetris.Pruebas -c Release --no-build -- --conexion-publicada
dotnet run --project Tetris.Presentacion -c Release --no-launch-profile -- --pruebas
dotnet run --project Tetris.Presentacion -c Release --no-launch-profile -- --verificar
```

Desde `frontend/tetris-app`, con Node compatible con Angular/Capacitor instalado:

```powershell
npm ci
npm test -- --watch=false
npm run lint
npm run test:presentacion
npm run build
npm run verificar:publicacion
firebase emulators:exec --only hosting --project demo-tetris --non-interactive "node scripts/VerificarPrivacidad.cjs"
npx cap sync android
```

Verificaciones registradas en esta revisión:

| Verificación | Resultado y alcance |
| --- | --- |
| Solución Release | Sin errores ni advertencias |
| Backend | 198/198: incluye Solitario sin código, bloqueo de invitación corta/legada, partida/reconexión individual, creación/consulta/conexión con idéntico código para anfitrión e invitado en ambos modos multijugador, 1000 creaciones concurrentes sin duplicados, capacidad/cierre e identidad histórica independiente. Memoria/SQL generado, sin abrir SQL |
| HTTP y WebSocket | 368 en la última ejecución: servidor local en memoria; transferencia conserva código, tablero, turno y pausa en los tres modos, incluidas salas públicas y privadas; registro Google simulado, JWT, reconexión, permisos y límites |
| Producción | 31: HTTPS, proxy, CORS, Origin y cuotas con servidor local; sin Internet ni SQL |
| Frontend | 128/128: incluye Solitario sin sección de invitación aun con código legado, pantalla completa y copia para anfitrión/invitado en públicas/privadas multijugador, cambios de ronda y código inicial antes de SignalR; además de audio, Google simulado, predicción, sesiones, orientación y canvas. No acredita celulares físicos |
| Lint | Sin errores ni advertencias |
| Presentación | Confirmación/EOF, procesos descendientes, estados sin escrituras repetidas, logs compartidos con otro proceso escribiendo y bloqueo exclusivo, DNS/salud y QR |
| Hosting Emulator | Política pública en cuatro variantes, CSS, rutas Angular, JSON CORS/no-store, worker e index sin caché; sin deploy |
| Android | `cap sync`; clases Java y manifests debug/release compilados offline con JDK 21 y SDK 36; signingReport verificado. No APK |
| Dependencias | npm completo: cero vulnerabilidades reportadas; NuGet: ningún paquete vulnerable reportado |

### Controles, predicción y audio

La vista anterior esperaba el estado del servidor para mover la pieza: incorporaba la ida y vuelta completa de Internet. Ahora `PrediccionVisual` solo anticipa traslaciones/rotación de la pieza activa con el origen y la generación que informa el servidor. No modifica bloques fijos, puntuaciones, líneas, turnos, resultados ni efectos. En Compartido únicamente predice el dueño del turno; con Confusión activa se conserva la vista autoritativa para evitar diferencias al vencer el efecto. Se reconcilia con ultimaSecuencia, versión, ronda, turno y generación de pieza; los cambios de contexto y rechazos descartan predicciones. También se aceptan confirmaciones de secuencia sin cambio de versión cuando un movimiento toca una pared.

Caer y la fijación requieren confirmación: no se muestra un aterrizaje permanente ni se emite su sonido de forma especulativa. Mientras una fijación está pendiente se evita encadenar más caídas; se mantienen ocho comandos pendientes como máximo y la vista provisional vence en dos segundos sin confirmación. Una desconexión, pérdida de foco, orientación incompatible o expulsión elimina la vista provisional. No se reenvían comandos al reconectar. Teclado y táctil comparten repetición: primera acción inmediata, espera de 230 ms y repetición cada 100 ms; keyup/blur/cambio de control detienen la entrada. Rotación y Caer son una acción por pulsación. Se ignora la repetición del sistema operativo, sin enviar por fotograma.

Web Audio precarga y reutiliza buffers/una voz por efecto. Se midieron nuevamente los once MP3 originales mediante decodeAudioData en Edge, sin reproducirlos: colocar_pieza contiene **547,23 ms** de silencio inicial, derrota **466,06 ms**, victoria **365,04 ms** y eliminar_filas **14,17 ms**. `buscarComienzoAudio`, utilizada también por el diagnóstico, omite únicamente el silencio inicial del buffer con margen de 2 ms, sin reescribir MP3 ni tocar música. La preparación reduce esos silencios a **2 ms**. Se conservan eventos confirmados y deduplicación por secuencia; no se duplica el impacto al reconciliar. HTMLAudio queda como alternativa solo cuando no se pudo crear Web Audio; una descarga pendiente o un contexto suspendido no cambian de motor ni crean una segunda reproducción.

### Códigos públicos y privados; audio multiplataforma

Antes solo las privadas reservaban un código de seis caracteres; las públicas exponían doce caracteres del identificador y el formulario admitía seis. Ahora `CodigosSala` reserva mediante RNG criptográfico y `TryAdd` un código único entre las salas multijugador activas, tanto públicas como privadas. Solitario conserva su sala e identificador interno, sin reservar código de invitación. La privacidad solo determina el listado multijugador; no determina si existe código. Compartido y Competitivo permiten ingreso por código, normalizando espacios y mayúsculas. Se mantienen capacidad y estado autorizados por el servidor y mínimo dos/máximo cuatro en multijugador. La sección reutilizable CÓDIGO DE SALA permite consultar/copiar desde espera, partida y resultados multijugador; el listado público también lo muestra. Nunca es el código de recuperación de una cuenta.

El código permanece al iniciar, reconectar, transferir sesión y preparar otra ronda. Se libera al cancelar o retirar definitivamente la sala. El alias `CodigoPrivado` se conserva para clientes anteriores y sigue siendo nulo en públicas; el campo común es `Codigo`. El ingreso legado por prefijo de doce caracteres sigue disponible únicamente para salas públicas. El registro histórico conserva su identidad anterior derivada de Id y ronda, independiente del código corto reutilizable. No se requieren cambios SQL ni se ejecutaron consultas en la base real para esta corrección.

En audio se encontraron fallos concretos: se daba por desbloqueado el contexto antes de finalizar `resume`; una interrupción/carga pendiente intentaba HTMLAudio fuera del gesto, consumiendo el resultado aunque no sonara; una descarga fallida no se reintentaba. También faltaba derrota para un no ganador de Compartido por meta que no estaba marcado como perdido. Se corrigieron esas rutas, sin cambiar resultados ni reglas. `resume` se llama durante el gesto y se comprueba el estado running real. Se reutiliza el contexto y se recupera suspended/interrupted con una interacción posterior permitida. Solo se conserva un resultado en vivo pendiente, de la misma ronda y por diez segundos; se descarta con silencio, cambio de jugador/ronda o vencimiento. No se encolan movimientos ni se reproducen resultados al abrir un historial.

Todos los efectos usan GainNode: 0/25/50/75/100 % equivalen a ganancia 0/0,25/0,5/0,75/1. Movimiento conserva su atenuación relativa de 0,22. Cambiar el deslizador actualiza también voces activas; cero detiene efectos. «Probar sonido» reproduce aterrizaje al nivel elegido, espera la preparación, reemplaza la muestra anterior y no dispara además el clic global. No activa efectos deshabilitados; música y preferencias por jugador/dispositivo conservan su gestión independiente. [Apple documenta limitaciones del volumen de HTML media en iOS](https://developer.apple.com/library/archive/documentation/AudioVideo/Conceptual/Using_HTML5_Audio_Video/Device-SpecificConsiderations/Device-SpecificConsiderations.html); estos porcentajes controlan los efectos Web Audio de la aplicación, no el volumen físico ni las restricciones del sistema.

El diagnóstico opcional existente añade contadores audio_contexto_running/suspended/interrupted, audio_resume_fallido, audio_carga_fallida, audio_efecto_no_disponible y rechazos de reproducción, sin alertas, secretos ni registros continuos. Los once recursos decodifican en Edge; la espera/interrupción de Safari se probó por simulación, sin atribuir una causa única al iPhone observado. En iPhone físico/Safari/PWA falta verificar victoria/derrota únicas, regreso tras bloqueo/cambio de app/interrupción, nueva interacción cuando corresponda y muestra a 0/25/50/75/100 %, efectos deshabilitados, navegación y reapertura. Repetir en Android/Capacitor; respetar silencio y volumen del sistema.

Para incorporar esta corrección: reiniciar la API con el código nuevo (sin partidas activas), publicar el www preparado en Firebase cuando se autorice y recargar web/PWA. Android ya está sincronizado; la APK instalada necesita una nueva generación e instalación posterior para recibir el JavaScript actualizado. No se generó APK, no se publicó Firebase, no se cambiaron firma/secretos/SQL y no se modificó el panel de presentación.

### Código visible para el anfitrión: revisión del 28 de octubre

Se compararon por HTTP público el index, la plantilla de partida y el componente publicados con la compilación local previa: eran iguales y mostraban `sala.codigo` sin condición de rol ni privacidad. El backend usa el mismo proyector en creación, consulta, ingreso y SignalR; las pruebas verifican idéntico código para ambos participantes y tras reconexión/transferencia. No se encontró un contrato de creación distinto ni una condición que oculte el código al creador. La APK debug existente en el disco tenía otro main y no contenía el componente actual: no se asume que sea la APK instalada durante la prueba del usuario. Las instalaciones/versiones deben identificarse por separado.

Se confirmó que la respuesta de consulta inicial HTTP se descartaba hasta que terminaba la negociación de SignalR. Ahora se aplica inmediatamente, con la misma comprobación de intento vigente para evitar respuestas tardías. Así la sección se muestra y permite copiar antes de completar el hub; los controles siguen bloqueados hasta conectar. No se inventan códigos ni se conserva información de otra sala.

También se confirmó `Cache-Control: max-age=3600` en la raíz pública, aunque la regla anterior protegía `/index.html`. La regla nueva envía `no-store` en las URLs sin extensión que sirven Angular, incluida la raíz y los enlaces directos a Partida. El emulador comprobó `/`, `/login`, `/home`, `/salas/crear` y `/partida/sala-de-prueba`, además de privacidad/worker/configuración. No se cambió el worker: no intercepta fetch ni almacena respuestas. No se cambió audio, backend productivo, reglas, SQL, panel ni diseño.

Las pruebas Angular completas cubren anfitrión e invitado, código público y privado, los tres modos, copia y cambios de ronda. Desde frontend/tetris-app, `node scripts/VerificarCodigoSala.cjs` comprueba en Edge/Chrome aislado el template y CSS reales del componente con CSS de producción en marcos de 320×568, 390×844, 430×932 y 1280×800: 16 escenarios de rol/privacidad, código y botón visibles sin desbordamiento. Es una comprobación de layout y no ejecuta cuentas ni backend; las condiciones Angular se cubren en las pruebas de la página real. No equivale a Safari físico.

La caché antigua es una explicación posible de la diferencia observada; no se comprobó el contenido de la pestaña del iPhone, por lo que no se afirma una causa única del caso físico. El usuario todavía no había probado abrir una pestaña nueva. Después de publicar con autorización, cerrar/reabrir la pestaña o PWA y crear una sala pública y privada en iPhone; verificar el mismo código y copia con un invitado Android. No borrar credenciales ni cuentas. Una pestaña ya abierta conserva su JavaScript hasta recargar: las cabeceras nuevas no reemplazan código en ejecución.

Esta revisión necesita actualizar Hosting para la web y recargar el sitio. No necesita cambios SQL ni backend. Para incorporar el ajuste de carga inicial a Android se requiere sincronizar Capacitor y, cuando se autorice, generar e instalar una APK nueva; no es necesario generar una APK para aplicar la corrección de Hosting en iPhone. No se publicó ni se generó APK durante esta revisión.

### Regla definitiva: Solitario sin invitaciones

La corrección posterior limita la reserva de códigos a Compartido y Competitivo. Solitario (`Individual` en el contrato) conserva su Guid interno, su capacidad de uno y la recuperación/transferencia por identidad autenticada. El DTO conserva `Codigo` como string vacío y `CodigoPrivado` como null, evitando cambiar tipos compartidos. No hay entrada reservada en el índice de códigos; también se rechaza el prefijo legado de doce caracteres, incluso para el creador. El acceso a la partida propia sigue usando Id y sesión. La identidad histórica de persistencia sigue derivándose de Id/ronda, por lo que no necesita código de invitación ni cambios SQL.

La plantilla muestra el componente exclusivamente para Compartido/Competitivo y lo omite en Solitario durante espera, juego y resultados, aun si un backend anterior enviara un código. Las pruebas comprueban juego y reconexión individual sin código, igualdad/copia para anfitrión e invitado en ambos modos multijugador públicos/privados y transferencia HTTP/SignalR de los tres modos. Pasan 198 pruebas backend, 128 frontend y 368 comprobaciones HTTP/SignalR; lint, compilación de producción, verificador de publicación y sincronización Capacitor correctos. No se modificó audio ni reglas.

Para incorporar esta última corrección se debe reiniciar la API con el código nuevo, publicar Firebase cuando se autorice y recargar web/PWA. Capacitor quedó sincronizado; la APK instalada necesita una nueva generación e instalación posterior para ocultar también allí la sección de Solitario. No se generó APK, publicó Firebase, ejecutó SQL ni creó commit/push durante el trabajo.

| Comparación reproducible | Antes | Después | Alcance |
| --- | --- | --- | --- |
| Vista de un movimiento con RTT simulado de 1000 ms | Dependía de la confirmación de 1000 ms | La posición provisional ya está disponible al comprobar a los 16 ms | Reloj controlado, no pantalla física |
| Silencio en el buffer de colocación | 547,23 ms | 2 ms | Mismo MP3 decodificado en Edge y función de producción; no micrófono |
| WebSocket local, p50/p95 | 1,905 / 3,961 ms | 1,837 / 4,674 ms | 22 muestras por ejecución, memoria, sin SQL ni Internet; no demuestra mejora del RTT |
| Motor local, p50/p95 | 0,0073 / 0,079 ms | 0,0050 / 0,0811 ms | 26 muestras; variación pequeña, sin afirmar aceleración |
| Renderizado | Sin tiempo real comparable anterior | Se mantiene RAF y deduplicación; disponible en diagnóstico | No se inventó una mejora temporal |

### Diagnóstico en navegador y servidor

Para medir en localhost y luego en el sitio publicado, con el mismo equipo y partida, abrir la consola del navegador y ejecutar:

```javascript
localStorage.setItem('tetris.diagnostico.rendimiento', '1');
location.reload();
// Jugar con izquierda/derecha/rotación/bajada y consultar:
window.tetrisRendimiento.resumen();
// Desactivar y recargar al terminar:
localStorage.removeItem('tetris.diagnostico.rendimiento');
location.reload();
```

Incluye evento→procesamiento, entrada→siguiente dibujo, intervalos reales de entradas, RTT/confirmación, tiempo de dibujo, estados descartados, máximo pendiente, reconexiones, transporte seleccionado y silencio omitido por efecto. Entrada→dibujo mide el siguiente canvas actualizado tras una entrada aceptada, no la latencia de la pantalla física; movimientos bloqueados pueden no producir un dibujo. No contiene nombres, tokens ni contenido de partidas. Separar resultados locales de los de Internet y comprobar que el transporte reportado sea WebSockets; se mantiene long polling como alternativa y no se habilita SSE en Quick Tunnels.

Desde frontend/tetris-app, `npm run medir:audio` abre Edge/Chrome sin ventana con un perfil temporal y servidor loopback, decodifica todos los efectos y compara con la preparación real. No reproduce sonido ni usa cuentas; cierra sus procesos al terminar. Para un navegador instalado en otra ruta, definir TETRIS_NAVEGADOR_PRUEBA. Esta prueba requiere permisos locales para iniciar el navegador.

Para diagnosticar procesamiento real de la API sin exponer consultas ni secretos, definir temporalmente `Diagnostico__Rendimiento=true` y `Logging__LogLevel__TetrisRendimiento=Information` en el entorno de esa API y reiniciarla. Los registros numéricos separan validación/espera de ejecución/publicación. Desactivar ambas opciones y reiniciar después de medir. No habilitar registro general de URLs del hub: pueden transportar JWT. Esta revisión no midió SQL real ni Cloudflare, y no atribuye su latencia sin datos.

La telemetría opcional de `RendimientoServicio` permanece desactivada por defecto, acotada en memoria y sin registrar secretos. La reproducción se prueba con simulación y los MP3 se decodificaron en un navegador real aislado; el retraso acústico de parlantes y dispositivos debe comprobarse físicamente.

Los cambios de Google/nivel/rendimiento no requieren columnas ni migraciones nuevas: usan GoogleSub, AvatarGoogle e índices únicos existentes, y el progreso activo permanece en memoria. No se cambian secretos, claves de recuperación, Client ID ni la URL permanente. Para probarlos públicamente se necesita reiniciar la API con este código y publicar el nuevo www con autorización. Capacitor ya quedó sincronizado con la compilación de producción; una APK anterior conserva su JavaScript hasta generar e instalar una versión nueva. Antes de distribuir quedan las comprobaciones de Google real, firma/OAuth release y celulares ya detalladas abajo.

## Apertura y posterior generación en Android Studio

Para incorporar la corrección del cierre automático, recompilar y reiniciar la API, publicar el nuevo `www` con autorización y recargar los navegadores/PWA. Una APK existente necesita recompilar/sincronizar y luego generar e instalar la nueva versión por decisión del responsable: el JSON remoto actualiza la dirección del backend, no el código JavaScript ya instalado. Esta corrección no añade columnas SQL ni cambia reglas.

Después de preparar el esquema autorizado, secretos estables, publicar la configuración/cabeceras con autorización separada y comprobar OAuth:

```powershell
# Desde frontend/tetris-app; no genera APK
npm ci
npm run build
npm run verificar:publicacion
npx cap sync android
npx cap open android
```

Usar el JDK 21 de Android Studio/Gradle y SDK 36. Para verificar clases, manifests y certificados sin generar APK, desde `android`:

```powershell
.\gradlew.bat :app:compileDebugJavaWithJavac :app:processDebugManifest :app:compileReleaseJavaWithJavac :app:processReleaseManifest :app:signingReport
```

Luego, cuando el responsable decida generar la app, usar Build > Generate Signed App Bundle or APK, seleccionar APK y el certificado propio; esa operación no se realizó en esta revisión. No se creó certificado release. Las dependencias nativas compilan; Gradle conserva advertencias de terceros (flatDir, APIs obsoletas y marcador de proveedor Facebook que el plugin elimina), sin fallo de compilación.

Antes de distribuir, probar dos dispositivos reales con Google y código: transferir Individual/Compartido/Competitivo mientras hay partida, turno, pausa y efectos; comprobar cierre del control anterior, audio con gesto, controles táctiles, portrait, teléfonos pequeños/grandes, Safari/PWA, cierre/reapertura, reconexión dentro/fuera de treinta segundos y backend apagado. Confirmar un nuevo túnel desde una instalación ya existente. No se verificó una cuenta Google real ni un celular físico durante este trabajo. El esquema SQL, la publicación autorizada, OAuth release y esas pruebas son pendientes para distribución, no funciones que se declaren comprobadas.

## Limpieza y documentación

Se retiraron los informes históricos `INFORME_INTEGRACION`, `INFORME_AUDIO_INTERFAZ`, `INFORME_CLOUDFLARE_ORIENTACION`, `INFORME_CONFIRMACION_FIREBASE`, `INFORME_RENDIMIENTO`, `INFORME_RUTA_PRIVACIDAD`, `INFORME_SALUD_PRESENTACION`, `INFORME_SIGUIENTE_PIEZA` y `INFORME_USUARIOS_CORONAS_RONDAS`, comprobando referencias. Se quitaron los diagnósticos manuales `MedirAudioNavegador.cjs` y `DiagnosticarTunel.ps1`, sus métodos de diagnóstico sin consumidores y el perfil manual temporal de confirmación. La prueba automatizada de consola permanece en `scripts/presentacion/pruebas/ConfirmacionConsola.ps1` y se ejecuta mediante el arnés interno.

Se conservan pruebas permanentes, assets, utilidades de iconos/QR, scripts SQL, [guía de presentación](frontend/tetris-app/PRESENTACION_VISUAL_STUDIO.md), [Google](frontend/tetris-app/GOOGLE_ANDROID.md), [desarrollo LAN](frontend/tetris-app/DESARROLLO_ANDROID.md), [publicación móvil](frontend/tetris-app/PUBLICACION_MOVIL.md), política y documentos de reglas/contratos. No se sustituyó la arquitectura. `.gitignore` excluye compilaciones, logs, evidencia local, APK/AAB, configuración privada y claves; no elimina archivos ya versionados ni sustituye revisar lo que se vaya a publicar.

La política pública todavía señala las decisiones del responsable sobre conservación de cuentas/registros y edad mínima; no se inventaron plazos generales ni un sistema de borrado de cuentas inexistente. No se realizaron deploy, commits, push, cambios de seguridad Windows ni operaciones sobre TetrisDB.
