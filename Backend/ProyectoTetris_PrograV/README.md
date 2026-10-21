# Tetris — servidor C# para Ionic + Angular

El frontend definitivo será Ionic, Angular, TypeScript, HTML y SCSS, empaquetado con Capacitor
para Android. Esta solución contiene el servidor que ejecutarás en tu computadora y los
contratos necesarios para que desarrolles personalmente la interfaz en Visual Studio Code.

No se creó un proyecto Ionic, pantallas ni APK.

## Ejecutar

1. Abre ProyectoTetris_PrograV.slnx en Visual Studio.
2. Selecciona ProyectoTetris_PrograV como proyecto de inicio y el perfil Tetris.API.
3. Verifica ConnectionStrings:TetrisDB en appsettings.json.
4. Ejecuta. En desarrollo escucha en todas las interfaces por el puerto TCP 5186.
5. Comprueba http://localhost:5186/salud desde tu computadora.
6. Sigue docs/FRONTEND.md para conectar un celular de la misma red.

~~~powershell
dotnet build ProyectoTetris_PrograV.slnx
dotnet run --project ProyectoTetris_PrograV/ProyectoTetris_PrograV.csproj --launch-profile Tetris.API
dotnet run --project Tetris.Pruebas/Tetris.Pruebas.csproj
~~~

## Solución conservada

Dominio, AccesoDatos, LogicaNegocio, Utilitarios y la API mantienen sus responsabilidades.
Tetris.Cliente se conserva como cliente HTTP C# independiente y referencia de contratos;
Ionic no necesita compilarlo ni referenciarlo.

El motor autoritativo mantiene los tres modos. El servidor publica el estado mediante
SignalR en /hubs/juego y guarda resultados en SQL Server.

## Autenticación y red

Ionic utilizará JWT Bearer. Login devuelve los datos públicos del usuario junto con
token, tipoToken y expiraEnUtc. Las cookies siguen disponibles para clientes anteriores.
Los orígenes permitidos están enumerados en appsettings.Development.json.

En Development, si no configuras Jwt:Clave, se genera una clave temporal en memoria.
Los tokens anteriores dejan de servir al reiniciar. Para una clave persistente utiliza
secretos de usuario o Jwt__Clave; nunca la escribas en archivos del repositorio.
Production exige una clave configurada y sus propios orígenes y URL de escucha.

Consulta docs/FRONTEND.md para IPv4, firewall privado, Capacitor, ejemplos TypeScript,
JWT, SignalR y los pasos futuros para generar el APK.

## Verificación

~~~powershell
dotnet run --project Tetris.Pruebas/Tetris.Pruebas.csproj -- --integracion-http
dotnet run --project Tetris.Pruebas/Tetris.Pruebas.csproj -- --integracion-sql
~~~

La integración HTTP usa un servidor temporal, cuatro conexiones WebSocket y usuarios
en memoria. La integración SQL crea datos dentro de una transacción y los revierte.
Las pruebas son un ejecutable: se usa dotnet run, no dotnet test.

Documentación: docs/ARQUITECTURA.md, docs/FRONTEND.md, docs/VERIFICACION.md y
docs/REGLAS_NUEVAS.md (piezas especiales, pausas por votación y salas privadas).
docs/CONFIGURACION_PARTIDA.md (pieza que elimina fila, piezas normales, velocidad, puntos y eventos de sonido).


Reglas definitivas, contratos, pruebas y pendientes: [docs/ACTUALIZACION_DEFINITIVA.md](docs/ACTUALIZACION_DEFINITIVA.md).
