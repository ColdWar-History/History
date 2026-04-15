namespace ColdWarHistory.BuildingBlocks.Api;

public sealed record OpenApiField(string Name, string Type, bool Required, string Description);

public sealed record OpenApiResponseDescription(int StatusCode, string Description);

public sealed record OpenApiEndpointDescription(
    string Method,
    string Path,
    string Summary,
    string Tag,
    bool RequiresAuthentication,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<OpenApiField> RequestFields,
    IReadOnlyCollection<OpenApiResponseDescription> Responses);
