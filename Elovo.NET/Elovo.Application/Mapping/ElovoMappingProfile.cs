using AutoMapper;
using Elovo.Application.DTOs;
using Elovo.Domain;

namespace Elovo.Application.Mapping;

public class ElovoMappingProfile : Profile
{
    public ElovoMappingProfile()
    {
        CreateMap<User, UserDto>();
    }
}
