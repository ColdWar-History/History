namespace ColdWarHistory.BuildingBlocks.Infrastructure;

public static class WorkspacePaths
{
    public static string ResolveBackendRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ColdWarHistory.Backend.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    public static string ResolveRuntimePath(params string[] segments)
    {
        var parts = new List<string> { ResolveBackendRoot(), "runtime" };
        parts.AddRange(segments);
        return Path.Combine(parts.ToArray());
    }
}
