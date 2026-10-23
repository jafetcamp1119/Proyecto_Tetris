# Desarrollo local: navegador y Android

La dirección está centralizada en src/environments/configuracion-red.ts. Los jugadores no ven ni escriben direcciones. Se conservan el paquete com.proyectotetris.juegocr y el proyecto Android existente.

## Red Wi-Fi actual

Verificado el 23/10/2026: interfaz Wi-Fi, IPv4 192.168.1.20, puerta de enlace 192.168.1.1. Backend: http://192.168.1.20:5186. Navegador de la PC: http://localhost:5186. El backend escucha en http://0.0.0.0:5186; 0.0.0.0 es la dirección de escucha, no una dirección para los teléfonos.

Desde frontend/tetris-app, al cambiar de Wi-Fi o usar otro punto de acceso:

    npm.cmd run red:local

El script scripts/ConfigurarRedLocal.ps1 consulta Windows, exige una conexión activa con puerta de enlace e IPv4 privada y actualiza la URL Android y la excepción HTTP de Android debug. No instala herramientas, no abre el firewall y no genera APK.

Si Windows tiene varias conexiones activas, indicar la interfaz de desarrollo:

    npm.cmd run red:local -- -Interfaz "Wi-Fi"

La IP no se descubre automáticamente desde una aplicación ya instalada. Después de cambiarla, la desarrolladora debe volver a compilar el frontend, sincronizar Capacitor y generar e instalar su APK actualizado. Como alternativa operativa, puede reservar una IP en el router. El jugador nunca configura nada.

## Ejecutar el backend real

Desde Backend/ProyectoTetris_PrograV:

    dotnet run --project ProyectoTetris_PrograV --launch-profile Tetris.API

El perfil ya usa Development y http://0.0.0.0:5186. Si el ejecutable falta porque una compilación previa utilizó UseAppHost=false, ejecutar normalmente con compilación (sin --no-build) o iniciar el DLL desde su directorio con ASPNETCORE_URLS=http://0.0.0.0:5186 y ASPNETCORE_ENVIRONMENT=Development.

Mantener el backend encendido mientras se juega. PC y teléfonos deben compartir la misma red sin aislamiento de clientes. Permitir TCP 5186 únicamente en el perfil privado del firewall si Windows bloquea los teléfonos. No se abrió ninguna regla automáticamente.

En la revisión se comprobó la respuesta de /salud usando la IPv4 Wi-Fi real desde la PC. Esto no prueba la conectividad desde un teléfono ni las reglas de su red.

## Ejecutar Ionic en la PC

Desde frontend/tetris-app:

    npm.cmd start -- --port 8100

Abrir http://localhost:8100. Ya no existe Login > Configurar servidor.

## Preparar los archivos para que la desarrolladora empaquete Android

    npm.cmd run red:local
    npm.cmd run build
    node node_modules/@capacitor/cli/bin/capacitor sync android

Estos pasos no generan APK. Después, la desarrolladora realiza la compilación e instalación por su cuenta. No ejecutar cap add android: la plataforma ya existe.

La instalación previa tiene JDK 21 en C:\Program Files\Android\openjdk\jdk-21.0.8 y Android SDK en C:\Users\jafet\AppData\Local\Android\Sdk. Para usar el wrapper Gradle existente elegir ese JDK 21; el Java global 27 no corresponde al wrapper verificado anteriormente. En esta revisión no se ejecutó Gradle ni se generó un APK.

## HTTP de desarrollo y versión definitiva

Para la red local de confianza solicitada, Android debug permite HTTP exclusivamente hacia la IPv4 detectada. La política está en android/app/src/debug/res/xml/seguridad_red_desarrollo.xml. MainActivity permite contenido mixto únicamente si la aplicación es depurable; así el WebView local HTTPS puede llamar al backend HTTP y a SignalR. No se desactiva la validación de certificados.

El manifiesto y la política debug no se incluyen en release, y MainActivity no permite contenido mixto en una aplicación no depurable. Antes de distribuir una versión definitiva se necesita un backend HTTPS con certificado válido, una dirección estable, permitirHttpLocal=false y orígenes CORS explícitos. No se configuró un puerto HTTPS alternativo.

Referencia del comportamiento del WebView: documentación oficial de Capacitor, opción allowMixedContent:
https://capacitorjs.com/docs/config

## Pruebas manuales pendientes en Android

1. Conectar dos, tres y cuatro teléfonos a la red de confianza e ingresar como invitados distintos.
2. Probar salas públicas y privadas, límite de ingreso, Listo e inicio.
3. Compartido: mismo tablero, turnos, siete especiales seleccionables, efectos y actualización de la configuración de invitados.
4. Competitivo: tablero propio, sin especiales, espectadores, resultados y coronas.
5. Probar movimiento mantenido, rotación, caída, pausas, suspensión y retorno antes/después del plazo de reconexión.
6. Escuchar todos los efectos y ambas canciones en altavoces reales; confirmar volumen, silencio, ausencia de avisos de sala y de activaciones especiales.
7. Cambiar de red y repetir red:local, empaquetado e instalación. Revisar firewall y aislamiento del punto de acceso si /salud no es accesible desde otro dispositivo.
8. Verificar con cuentas Google reales la vinculación sin perder progreso y recuperación desde otro Android. GOOGLE_ANDROID.md contiene los datos OAuth.

No se afirma que Google Sign-In ni estas pruebas físicas estén terminados.

