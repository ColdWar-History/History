using ColdWarHistory.Auth.Application;
using ColdWarHistory.Auth.Domain;
using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Content.Application;
using ColdWarHistory.Content.Domain;
using ColdWarHistory.Game.Application;
using ColdWarHistory.Game.Domain;
using ColdWarHistory.Progress.Application;
using ColdWarHistory.Progress.Domain;

var tests = new ServiceApplicationTests();
await tests.RunAllAsync();

Console.WriteLine("Service unit harness completed successfully.");

internal sealed class ServiceApplicationTests
{
    private static readonly CancellationToken CancellationToken = CancellationToken.None;

    public async Task RunAllAsync()
    {
        await AuthRegistersLogsInRefreshesAndRejectsInvalidCredentialsAsync();
        await ContentFiltersPublishesAndVersionsCatalogItemsAsync();
        await GameGeneratesScoresReportsAndPublishesProgressAsync();
        await ProgressRecordsAchievementsAndRanksLeaderboardAsync();
    }

    private static async Task AuthRegistersLogsInRefreshesAndRejectsInvalidCredentialsAsync()
    {
        var repository = new InMemoryAuthUserRepository();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-05-16T10:00:00Z"));
        var service = new AuthService(repository, new PrefixPasswordHasher(), new DeterministicTokenFactory(clock), clock);

        var registration = await service.RegisterAsync(new RegisterRequest(" operator ", " operator@cw.local ", "Pass123!"), CancellationToken);
        Ensure(registration.IsSuccess, "Registration should succeed.");
        EnsureEqual("operator", registration.Value!.UserName, "Registration trims user name.");
        Ensure(registration.Value.Roles.Contains(Roles.User), "Registered user should receive user role.");
        EnsureEqual(1, repository.Users.Count, "Registration should persist one user.");

        var duplicate = await service.RegisterAsync(new RegisterRequest("operator", "other@cw.local", "Pass123!"), CancellationToken);
        Ensure(!duplicate.IsSuccess && duplicate.Error?.Code == "conflict", "Duplicate user name should fail with conflict.");

        var badLogin = await service.LoginAsync(new LoginRequest("operator", "wrong"), CancellationToken);
        Ensure(!badLogin.IsSuccess && badLogin.Error?.Code == "unauthorized", "Invalid credentials should fail.");

        var login = await service.LoginAsync(new LoginRequest("operator@cw.local", "Pass123!"), CancellationToken);
        Ensure(login.IsSuccess, "Login by email should succeed.");

        var introspection = await service.IntrospectAsync(new TokenIntrospectionRequest(login.Value!.AccessToken), CancellationToken);
        Ensure(introspection.IsActive && introspection.UserId == login.Value.UserId, "Issued access token should introspect as active.");

        var refresh = await service.RefreshAsync(new RefreshTokenRequest(login.Value.RefreshToken), CancellationToken);
        Ensure(refresh.IsSuccess, "Refresh should succeed for active refresh token.");

        var reuseOldRefresh = await service.RefreshAsync(new RefreshTokenRequest(login.Value.RefreshToken), CancellationToken);
        Ensure(!reuseOldRefresh.IsSuccess && reuseOldRefresh.Error?.Code == "unauthorized", "Refresh token reuse should be rejected after rotation.");

        await service.LogoutAsync(new LogoutRequest(refresh.Value!.RefreshToken), CancellationToken);
        var afterLogout = await service.RefreshAsync(new RefreshTokenRequest(refresh.Value.RefreshToken), CancellationToken);
        Ensure(!afterLogout.IsSuccess, "Logout should revoke the active refresh token.");
    }

    private static async Task ContentFiltersPublishesAndVersionsCatalogItemsAsync()
    {
        var repository = new InMemoryContentRepository();
        var publishedCipherId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var draftCipherId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        repository.Ciphers.Add(new CipherCard(publishedCipherId, "caesar", "Caesar", "Substitution", "Classical", 1, "Roman shift", "Description", "DWWDFN", [], PublicationStatus.Published));
        repository.Ciphers.Add(new CipherCard(draftCipherId, "mask", "Mask", "Transposition", "Modern", 3, "Hidden draft", "Description", "MASK", [], PublicationStatus.Draft));

        repository.Events.Add(new HistoricalEvent(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Berlin Airlift", new DateOnly(1948, 6, 24), "Europe", "Logistics", "Summary", "Description", ["USA", "USSR"], ["caesar"], PublicationStatus.Published));
        repository.Events.Add(new HistoricalEvent(Guid.Parse("44444444-4444-4444-4444-444444444444"), "Draft Event", new DateOnly(1950, 1, 1), "Asia", "Signals", "Summary", "Description", [], ["mask"], PublicationStatus.Draft));
        repository.Collections.Add(new CuratedCollection(Guid.Parse("55555555-5555-5555-5555-555555555555"), "Published Set", "Signals", "Summary", [], ["caesar"], PublicationStatus.Published));
        repository.Collections.Add(new CuratedCollection(Guid.Parse("66666666-6666-6666-6666-666666666666"), "Draft Set", "Signals", "Summary", [], ["mask"], PublicationStatus.Draft));

        var service = new ContentService(repository);

        var publishedCiphers = await service.GetCiphersAsync(null, null, null, publishedOnly: true, CancellationToken);
        EnsureEqual(1, publishedCiphers.Count, "Published cipher filter should hide drafts.");
        EnsureEqual("caesar", publishedCiphers.Single().Code, "Published cipher filter should keep published item.");

        var search = await service.GetCiphersAsync("roman", "substitution", "classical", publishedOnly: true, CancellationToken);
        EnsureEqual(1, search.Count, "Cipher search/category/era filters should be case-insensitive.");

        var created = await service.UpsertCipherAsync(null, new UpsertCipherCardRequest("vigenere", "Vigenere", "Polyalphabetic", "Renaissance", 3, "Summary", "Description", "LXFOPV", [], "editor", "Initial draft"), CancellationToken);
        EnsureEqual("Draft", created.PublicationStatus, "Created cipher should start as draft.");
        EnsureEqual(1, created.Versions.Count, "Created cipher should receive first version.");

        await service.PublishCipherAsync(created.Id, "Published", CancellationToken);
        var publishedCreated = await service.GetCipherAsync(created.Id, CancellationToken);
        EnsureEqual("Published", publishedCreated!.PublicationStatus, "Publish should change cipher status.");

        await EnsureThrowsAsync(() => service.PublishCipherAsync(created.Id, "Unsupported", CancellationToken), "Unsupported publication status should fail.");

        var europe1948 = await service.GetEventsAsync("europe", 1948, "logistics", publishedOnly: true, CancellationToken);
        EnsureEqual(1, europe1948.Count, "Event filters should combine region, year, topic and publication status.");

        var collections = await service.GetCollectionsAsync(publishedOnly: true, CancellationToken);
        EnsureEqual(1, collections.Count, "Published collection filter should hide drafts.");
        Ensure(repository.SaveCalls >= 2, "Content mutations should save changes.");
    }

    private static async Task GameGeneratesScoresReportsAndPublishesProgressAsync()
    {
        var repository = new InMemoryGameRepository();
        var cryptoClient = new EchoCryptoGameClient();
        var progressPublisher = new RecordingGameProgressPublisher();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-05-16T11:00:00Z"));
        var service = new GameService(repository, cryptoClient, progressPublisher, clock);
        var user = new CurrentUser(Guid.Parse("77777777-7777-7777-7777-777777777777"), "operator", [Roles.User]);

        var challenge = await service.GenerateTrainingChallengeAsync("caesar", "hard", CancellationToken);
        Ensure(repository.Challenges.ContainsKey(challenge.Id), "Generated challenge should be stored.");
        var storedChallenge = repository.Challenges[challenge.Id];
        EnsureEqual("hard", challenge.Difficulty, "Difficulty should be normalized.");
        EnsureEqual($"encoded:{storedChallenge.ExpectedAnswer}", challenge.Input, "Game should ask crypto client to encode challenge input.");

        var correctAttempt = await service.SubmitChallengeAsync(challenge.Id, new SubmitTrainingAnswerRequest(storedChallenge.ExpectedAnswer.ToLowerInvariant(), UsedHint: true), user, CancellationToken);
        Ensure(correctAttempt.IsCorrect, "Challenge answer should be normalized before comparison.");
        EnsureEqual(challenge.BaseScore - 20, correctAttempt.AwardedScore, "Hint should reduce awarded score.");
        EnsureEqual(1, progressPublisher.CompletedChallenges.Count, "Authenticated challenge should publish progress.");

        var wrongAttempt = await service.SubmitChallengeAsync(challenge.Id, new SubmitTrainingAnswerRequest("wrong", UsedHint: false), CurrentUserGuest(), CancellationToken);
        Ensure(!wrongAttempt.IsCorrect && wrongAttempt.AwardedScore == 0, "Wrong guest answer should score zero.");
        EnsureEqual(1, progressPublisher.CompletedChallenges.Count, "Guest challenge should not publish progress.");

        var shift = await service.StartShiftAsync("normal", CancellationToken);
        EnsureEqual(3, shift.Messages.Count, "Shift should contain three messages.");

        foreach (var message in shift.Messages)
        {
            var expectedDecision = message.Headline switch
            {
                "Перехват штаба" => "escalate",
                "Рутинная сводка" => "allow",
                _ => "reject"
            };

            await service.ResolveShiftMessageAsync(shift.ShiftId, new ResolveShiftMessageRequest(message.MessageId, expectedDecision, null), user, CancellationToken);
        }

        var report = await service.GetShiftReportAsync(shift.ShiftId, user, CancellationToken);
        Ensure(report is not null, "Completed shift should produce report.");
        EnsureEqual(180, report!.TotalScore, "All correct shift decisions should score 180.");
        EnsureEqual(1, progressPublisher.CompletedShifts.Count, "Completed shift should publish progress once.");

        var incompleteShift = await service.StartShiftAsync("normal", CancellationToken);
        var incompleteReport = await service.GetShiftReportAsync(incompleteShift.ShiftId, user, CancellationToken);
        Ensure(incompleteReport is null, "Incomplete shift should not produce report.");
    }

    private static async Task ProgressRecordsAchievementsAndRanksLeaderboardAsync()
    {
        var repository = new InMemoryProgressRepository();
        var service = new ProgressService(repository);
        var userId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var rivalId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        await service.RecordCryptoOperationAsync(new CryptoOperationRecordedEvent(Guid.NewGuid(), userId, "operator", "caesar", "encrypt", "HELLO", "KHOOR", DateTimeOffset.Parse("2026-05-16T12:00:00Z")), CancellationToken);
        var profileAfterOperation = await service.GetProfileAsync(userId, "operator", CancellationToken);
        EnsureEqual(1, profileAfterOperation.Metrics.CryptoOperations, "Crypto operation should be visible in profile.");
        Ensure(profileAfterOperation.Achievements.Any(item => item.Code == "first-signal"), "First crypto operation should unlock first-signal.");

        for (var index = 0; index < 5; index++)
        {
            await service.RecordChallengeCompletedAsync(new ChallengeCompletedEvent(Guid.NewGuid(), userId, "operator", "caesar", true, 20, DateTimeOffset.UtcNow), CancellationToken);
        }

        await service.RecordShiftCompletedAsync(new ShiftCompletedEvent(Guid.NewGuid(), userId, "operator", 75, 2, 1, DateTimeOffset.UtcNow), CancellationToken);

        for (var index = 0; index < 22; index++)
        {
            await service.RecordCryptoOperationAsync(new CryptoOperationRecordedEvent(Guid.NewGuid(), userId, "operator", "atbash", "decrypt", $"IN{index}", $"OUT{index}", DateTimeOffset.UtcNow.AddMinutes(index)), CancellationToken);
        }

        var profile = await service.GetProfileAsync(userId, "operator", CancellationToken);
        EnsureEqual(20, profile.RecentOperations.Count, "Recent operations should be capped at 20.");
        EnsureEqual(5, profile.Metrics.CorrectChallenges, "Correct challenges should be counted.");
        EnsureEqual(1, profile.Metrics.ShiftReportsCompleted, "Shift completions should be counted.");
        Ensure(profile.Achievements.Any(item => item.Code == "cadet"), "First challenge should unlock cadet.");
        Ensure(profile.Achievements.Any(item => item.Code == "analyst"), "Five correct challenges should unlock analyst.");
        Ensure(profile.Achievements.Any(item => item.Code == "inspector"), "First shift should unlock inspector.");

        await service.RecordChallengeCompletedAsync(new ChallengeCompletedEvent(Guid.NewGuid(), rivalId, "rival", "caesar", true, profile.Metrics.TotalScore, DateTimeOffset.UtcNow), CancellationToken);

        var leaderboard = await service.GetLeaderboardAsync(CancellationToken);
        EnsureEqual(userId, leaderboard.First().UserId, "Leaderboard should rank by score and then correct answers.");
    }

    private static CurrentUser CurrentUserGuest() => new(null, null, []);

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void EnsureEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
        }
    }

    private static async Task EnsureThrowsAsync(Func<Task> action, string message)
    {
        try
        {
            await action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}

internal sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = utcNow;
}

internal sealed class PrefixPasswordHasher : IPasswordHasher
{
    public string Hash(string value) => $"hashed:{value}";
}

internal sealed class DeterministicTokenFactory(IClock clock) : ITokenFactory
{
    private int _counter;

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken() =>
        ($"access-{++_counter}", clock.UtcNow.AddMinutes(15));

    public (string Token, DateTimeOffset ExpiresAt) CreateRefreshToken() =>
        ($"refresh-{++_counter}", clock.UtcNow.AddDays(7));
}

internal sealed class InMemoryAuthUserRepository : IAuthUserRepository
{
    public List<AuthUser> Users { get; } = [];

    public Task<AuthUser?> FindByUserNameOrEmailAsync(string userNameOrEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(user =>
            string.Equals(user.UserName, userNameOrEmail, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Email, userNameOrEmail, StringComparison.OrdinalIgnoreCase)));

    public Task<AuthUser?> FindByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(user => user.RefreshSessions.Any(session => session.Token == refreshToken)));

    public Task<AuthUser?> FindByAccessTokenAsync(string accessToken, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(user => user.AccessSessions.Any(session => session.Token == accessToken)));

    public Task<bool> ExistsAsync(string userName, string email, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Any(user =>
            string.Equals(user.UserName, userName.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(AuthUser user, CancellationToken cancellationToken)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class InMemoryContentRepository : IContentRepository
{
    public List<CipherCard> Ciphers { get; } = [];
    public List<HistoricalEvent> Events { get; } = [];
    public List<CuratedCollection> Collections { get; } = [];
    public int SaveCalls { get; private set; }

    public Task<IReadOnlyCollection<CipherCard>> GetCiphersAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<CipherCard>>(Ciphers);
    public Task<CipherCard?> GetCipherAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Ciphers.FirstOrDefault(item => item.Id == id));
    public Task AddCipherAsync(CipherCard cipher, CancellationToken cancellationToken)
    {
        Ciphers.Add(cipher);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<HistoricalEvent>> GetEventsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<HistoricalEvent>>(Events);
    public Task<HistoricalEvent?> GetEventAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Events.FirstOrDefault(item => item.Id == id));
    public Task AddEventAsync(HistoricalEvent historicalEvent, CancellationToken cancellationToken)
    {
        Events.Add(historicalEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<CuratedCollection>> GetCollectionsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<CuratedCollection>>(Collections);
    public Task<CuratedCollection?> GetCollectionAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Collections.FirstOrDefault(item => item.Id == id));
    public Task AddCollectionAsync(CuratedCollection collection, CancellationToken cancellationToken)
    {
        Collections.Add(collection);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryGameRepository : IGameRepository
{
    public Dictionary<Guid, TrainingChallenge> Challenges { get; } = [];
    public Dictionary<Guid, ShiftSession> Shifts { get; } = [];
    public DailyChallengeSnapshot? DailyChallenge { get; private set; }

    public Task StoreChallengeAsync(TrainingChallenge challenge, CancellationToken cancellationToken)
    {
        Challenges[challenge.Id] = challenge;
        return Task.CompletedTask;
    }

    public Task<TrainingChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken) =>
        Task.FromResult(Challenges.GetValueOrDefault(challengeId));

    public Task StoreShiftAsync(ShiftSession session, CancellationToken cancellationToken)
    {
        Shifts[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<ShiftSession?> GetShiftAsync(Guid shiftId, CancellationToken cancellationToken) =>
        Task.FromResult(Shifts.GetValueOrDefault(shiftId));

    public Task<DailyChallengeSnapshot?> GetDailyChallengeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(DailyChallenge);

    public Task SetDailyChallengeAsync(DailyChallengeSnapshot snapshot, CancellationToken cancellationToken)
    {
        DailyChallenge = snapshot;
        return Task.CompletedTask;
    }
}

internal sealed class EchoCryptoGameClient : ICryptoGameClient
{
    public Task<string> ExecuteAsync(string cipherCode, string mode, string input, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken) =>
        Task.FromResult($"encoded:{input}");
}

internal sealed class RecordingGameProgressPublisher : IGameProgressPublisher
{
    public List<ChallengeCompletedEvent> CompletedChallenges { get; } = [];
    public List<ShiftCompletedEvent> CompletedShifts { get; } = [];

    public Task PublishChallengeCompletedAsync(ChallengeCompletedEvent integrationEvent, CancellationToken cancellationToken)
    {
        CompletedChallenges.Add(integrationEvent);
        return Task.CompletedTask;
    }

    public Task PublishShiftCompletedAsync(ShiftCompletedEvent integrationEvent, CancellationToken cancellationToken)
    {
        CompletedShifts.Add(integrationEvent);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryProgressRepository : IProgressRepository
{
    private readonly Dictionary<Guid, UserProgressAggregate> _items = [];

    public Task<UserProgressAggregate> GetOrCreateAsync(Guid userId, string userName, CancellationToken cancellationToken)
    {
        if (!_items.TryGetValue(userId, out var item))
        {
            item = new UserProgressAggregate(userId, userName);
            _items[userId] = item;
        }
        else
        {
            item.Rename(userName);
        }

        return Task.FromResult(item);
    }

    public Task<IReadOnlyCollection<UserProgressAggregate>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<UserProgressAggregate>>(_items.Values.ToArray());

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
