
namespace Elovo.Application.Mapping;

public class ElovoMappingProfile : Profile
{
    public ElovoMappingProfile()
    {
        CreateMap<User, UserDto>();
    }
}
