using System;
using System.Collections.Generic;
using System.Text;

namespace Dominio.EntidadesTipadas
{
    public class TUsuario
    {
        public int UsuarioId { get; set; }
        public string NombreUsuario { get; set; } = "";
        [System.Text.Json.Serialization.JsonIgnore]
        public string? Correo { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string PasswordHash { get; set; } = "";
        public DateTime FechaRegistro { get; set; }
        public bool Activo { get; set; }
        public bool GoogleVinculado { get; set; }
        public string? AvatarGoogle { get; set; }
    }
}
