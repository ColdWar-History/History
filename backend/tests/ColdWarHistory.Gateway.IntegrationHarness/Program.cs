using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ColdWarHistory.BuildingBlocks.Contracts;

var backendRoot = ResolveBackendRoot();
var services = new[]
{
    new ServiceProcess("auth", Path.Combine(backendRoot, "src", "Services", "Auth", "ColdWarHistory.Auth.Api", "ColdWarHistory.Auth.Api.csproj"), $"{ServiceEndpoints.Auth}/health"),
    new ServiceProcess("progress", Path.Combine(backendRoot, "src", "Services", "Progress", "ColdWarHistory.Progress.Api", "ColdWarHistory.Progress.Api.csproj"), $"{ServiceEndpoints.Progress}/health"),
    new ServiceProcess("crypto", Path.Combine(backendRoot, "src", "Services", "Crypto", "ColdWarHistory.Crypto.Api", "ColdWarHistory.Crypto.Api.csproj"), $"{ServiceEndpoints.Crypto}/health"),
    new ServiceProcess("content", Path.Combine(backendRoot, "src", "Services", "Content", "ColdWarHistory.Content.Api", "ColdWarHistory.Content.Api.csproj"), $"{ServiceEndpoints.Content}/health"),
    new ServiceProcess("game", Path.Combine(backendRoot, "src", "Services", "Game", "ColdWarHistory.Game.Api", "ColdWarHistory.Game.Api.csproj"), $"{ServiceEndpoints.Game}/health"),
    new ServiceProcess("gateway", Path.Combine(backendRoot, "src", "Services", "Gateway", "ColdWarHistory.Gateway.Api", "ColdWarHistory.Gateway.Api.csproj"), $"{ServiceEndpoints.Gateway}/health")
};

using var httpClient = new HttpClient { BaseAddress = new Uri(ServiceEndpoints.Gateway) };

try
{
    foreach (var service in services)
    {
        service.Start(backendRoot);
    }

    foreach (var service in services)
    {
        await WaitForHealthAsync(httpClient, service.HealthUrl, TimeSpan.FromSeconds(30));
    }

    var register = await (await httpClient.PostAsJsonAsync("/api/auth/register", new RegisterRequest("operator", "operator@cw.local", "Pass123!"))).Content.ReadFromJsonAsync<AuthTokensResponse>();
    Ensure(register is not null, "Registration failed.");

    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", register!.AccessToken);

    var ciphers = await httpClient.GetFromJsonAsync<List<CipherCardDto>>("/api/content/ciphers?publishedOnly=true");
    Ensure(ciphers is not null && ciphers.Count >= 5, "Content catalog is unavailable through gateway.");

    var cryptoResult = await (await httpClient.PostAsJsonAsync("/api/crypto/transform", new CryptoTransformRequest("caesar", "encrypt", "HELLO", new Dictionary<string, string> { ["shift"] = "3" }))).Content.ReadFromJsonAsync<CryptoTransformResponse>();
    Ensure(cryptoResult?.Output == "KHOOR", "Crypto transformation via gateway returned unexpected output.");

    var challenge = await (await httpClient.PostAsync("/api/game/training/generate?cipherCode=caesar&difficulty=normal", null)).Content.ReadFromJsonAsync<TrainingChallengeDto>();
    Ensure(challenge is not null, "Training challenge was not generated.");

    var challengeResult = await (await httpClient.PostAsJsonAsync($"/api/game/training/{challenge!.Id}/submit", new SubmitTrainingAnswerRequest("COLDWAR", false))).Content.ReadFromJsonAsync<ChallengeAttemptResultDto>();
    Ensure(challengeResult is not null, "Training challenge submission failed.");

    var shift = await (await httpClient.PostAsJsonAsync("/api/game/shifts/start", new StartShiftRequest("normal"))).Content.ReadFromJsonAsync<ShiftSessionDto>();
    Ensure(shift is not null && shift.Messages.Count == 3, "Shift session did not start correctly.");

    var decisions = new[] { "escalate", "allow", "reject" };
    for (var index = 0; index < shift!.Messages.Count; index++)
    {
        var message = shift.Messages.ElementAt(index);
        var resolution = await (await httpClient.PostAsJsonAsync($"/api/game/shifts/{shift.ShiftId}/resolve", new ResolveShiftMessageRequest(message.MessageId, decisions[index], null))).Content.ReadFromJsonAsync<ShiftResolutionDto>();
        Ensure(resolution is not null, $"Shift message {message.MessageId} was not resolved.");
    }

    var report = await httpClient.GetFromJsonAsync<ShiftReportDto>($"/api/game/shifts/{shift.ShiftId}/report");
    Ensure(report is not null && report.TotalScore > 0, "Shift report was not produced.");

    var profile = await httpClient.GetFromJsonAsync<UserProfileDto>("/api/progress/profile");
    Ensure(profile is not null, "Profile endpoint is unavailable.");
    Ensure(profile!.Metrics.CryptoOperations >= 1, "Crypto operation was not recorded in progress.");
    Ensure(profile.Metrics.ShiftReportsCompleted >= 1, "Shift completion was not recorded in progress.");

    var leaderboard = await httpClient.GetFromJsonAsync<List<LeaderboardEntryDto>>("/api/progress/leaderboard");
    Ensure(leaderboard is not null && leaderboard.Count >= 1, "Leaderboard endpoint is unavailable.");

    Console.WriteLine("Gateway integration harness completed successfully.");
}
finally
{
    foreach (var service in services.Reverse())
    {
        service.Dispose();
    }
}

static async Task WaitForHealthAsync(HttpClient gatewayClient, string url, TimeSpan timeout)
{
    using var client = new HttpClient();
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
