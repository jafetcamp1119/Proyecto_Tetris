using AutoMapper;

using Dominio.Entidades;
using Dominio.EntidadesTipadas;

namespace Dominio.DTO
{
    // Define qué propiedades se copian entre entidades de SQL y datos de la aplicación.
    // La información privada de autenticación no forma parte del usuario público.
    public class AutoMapperProfile : Profile
    {
        // Configura los mapeos utilizados por los servicios y excluye los campos privados del usuario.
        public AutoMapperProfile()
        {
            CreateMap<TUsuario, Usuario>(MemberList.None)
                .ForMember(usuario => usuario.PasswordHash, opciones => opciones.Ignore())
                .ForMember(usuario => usuario.Correo, opciones => opciones.Ignore())
                .ForMember(usuario => usuario.NombreUsuarioNormalizado, opciones => opciones.Ignore());

            CreateMap<Usuario, TUsuario>()
                .ForMember(x => x.PasswordHash, o => o.Ignore())
                .ForMember(x => x.GoogleVinculado, o => o.MapFrom(x => x.GoogleSub != null));
            CreateMap<TTurno, TurnosPartidum>(MemberList.None);
            CreateMap<TurnosPartidum, TTurno>(MemberList.None);
            CreateMap<TResultado, Resultado>(MemberList.None);
            CreateMap<Resultado, TResultado>()
                .ForMember(x => x.Modalidad, o => o.Ignore())
                .ForMember(x => x.EmpatePrimerLugar, o => o.Ignore())
                .ForMember(x => x.CantidadParticipantes, o => o.Ignore());
            CreateMap<TParticipante, ParticipantesPartidum>(MemberList.None);
            CreateMap<ParticipantesPartidum, TParticipante>();
            CreateMap<TPartida, Partida>(MemberList.None);
            CreateMap<Partida, TPartida>();
            CreateMap<TConfiguracion, ConfiguracionJugador>(MemberList.None);
            CreateMap<ConfiguracionJugador, TConfiguracion>();
        }
    }
}
