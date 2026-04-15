using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Game.Domain;

namespace ColdWarHistory.Game.Application;

public sealed class GameService(
    IGameRepository repository,
    ICryptoGameClient cryptoClient,
    IGameProgressPublisher progressPublisher,
    IClock clock) : IGameService
{
    private static readonly IReadOnlyList<string> TrainingPhrases =
    [
        "COLDWAR",
        "BERLINAIRLIFT",
        "SIGNALCHECK",
        "DECODETHIS",
        "MISSIONALERT"
    ];

    public async Task<TrainingChallengeDto> GenerateTrainingChallengeAsync(string cipherCode, string difficulty, CancellationToken cancellationToken)
    {
        var normalizedDifficulty = string.IsNullOrWhiteSpace(difficulty) ? "normal" : difficulty.Trim().ToLowerInvariant();
        var (plainText, parameters, baseScore) = BuildTrainingTemplate(cipherCode, normalizedDifficulty);
        var encoded = await cryptoClient.ExecuteAsync(cipherCode, "encrypt", plainText, parameters, cancellationToken);
        var challenge = new TrainingChallenge(
            Guid.NewGuid(),
            cipherCode,
            normalizedDifficulty,
            $"Расшифруйте сообщение, используя шифр {cipherCode}.",
            encoded,
            "decrypt",
            parameters,
            plainText,
            baseScore,
            clock.UtcNow);

        await repository.StoreChallengeAsync(challenge, cancellationToken);
        return Map(challenge);
    }

    public async Task<ChallengeAttemptResultDto> SubmitChallengeAsync(Guid challengeId, SubmitTrainingAnswerRequest request, CurrentUser currentUser, CancellationToken cancellationToken)
    {
        var challenge = await repository.GetChallengeAsync(challengeId, cancellationToken)
            ?? throw new InvalidOperationException("Challenge not found.");

        var normalizedExpected = Normalize(request.Answer);
        var isCorrect = normalizedExpected == Normalize(challenge.ExpectedAnswer);
        var awardedScore = isCorrect ? Math.Max(0, challenge.BaseScore - (request.UsedHint ? 20 : 0)) : 0;

        if (currentUser.IsAuthenticated)
        {
            await progressPublisher.PublishChallengeCompletedAsync(
                new ChallengeCompletedEvent(challenge.Id, currentUser.UserId!.Value, currentUser.UserName ?? "unknown", challenge.CipherCode, isCorrect, awardedScore, clock.UtcNow),
                cancellationToken);
        }

        return new ChallengeAttemptResultDto(
            challenge.Id,
            isCorrect,
            awardedScore,
            isCorrect ? "Ответ верный, шаблон распознан корректно." : "Ответ не совпал с ожидаемым результатом расшифровки.",
            challenge.ExpectedAnswer,
            request.Answer,
            clock.UtcNow);
    }

    public async Task<DailyChallengeDto> GetDailyChallengeAsync(CancellationToken cancellationToken)
    {
        var current = await repository.GetDailyChallengeAsync(cancellationToken);
        if (current is null || current.Date != DateOnly.FromDateTime(clock.UtcNow.UtcDateTime))
        {
            await RefreshDailyChallengeAsync(cancellationToken);
            current = await repository.GetDailyChallengeAsync(cancellationToken);
        }

        return new DailyChallengeDto(current!.Date, Map(current.Challenge), current.Theme);
    }

    public async Task RefreshDailyChallengeAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var cipherCode = today.DayOfWeek switch
        {
            DayOfWeek.Monday => "caesar",
            DayOfWeek.Tuesday => "atbash",
            DayOfWeek.Wednesday => "vigenere",
            DayOfWeek.Thursday => "rail-fence",
            _ => "columnar"
        };

        var challenge = await GenerateTrainingChallengeAsync(cipherCode, "hard", cancellationToken);
        var storedChallenge = await repository.GetChallengeAsync(challenge.Id, cancellationToken)
            ?? throw new InvalidOperationException("Daily challenge was not stored.");

        await repository.SetDailyChallengeAsync(new DailyChallengeSnapshot(today, storedChallenge, "Daily Challenge"), cancellationToken);
    }

    public async Task<ShiftSessionDto> StartShiftAsync(string difficulty, CancellationToken cancellationToken)
    {
        var normalizedDifficulty = string.IsNullOrWhiteSpace(difficulty) ? "normal" : difficulty.Trim().ToLowerInvariant();
        var messages = new List<ShiftMessage>();

        foreach (var template in BuildShiftTemplates(normalizedDifficulty))
        {
            var encoded = await cryptoClient.ExecuteAsync(template.CipherCode, "encrypt", template.PlainText, template.Parameters, cancellationToken);
            messages.Add(new ShiftMessage(Guid.NewGuid(), template.Headline, encoded, template.CipherCode, template.Briefing, template.ExpectedDecision));
        }

        var session = new ShiftSession(Guid.NewGuid(), normalizedDifficulty, messages, clock.UtcNow);
        await repository.StoreShiftAsync(session, cancellationToken);
        return new ShiftSessionDto(session.Id, session.Difficulty, session.Messages.Select(message => new ShiftMessageDto(message.Id, message.Headline, message.EncodedMessage, message.CipherCode, message.Briefing)).ToArray(), session.StartedAt);
    }

    public async Task<ShiftResolutionDto> ResolveShiftMessageAsync(Guid shiftId, ResolveShiftMessageRequest request, CurrentUser currentUser, CancellationToken cancellationToken)
    {
        var session = await repository.GetShiftAsync(shiftId, cancellationToken)
            ?? throw new InvalidOperationException("Shift not found.");

        var message = session.Messages.FirstOrDefault(item => item.Id == request.MessageId)
            ?? throw new InvalidOperationException("Shift message not found.");

        var isCorrect = string.Equals(message.ExpectedDecision, request.Decision, StringComparison.OrdinalIgnoreCase);
        var score = isCorrect ? 60 : -20;
        var explanation = isCorrect
            ? "Решение совпало с ожидаемой оценкой достоверности."
            : $"Ожидалось решение '{message.ExpectedDecision}'.";

        session.RecordResolution(new ShiftResolution(request.MessageId, request.Decision, isCorrect, score, explanation));
        await repository.StoreShiftAsync(session, cancellationToken);

        if (session.IsCompleted && currentUser.IsAuthenticated)
        {
            var report = await GetShiftReportInternalAsync(session, currentUser, cancellationToken);
            await progressPublisher.PublishShiftCompletedAsync(
                new ShiftCompletedEvent(session.Id, currentUser.UserId!.Value, currentUser.UserName ?? "unknown", report.TotalScore, report.CorrectDecisions, report.IncorrectDecisions, report.CompletedAt),
                cancellationToken);
        }

        return new ShiftResolutionDto(request.MessageId, request.Decision, isCorrect, score, explanation);
    }

    public async Task<ShiftReportDto?> GetShiftReportAsync(Guid shiftId, CurrentUser currentUser, CancellationToken cancellationToken)
    {
        var session = await repository.GetShiftAsync(shiftId, cancellationToken)
            ?? throw new InvalidOperationException("Shift not found.");

        return session.IsCompleted ? await GetShiftReportInternalAsync(session, currentUser, cancellationToken) : null;
    }

    private Task<ShiftReportDto> GetShiftReportInternalAsync(ShiftSession session, CurrentUser currentUser, CancellationToken cancellationToken)
    {
        var correct = session.Resolutions.Values.Count(item => item.IsCorrect);
        var incorrect = session.Resolutions.Count - correct;
        var totalScore = session.Resolutions.Values.Sum(item => item.ScoreDelta);
        var recommendation = incorrect == 0
            ? "Отличная смена: можно повышать сложность."
            : "Стоит внимательнее сверять решение с контекстом сообщения и типом шифра.";

        return Task.FromResult(new ShiftReportDto(
            session.Id,
            totalScore,
            correct,
            incorrect,
            session.Resolutions.Values.Select(item => new ShiftResolutionDto(item.MessageId, item.Decision, item.IsCorrect, item.ScoreDelta, item.Explanation)).ToArray(),
            recommendation,
            clock.UtcNow));
    }

    private static TrainingChallengeDto Map(TrainingChallenge challenge) =>
        new(challenge.Id, challenge.CipherCode, challenge.Difficulty, challenge.Prompt, challenge.Input, challenge.ExpectedMode, challenge.Parameters, challenge.BaseScore, challenge.GeneratedAt);

    private static (string PlainText, IReadOnlyDictionary<string, string> Parameters, int BaseScore) BuildTrainingTemplate(string cipherCode, string difficulty)
    {
        var text = TrainingPhrases[Math.Abs(HashCode.Combine(cipherCode, difficulty)) % TrainingPhrases.Count];
        return cipherCode.ToLowerInvariant() switch
        {
            "caesar" => (text, new Dictionary<string, string> { ["shift"] = difficulty == "hard" ? "7" : "3" }, difficulty == "hard" ? 120 : 80),
            "atbash" => (text, new Dictionary<string, string>(), 70),
            "vigenere" => (text, new Dictionary<string, string> { ["key"] = difficulty == "hard" ? "CIPHER" : "KEY" }, difficulty == "hard" ? 140 : 100),
            "rail-fence" => (text, new Dictionary<string, string> { ["rails"] = difficulty == "hard" ? "4" : "3" }, difficulty == "hard" ? 130 : 90),
            "columnar" => (text, new Dictionary<string, string> { ["key"] = difficulty == "hard" ? "RADAR" : "CODE" }, difficulty == "hard" ? 150 : 110),
            _ => throw new InvalidOperationException($"Unsupported cipher {cipherCode}.")
        };
    }

    private static IReadOnlyCollection<ShiftTemplate> BuildShiftTemplates(string difficulty) =>
    [
        new("Перехват штаба", "REQUEST EVAC WINDOW", "caesar", new Dictionary<string, string> { ["shift"] = difficulty == "hard" ? "6" : "4" }, "Сообщение похоже на срочный запрос эвакуации.", "escalate"),
        new("Рутинная сводка", "SUPPLY TRAIN ON TIME", "rail-fence", new Dictionary<string, string> { ["rails"] = "3" }, "Служебная запись без признаков дезинформации.", "allow"),
        new("Подозрительный приказ", "IGNORE PRIOR CHANNEL", "vigenere", new Dictionary<string, string> { ["key"] = "MASK" }, "Сообщение просит игнорировать прежний канал связи.", "reject")
    ];

    private static string Normalize(string value) =>
        new(value.ToUpperInvariant().Where(char.IsLetter).ToArray());

    private sealed record ShiftTemplate(string Headline, string PlainText, string CipherCode, IReadOnlyDictionary<string, string> Parameters, string Briefing, string ExpectedDecision);
}
