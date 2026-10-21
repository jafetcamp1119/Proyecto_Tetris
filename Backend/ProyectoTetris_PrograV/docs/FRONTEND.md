# Conectar Ionic + Angular con el servidor Tetris

El frontend definitivo será Ionic, Angular, TypeScript, HTML y SCSS. Capacitor empaquetará
la aplicación Android y Android Studio permitirá generar el APK. Tu computadora ejecutará
la API, SignalR, el motor, las salas y SQL Server. No necesitas servicios en la nube.

Esta etapa no crea pantallas ni un proyecto Ionic. Tetris.Cliente permanece como biblioteca
C# de referencia; no se importa en Angular.

## 1. Lo que tomamos de tu quiz

Se revisó pokedex-jafet-app.zip sin modificarlo. Usa Angular standalone, servicios
@Injectable, interfaces en src/app/models, Capacitor 8, CapacitorHttp y webDir: 'www'.
Puedes mantener esa organización para Tetris.

El quiz no usa HttpClient de Angular en su servicio Pokemon: usa CapacitorHttp.
Los ejemplos de esta guía siguen esa forma. Si eliges HttpClient, registra provideHttpClient()
en los providers de bootstrapApplication y agrega Authorization en sus solicitudes.
[Configuración oficial de HttpClient](https://angular.dev/guide/http/setup).

## 2. Encender el backend

En Visual Studio abre ProyectoTetris_PrograV.slnx, elige el proyecto API como inicio y el
perfil Tetris.API. Revisa la conexión SQL de appsettings.json y ejecuta con F5.

También puedes usar:

~~~powershell
dotnet run --project ProyectoTetris_PrograV/ProyectoTetris_PrograV.csproj --launch-profile Tetris.API
~~~

El perfil activa Development y escucha en http://0.0.0.0:5186.
0.0.0.0 significa escuchar en las interfaces del equipo; no es la dirección que escribirás en Ionic.
El perfil alternativo Tetris.SoloLocal escucha únicamente en localhost.

Desde el navegador del equipo abre http://localhost:5186/salud.
Debe responder con estado 200 y un mensaje de servidor activo.
Este endpoint comprueba el proceso de la API; no comprueba la disponibilidad de SQL.

Mantén el proceso abierto mientras juegan los teléfonos.

## 3. Dirección para cada dispositivo

En PowerShell ejecuta ipconfig. Busca Dirección IPv4 del adaptador Wi-Fi conectado.
No uses una dirección de VPN, adaptador desconectado o tarjeta virtual.

| Desde dónde llamas | Dirección base del backend |
|---|---|
| Ionic en navegador de la misma computadora | http://localhost:5186 |
| APK en un celular físico | http://<IPv4_PC>:5186 |
| Navegador del celular | http://<IPv4_PC>:5186 |
| Emulador Android estándar, servidor en el host | http://10.0.2.2:5186 |

<IPv4_PC> es un marcador: reemplázalo por la dirección que te mostró ipconfig.
No representa una IP real de tu equipo. Si la red cambia la dirección, actualiza solo
la URL base del cliente; el backend escucha sin depender de una IP fija.

localhost dentro de un celular representa ese celular, no tu computadora.

Antes de probar Ionic, abre http://<IPv4_PC>:5186/salud en el navegador del celular.
Equipo y teléfono deben estar en la misma Wi-Fi y el router debe permitir comunicación
entre clientes. Una red de invitados puede aislar los dispositivos.

## 4. Firewall de Windows

No se modificó tu firewall automáticamente. Configura la red Wi-Fi como Privada solo
si es tu red de confianza. En PowerShell como administrador puedes crear esta regla:

~~~powershell
New-NetFirewallRule -DisplayName "Tetris API WiFi privada" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5186 -Profile Private -RemoteAddress LocalSubnet
~~~

Solo abre TCP 5186 desde la subred local y en el perfil privado. No abras SQL Server,
puertos adicionales ni el perfil Público para los teléfonos: ellos hablan con la API.

Para retirar la regla:

~~~powershell
Remove-NetFirewallRule -DisplayName "Tetris API WiFi privada"
~~~

## 5. CORS

appsettings.Development.json permite explícitamente:

- http://localhost:8100 y http://127.0.0.1:8100 para Ionic.
- http://localhost:4200 y http://127.0.0.1:4200 para Angular.
- http://localhost, https://localhost y capacitor://localhost para los contenedores configurados.

El origen es la dirección de la página que hace la petición, no la dirección del backend.
Una APK normalmente conserva un origen localhost aunque se conecte a la IPv4 de tu equipo.

Si ejecutas ionic serve --external y abres la página desde otro dispositivo, su origen es
la IPv4 de tu PC con el puerto de Ionic. En Development, appsettings.Development.json activa
Cors:PermitirRedLocal, que acepta además orígenes http con IPv4 privada (10.x.x.x, 172.16-31.x.x y
192.168.x.x). Una dirección pública sigue rechazada. En Production esta opción está en false.
No uses comodines ni AllowAnyOrigin con credenciales.

La política permite Authorization, Content-Type y los métodos HTTP necesarios.
Las credenciales se permiten solo para la lista explícita, conservando clientes de cookies.
Ionic puede usar Bearer sin withCredentials. CORS no sustituye la autenticación.

Production no hereda la lista de desarrollo: debes configurar sus orígenes explícitamente.

## 6. Configuración de URL en tu futuro Ionic

En el archivo de entorno que utilices:

~~~typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5186'
};
~~~

Para compilar una APK de prueba, reemplaza localhost por <IPv4_PC>.
Utiliza una sola apiUrl en los servicios; no repitas la IP en cada método.
No copies Jwt:Clave al frontend: el cliente solo recibe un token firmado.

Puedes copiar las interfaces de docs/contratos/tetris.model.ts a src/app/models.
Incluyen respuesta, usuario, login, registro, autenticación, configuración, sala,
movimiento, tablero, puntuación, resultados y ranking.
Las propiedades JSON son camelCase. Los enums son cadenas y las fechas UTC son cadenas ISO.
Los GUID de salas son strings. Los campos opcionales de estado usan null, no undefined.
Mantén secuencia como contador entero creciente por jugador y sala.

## 7. Login y JWT

POST /api/usuarios/registrar recibe nombreUsuario, correo y contrasena.
POST /api/usuarios/login recibe nombreUsuario y contrasena.

Login devuelve IRespuesta<IAutenticacion>. data conserva los campos de IUsuario y agrega:

- token: JWT que se envía en las solicitudes.
- tipoToken: Bearer.
- expiraEnUtc: vencimiento en UTC.

El servidor valida usuario y contraseña con la lógica existente y no devuelve PasswordHash.
En los endpoints protegidos envía Authorization: Bearer <token>.
No envíes usuarioId para demostrar tu identidad: la API lo obtiene del token validado.

Un ejemplo breve de servicio, para escribir después en tu proyecto:

~~~typescript
import { Injectable } from '@angular/core';
import { CapacitorHttp } from '@capacitor/core';
import { environment } from '../../environments/environment';
import { IAutenticacion, ILoginUsuario, IRespuesta } from '../models/tetris.model';

@Injectable({ providedIn: 'root' })
export class AccesoService {
  private token: string | null = null;

  obtenerToken(): string {
    return this.token ?? '';
  }

  async login(datos: ILoginUsuario): Promise<IAutenticacion> {
    const respuesta = await CapacitorHttp.post({
      url: environment.apiUrl + '/api/usuarios/login',
      headers: { 'Content-Type': 'application/json' },
      data: datos
    });

    const resultado = respuesta.data as IRespuesta<IAutenticacion>;

    if (respuesta.status !== 200 || !resultado.success || !resultado.data) {
      throw new Error(resultado.error ?? 'No se pudo iniciar sesión.');
    }

    this.token = resultado.data.token;
    return resultado.data;
  }
}
~~~

Para una llamada protegida con CapacitorHttp agrega:

~~~typescript
headers: {
  Authorization: 'Bearer ' + acceso.obtenerToken(),
  'Content-Type': 'application/json'
}
~~~

El quiz ya utiliza CapacitorHttp. Sus llamadas nativas y las del navegador tienen
comportamientos distintos; prueba también ionic serve para verificar CORS.
[Referencia de CapacitorHttp](https://capacitorjs.com/docs/apis/http).

Un 400 indica datos o acción inválidos; 401 indica sesión ausente, inválida o vencida;
403 significa falta de permiso y 500 un fallo inesperado.
Las respuestas de validación conservan success=false, error y los errores por campo en data.
Las operaciones de negocio también utilizan Respuesta<T>. Logout responde 204 sin cuerpo.
Los servicios CRUD existentes siguen devolviendo 400 para fallas que representan con Respuesta<T>.

Para comenzar conserva el token en memoria. Al cerrar sesión detén SignalR, llama logout
y elimina el token del cliente. Logout borra la cookie heredada; no revoca un JWT ya emitido.
Este vence normalmente a los 60 minutos. No hay refresh tokens: vuelve a iniciar sesión al vencer.

### Clave del servidor

En Development, si falta Jwt:Clave se crea una clave temporal segura en memoria.
Reiniciar el servidor invalida sus tokens anteriores. Para conservar sesiones entre reinicios,
configura una clave persistente mediante secretos de usuario:

~~~powershell
$claveTetris = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
dotnet user-secrets set "Jwt:Clave" $claveTetris --project ProyectoTetris_PrograV/ProyectoTetris_PrograV.csproj
~~~

La clave no se imprime ni se guarda en el repositorio. También puedes usar la variable de
entorno Jwt__Clave. Production exige una clave de al menos 32 bytes; no usa la temporal.
Emisor, audiencia y duración están en la sección Jwt. Este ajuste no cambia SQL Server.

## 8. SignalR desde Angular

Instala @microsoft/signalr en tu futuro proyecto Ionic. Usa el mismo apiUrl y un
accessTokenFactory que lea el token actual. El servidor acepta access_token en la URL
solo bajo /hubs/juego, como requieren algunos transportes del navegador.
[Autenticación oficial de SignalR](https://learn.microsoft.com/aspnet/core/signalr/authn-and-authz).

Ejemplo de conexión sin pantalla:

~~~typescript
import * as signalR from '@microsoft/signalr';

let estadoActual: ISala | null = null;

const conexion = new signalR.HubConnectionBuilder()
  .withUrl(environment.apiUrl + '/hubs/juego', {
    accessTokenFactory: () => acceso.obtenerToken(),
    withCredentials: false
  })
  .withAutomaticReconnect()
  .build();

function aplicarEstado(estado: ISala): void {
  if (estadoActual === null || estado.version >= estadoActual.version) {
    estadoActual = estado;
  }
}

conexion.on('EstadoActualizado', aplicarEstado);

conexion.onreconnecting(() => {
  // Indica pérdida temporal de conexión y suspende el envío de controles.
});

conexion.onreconnected(async () => {
  const respuesta = await conexion.invoke<IRespuesta<ISala>>('Entrar', salaId);

  if (respuesta.success && respuesta.data) {
    aplicarEstado(respuesta.data);
  }
});

conexion.onclose(() => {
  // Informa el cierre; puede ser necesario iniciar sesión nuevamente.
});

await conexion.start();
const entrada = await conexion.invoke<IRespuesta<ISala>>('Entrar', salaId);

if (entrada.success && entrada.data) {
  aplicarEstado(entrada.data);
}
~~~

En este ejemplo salaId es el GUID recibido al crear o unirse a la sala y acceso es tu
servicio de autenticación. Importa ISala e IRespuesta desde tus modelos.

Antes de conectar, incorpórate mediante HTTP: crear una sala o unirse por código.
Entrar agrega tu conexión al grupo y devuelve una instantánea.
Cuando todos estén listos, el creador inicia la partida por HTTP.
Envía movimientos invocando Mover con salaId y un IMovimiento, o por el endpoint HTTP.
Ambos caminos validan el mismo estado del servidor.

No reinicies secuencia al reconectar: continúa desde ultimaSecuencia + 1 de tu jugador.
Actualiza controles según usuarioTurno y perdido, pero la autoridad final sigue en el servidor.
En competitivo, muestra todas las puntuaciones y espera Finalizada para anunciar ganadores.
Con un único jugador activo la partida continúa.

La reconexión vuelve a ejecutar Entrar porque la nueva conexión debe incorporarse al grupo.
El inicio inicial puede fallar: captura su error y ofrece reintentar. La reconexión automática
no sustituye ese manejo inicial. Si vence el JWT, la conexión se cierra; vuelve a autenticar.
No registres en consola tokens ni URLs de WebSocket que los contengan.

El plazo de reconexión es de 30 segundos reales, incluso durante una pausa. El tablero del desconectado
(o el turno compartido) espera como máximo ese plazo. Consulta docs/ACTUALIZACION_DEFINITIVA.md
para abandonos, estados, eventos nuevos, meta personal y corona.

## 9. Endpoints disponibles

| Verbo | Ruta | Cuerpo |
|---|---|---|
| GET | /salud | Ninguno; sin autenticación |
| POST | /api/usuarios/registrar | IRegistroUsuario; sin autenticación |
| POST | /api/usuarios/login | ILoginUsuario; sin autenticación |
| POST | /api/usuarios/logout | Ninguno |
| POST | /api/salas/{id}/abandonar | Ninguno; creador cancela, participante sale |
| GET | /api/usuarios/yo | Ninguno |
| GET | /api/configuracion | Ninguno |
| PUT | /api/configuracion | IConfiguracion |
| POST | /api/salas | ICrearSala (incluye privada, piezasEspeciales, piezasNormales, velocidad y puntos) |
| GET | /api/salas | Ninguno; lista solo salas públicas en espera |
| GET | /api/salas/{id} | Ninguno |
| PUT | /api/salas/{id}/piezas-especiales | IConfiguracionPiezas; solo el creador, en espera |
| PUT | /api/salas/{id}/configuracion | IConfiguracionSala (especiales, piezas normales, velocidad, puntos); solo el creador, en espera. Ver docs/CONFIGURACION_PARTIDA.md |
| POST | /api/salas/{id}/unirse | Ninguno |
| POST | /api/salas/codigo/{codigo}/unirse | Ninguno |
| PUT | /api/salas/{id}/listo | true o false, sin objeto envolvente |
| POST | /api/salas/{id}/iniciar | Ninguno |
| POST | /api/salas/{id}/movimientos | IMovimiento |
| POST | /api/salas/{id}/pausa | Ninguno; individual pausa, multijugador abre votación |
| POST | /api/salas/{id}/pausa/voto | IVotoPausa |
| POST | /api/salas/{id}/reanudar | Ninguno; solo partida individual |
| GET | /api/resultados/mios | Ninguno |
| GET | /api/resultados/ranking | Ninguno |

Las rutas protegidas necesitan Bearer o una cookie anterior válida.
El hub mantiene Entrar, Mover, EstadoActualizado y el manejo de desconexión, y agrega
SolicitarPausa, VotarPausa y Reanudar junto con los eventos PausaSolicitada, VotoPausaRegistrado,
PausaRechazada, PausaIniciada y PausaFinalizada (cada uno entrega un IPausa).
Registra esos eventos con conexion.on antes de llamar a start(). Detalles en docs/REGLAS_NUEVAS.md.

## 10. Capacitor y HTTP local en Android

Mantén webDir: 'www'. Para una compilación de prueba que llama al backend por HTTP,
revisa esta configuración de desarrollo en capacitor.config.ts:

~~~typescript
server: {
  cleartext: true
},
android: {
  allowMixedContent: true
},
plugins: {
  CapacitorHttp: {
    enabled: true
  }
}
~~~

Conserva los demás valores propios de tu aplicación. El origen Android puede seguir siendo
https://localhost; no necesitas cambiar androidScheme.
cleartext permite HTTP y allowMixedContent permite que la WebView HTTPS use ese servidor HTTP.
Son opciones de desarrollo: usa HTTPS y retira estas excepciones en una distribución final.
No uses server.url para apuntar al backend: no es el frontend que se carga en la APK.
Tampoco necesitas allowNavigation: ['*'].
[Configuración oficial de Capacitor](https://capacitorjs.com/docs/config).

Comprueba el permiso android.permission.INTERNET en AndroidManifest.xml.
Después de sincronizar, verifica en la configuración Android que HTTP esté habilitado para
la compilación de prueba. Si tu manifiesto o networkSecurityConfig lo bloquean, revisa su
configuración de depuración. CapacitorHttp nativo no convierte WebSocket en HTTP nativo:
prueba SignalR por separado desde la WebView.

## 11. Pasos futuros para APK

Cuando tú hayas creado el proyecto y sus pantallas:

1. Verifica que npm install y ionic build terminen correctamente.
2. Define appId y appName propios; no copies el identificador de la Pokédex como definitivo.
3. Verifica que las versiones de los paquetes Capacitor sean compatibles entre sí.
4. Instala la plataforma Android y añádela una sola vez si aún no existe.
5. Ejecuta npx cap sync android después de compilar o cambiar configuración.
6. Abre Android Studio con npx cap open android.
7. Instala los SDK/JDK que requiera la versión de Capacitor elegida y permite sincronizar Gradle.
8. Prueba en un dispositivo/emulador y genera primero una APK de depuración.
9. Para distribuir una versión final, configura la firma y HTTPS.

[Flujo Android de Capacitor](https://capacitorjs.com/docs/android).

La APK no incluye el servidor C# ni SQL Server. Necesita que tu computadora permanezca
encendida, accesible y ejecutando el backend.

## 12. Qué está listo y qué queda por comprobar

Están preparados REST, JSON, CORS explícito, Bearer, autenticación del hub, contratos y los
tres modos de juego. Tetris.Cliente se conserva y ya puede usar el JWT en llamadas C#.

No se crearon pantallas, servicios Angular completos, estilos, animaciones ni APK.
La implementación del frontend y su cliente JavaScript de SignalR la harás tú.
Tampoco se comprobó todavía el firewall de tu red ni una partida con cuatro teléfonos físicos.

Empieza comprobando /salud desde el celular; luego crea las interfaces y un servicio de
acceso como en el quiz. Cuando login funcione, continúa con sala, conexión al hub y estado.

