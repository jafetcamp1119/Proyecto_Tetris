using Dominio.EntidadesTipadas;
using Utilitarios;

namespace Dominio.InterfazJuego;

public interface ISalaLJ
{
    Respuesta<TSala> Crear(int usuarioId, string nombre, TCrearSala datos);
    Respuesta<TSala> ConfigurarPiezas(Guid salaId, int usuarioId, TConfiguracionPiezas datos);
    Respuesta<TSala> ConfigurarSala(Guid salaId, int usuarioId, TConfiguracionSala datos);
    TSalaPublica[] ListarPublicas();
    Respuesta<TSala> Unirse(Guid salaId, int usuarioId, string nombre);
    Respuesta<TSala> UnirsePorCodigo(string codigo, int usuarioId, string nombre);
    Respuesta<TSala> Abandonar(Guid salaId, int usuarioId);
    Respuesta<TSala> Obtener(Guid salaId, int usuarioId);
    Respuesta<TSala> Preparar(Guid salaId, int usuarioId, bool listo);
    Respuesta<TSala> Iniciar(Guid salaId, int usuarioId);
    Respuesta<TSala> NuevaRonda(Guid salaId, int usuarioId);
    Respuesta<TSala> Mover(Guid salaId, int usuarioId, TMovimiento datos);
    Respuesta<TSala> SolicitarPausa(Guid salaId, int usuarioId);
    Respuesta<TSala> VotarPausa(Guid salaId, int usuarioId, bool aceptar);
    Respuesta<TSala> Reanudar(Guid salaId, int usuarioId);
    Respuesta<TSala> Conectar(Guid salaId, int usuarioId, string conexionId);
    bool ConexionAutorizada(Guid salaId, int usuarioId, string conexionId);
    TSala[] Desconectar(string conexionId);
    TSala[] Avanzar(DateTime ahora);

    // Entrega y vacía los eventos de pausa pendientes para que la API los publique.
    TEventoSala[] TomarEventos();
    TSala[] PendientesPersistencia();
    void ConfirmarPersistencia(Guid salaId, long revision);
}
