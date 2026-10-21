using System;
using System.Collections.Generic;
using System.Text;

namespace Dominio.EntidadesTipadas
{
    public class TConfiguracion
    {
        public int ConfiguracionId { get; set; }
        public int UsuarioId { get; set; }
        public bool MostrarControlesPantalla { get; set; }
        public bool PermitirPausa { get; set; }
        public bool SonidoActivo { get; set; }
    }
}
