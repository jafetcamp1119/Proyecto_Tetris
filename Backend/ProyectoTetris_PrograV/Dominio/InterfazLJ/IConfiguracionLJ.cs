using System;
using System.Collections.Generic;
using System.Text;
using Dominio.EntidadesTipadas;
using Utilitarios;

namespace Dominio.InterfazLJ
{
    public interface IConfiguracionLJ
    {
        Respuesta<TConfiguracion> ObtenerPorUsuario(int usuarioId);

        Respuesta<TConfiguracion> GuardarPorUsuario(int usuarioId, TConfiguracion datos);
        Respuesta<TConfiguracion> Insertar(TConfiguracion configuracion);
        Respuesta<TConfiguracion> Modificar(TConfiguracion configuracion);
        Respuesta<bool> Eliminar(TConfiguracion configuracion);
        Respuesta<IEnumerable<TConfiguracion>> Obtener(TConfiguracion configuracion);
        Respuesta<TConfiguracion> Buscar(TConfiguracion configuracion);
        Respuesta<IEnumerable<TConfiguracion>> Listar();
    }
}
