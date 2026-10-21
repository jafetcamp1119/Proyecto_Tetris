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
            CreateMap<TUsuario, Usuario>(MemberList.None).ForMember(x => x.PasswordHash, o => o.Ignore());

            CreateMap<Usuario, TUsuario>().ForMember(x => x.PasswordHash, o => o.Ignore());
            CreateMap<TTurno, TurnosPartidum>(MemberList.None);
            CreateMap<TurnosPartidum, TTurno>(MemberList.None);
            CreateMap<TResultado, Resultado>(MemberList.None);
            CreateMap<Resultado, TResultado>();
            CreateMap<TParticipante, ParticipantesPartidum>(MemberList.None);
            CreateMap<ParticipantesPartidum, TParticipante>();
            CreateMap<TPartida, Partida>(MemberList.None);
            CreateMap<Partida, TPartida>();
            CreateMap<TConfiguracion, ConfiguracionJugador>(MemberList.None);
            CreateMap<ConfiguracionJugador, TConfiguracion>();
        }
    }
}
