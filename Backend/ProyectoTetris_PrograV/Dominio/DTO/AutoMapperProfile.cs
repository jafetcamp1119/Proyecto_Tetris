using Dominio.Entidades;
using AutoMapper;
using Dominio.EntidadesTipadas;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dominio.DTO
{
    public class AutoMapperProfile : Profile
    {
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
