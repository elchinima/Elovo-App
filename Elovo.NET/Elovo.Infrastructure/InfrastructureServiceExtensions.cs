
namespace Elovo.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString) || IsPlaceholder(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
        }

        services.AddDbContext<ElovoDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure()));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IFriendRequestRepository, FriendRequestRepository>();
        services.AddScoped<IPendingMessageRepository, PendingMessageRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    private static bool IsPlaceholder(string value)
    {
        return value.StartsWith("Set via ", StringComparison.OrdinalIgnoreCase);
    }
}
