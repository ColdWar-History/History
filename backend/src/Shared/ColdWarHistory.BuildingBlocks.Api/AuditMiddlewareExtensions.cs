using ColdWarHistory.BuildingBlocks.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ColdWarHistory.BuildingBlocks.Api;

public static class AuditMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestAudit(this IApplicationBuilder app, string serviceName)
    {
        return app.Use(async (context, next) =>
        {
            await next();

            try
            {
                var auditSink = context.RequestServices.GetRequiredService<IAuditSink>();
                var currentUserAccessor = context.RequestServices.GetRequiredService<ICurrentUserAccessor>();
                var currentUser = currentUserAccessor.GetCurrentUser();

                var details = new Dictionary<string, string?>
                {
                    ["method"] = context.Request.Method,
                    ["path"] = context.Request.Path,
                    ["statusCode"] = context.Response.StatusCode.ToString(),
                    ["traceId"] = context.TraceIdentifier
                };

                await auditSink.WriteAsync(
                    new AuditRecord(
                        DateTimeOffset.UtcNow,
                        serviceName,
                        "http.request",
                        context.Request.Path,
                        currentUser.UserId,
                        currentUser.UserName,
                        details),
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                var loggerFactory = context.RequestServices.GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger("ColdWarHistory.Audit");
                logger?.LogWarning(exception, "Audit write failed for {ServiceName}. Request response was preserved.", serviceName);
            }
        });
    }
}
