using System.Diagnostics;
using System.Net.Http.Json;

var backendRoot = ResolveBackendRoot();
var services = new[]
{
    new ServiceProcess("auth", Path.Combine(backendRoot, "src", "Services", "Auth", "ColdWarHistory.Auth.Api", "ColdWarHistory.Auth.Api.csproj"), "http://localhost:7001/health"),
    new ServiceProcess("progress", Path.Combine(backendRoot, "src", "Services", "Progress", "ColdWarHistory.Progress.Api", "ColdWarHistory.Progress.Api.csproj"), "http://localhost:7005/health"),
    new ServiceProcess("crypto", Path.Combine(backendRoot, "src", "Services", "Crypto", "ColdWarHistory.Crypto.Api", "ColdWarHistory.Crypto.Api.csproj"), "http://localhost:7002/health"),
    new ServiceProcess("content", Path.Combine(backendRoot, "src", "Services", "Content", "ColdWarHistory.Content.Api", "ColdWarHistory.Content.Api.csproj"), "http://localhost:7003/health"),
    new ServiceProcess("game", Path.Combine(backendRoot, "src", "Services", "Game", "ColdWarHistory.Game.Api", "ColdWarHistory.Game.Api.csproj"), "http://localhost:7004/health"),
    new ServiceProcess("gateway", Path.Combine(backendRoot, "src", "Services", "Gateway", "ColdWarHistory.Gateway.Api", "ColdWarHistory.Gateway.Api.csproj"), "http://localhost:7000/health")
};

var openApiChecks = new[]
{
    ("auth", "http://localhost:7001/openapi/v1.json", "/api/auth/login"),
    ("crypto", "http://localhost:7002/openapi/v1.json", "/api/crypto/transform"),
    ("content", "http://localhost:7003/openapi/v1.json", "/api/content/ciphers"),
    ("game", "http://localhost:7004/openapi/v1.json", "/api/game/daily"),
    ("progress", "http://localhost:7005/openapi/v1.json", "/api/progress/profile"),
    ("gateway", "http://localhost:7000/openapi/v1.json", "/api/auth/login")
};

try
{
    foreach (var service in services)
    {
        service.Start(backendRoot);
    }

    using var client = new HttpClient();

    foreach (var service in services)
    {
        await WaitForHealthAsync(client, service.HealthUrl, TimeSpan.FromSeconds(30));
    }

    foreach (var check in openApiChecks)
    {
        var json = await client.GetStringAsync(check.Item2);
        Ensure(json.Contains(check.Item3, StringComparison.OrdinalIgnoreCase), $"OpenAPI contract for {check.Item1} does not contain expected path {check.Item3}.");
    }

    Console.WriteLine("OpenAPI contract harness completed successfully.");
}
finally
{
    foreach (var service in services.Reverse())
    {
        service.Dispose();
    }
}

static async Task WaitForHealthAsync(HttpClient client, string url, TimeSpan timeout)
{
    var startedAt = DateTimeOffset.UtcNow;
    while (DateTimeOffset.UtcNow - startedAt < timeout)
    {
        try
        {
            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return;
            }
        }
        catch
        {
        }

        await Task.Delay(500);
    }

    throw new TimeoutException($"Service at {url} did not become healthy in time.");
}

static string ResolveBackendRoot()
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

    throw new DirectoryNotFoundException("Backend root was not found.");
}

static void Ensure(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class ServiceProcess(string name, string projectPath, string healthUrl) : IDisposable
{
    private Process? _process;

    public string HealthUrl { get; } = healthUrl;

    public void Start(string workingDirectory)
    {
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\" --no-build",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        _process.OutputDataReceived += (_, args) => { if (!string.IsNullOrWhiteSpace(args.Data)) Console.WriteLine($"[{name}] {args.Data}"); };
        _process.ErrorDataReceived += (_, args) => { if (!string.IsNullOrWhiteSpace(args.Data)) Console.Error.WriteLine($"[{name}:err] {args.Data}"); };
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public void Dispose()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
        }
        catch
        {
        }
        finally
        {
            _process.Dispose();
        }
    }
}
