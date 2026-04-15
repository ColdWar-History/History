namespace ColdWarHistory.BuildingBlocks.Application;

public sealed record CurrentUser(Guid? UserId, string? UserName, IReadOnlyCollection<string> Roles)
{
    public bool IsAuthenticated => UserId.HasValue;

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}

public interface ICurrentUserAccessor
{
    CurrentUser GetCurrentUser();
}
