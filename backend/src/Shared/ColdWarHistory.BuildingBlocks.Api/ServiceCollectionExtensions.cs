using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace ColdWarHistory.BuildingBlocks.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddColdWarApiDefaults(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, ColdWarHistory.BuildingBlocks.Infrastructure.SystemClock>();
        services.AddSingleton<IAuditSink, JsonLinesAuditSink>();
        services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
        return services;
    }

    public static IServiceCollection AddGatewayForwardedAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(HeaderForwardedAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderForwardedAuthenticationHandler>(
                HeaderForwardedAuthenticationHandler.SchemeName,
                _ => { });

        services.AddAuthorization();
        return services;
    }
}
