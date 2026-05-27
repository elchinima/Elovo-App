
namespace Elovo.Application.Mapping;

public class ElovoMappingProfile : Profile
{
    public ElovoMappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(
                destination => destination.IsOnline,
                options => options.MapFrom(source => source.Session != null && source.Session.IsOnline))
            .ForMember(
                destination => destination.LastSeenAt,
                options => options.MapFrom(source => source.Session == null ? null : source.Session.LastSeenAt));
    }
}
