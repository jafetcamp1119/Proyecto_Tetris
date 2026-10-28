# Backend de Proyecto Tetris

Consultar el [README principal](../../README.md) para configuración vigente, secretos, actualización no destructiva del esquema, ejecución, Android y limitaciones.

La solución conserva Dominio, AccesoDatos, LogicaNegocio, API, Utilitarios, cliente C#, pruebas y presentación. El servidor decide movimientos, puntuaciones, turnos, pausas y resultados.

~~~powershell
dotnet restore ProyectoTetris_PrograV.slnx
dotnet build ProyectoTetris_PrograV.slnx -c Release
dotnet run --project Tetris.Pruebas -c Release --no-build
dotnet run --project Tetris.Pruebas -c Release --no-build -- --integracion-http
dotnet run --project Tetris.Pruebas -c Release --no-build -- --conexion-publicada
~~~

Estas suites usan memoria o generación de SQL sin abrir la base real. Los modos opcionales que consultan SQL requieren una decisión separada.

docs/sql/06_sesion_unica.sql agrega dos columnas nullable a Usuarios y un índice sin borrar datos. No se aplica automáticamente. Conservar los scripts y documentos técnicos de docs como referencia; el código y el README principal contienen los contratos vigentes.
