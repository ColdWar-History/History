namespace ColdWarHistory.Crypto.Domain;

public sealed record CipherExecutionResult(string Output, IReadOnlyCollection<string> ValidationMessages, IReadOnlyCollection<CipherStep> Steps);

public sealed record CipherStep(int Order, string Title, string Description, string Snapshot);

public interface ICipherAlgorithm
{
    string Code { get; }
    string Name { get; }
    string Category { get; }
    string Era { get; }
    int Difficulty { get; }
    IReadOnlyCollection<CipherParameter> Parameters { get; }
    CipherExecutionResult Encrypt(string input, IReadOnlyDictionary<string, string> parameters);
    CipherExecutionResult Decrypt(string input, IReadOnlyDictionary<string, string> parameters);
}

public sealed record CipherParameter(string Name, string Label, string Type, bool IsRequired, string? Description);

public abstract class CipherAlgorithmBase : ICipherAlgorithm
{
    protected const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public abstract string Code { get; }
    public abstract string Name { get; }
    public abstract string Category { get; }
    public abstract string Era { get; }
    public abstract int Difficulty { get; }
    public abstract IReadOnlyCollection<CipherParameter> Parameters { get; }

    public abstract CipherExecutionResult Encrypt(string input, IReadOnlyDictionary<string, string> parameters);
    public abstract CipherExecutionResult Decrypt(string input, IReadOnlyDictionary<string, string> parameters);

    protected static string NormalizeLettersOnly(string input) =>
        new(input.ToUpperInvariant().Where(char.IsLetter).ToArray());

    protected static string ShiftBy(string input, int shift)
    {
        var buffer = input.ToUpperInvariant().ToCharArray();
        for (var index = 0; index < buffer.Length; index++)
        {
            var character = buffer[index];
            var alphabetIndex = Alphabet.IndexOf(character);
            if (alphabetIndex < 0)
            {
                continue;
            }

            buffer[index] = Alphabet[(alphabetIndex + shift + Alphabet.Length) % Alphabet.Length];
        }

        return new string(buffer);
    }
}

public sealed class CaesarCipher : CipherAlgorithmBase
{
    public override string Code => "caesar";
    public override string Name => "Caesar";
    public override string Category => "Substitution";
    public override string Era => "Classical";
    public override int Difficulty => 1;
    public override IReadOnlyCollection<CipherParameter> Parameters => [new("shift", "Shift", "integer", true, "Letter shift value")];

    public override CipherExecutionResult Encrypt(string input, IReadOnlyDictionary<string, string> parameters)
    {
        var shift = ParseShift(parameters);
        var normalized = input.ToUpperInvariant();
        var output = ShiftBy(normalized, shift);
        return new CipherExecutionResult(output, Array.Empty<string>(), [
            new CipherStep(1, "Normalize", "Transform text to uppercase for deterministic processing.", normalized),
            new CipherStep(2, "Shift alphabet", $"Move every alphabetic character by {shift} positions.", output)
        ]);
    }

    public override CipherExecutionResult Decrypt(string input, IReadOnlyDictionary<string, string> parameters)
    {
        var shift = ParseShift(parameters);
        var normalized = input.ToUpperInvariant();
        var output = ShiftBy(normalized, -shift);
        return new CipherExecutionResult(output, Array.Empty<string>(), [
            new CipherStep(1, "Normalize", "Transform text to uppercase.", normalized),
            new CipherStep(2, "Reverse shift", $"Move characters back by {shift} positions.", output)
        ]);
    }

    private static int ParseShift(IReadOnlyDictionary<string, string> parameters) =>
        parameters.TryGetValue("shift", out var value) && int.TryParse(value, out var shift)
            ? shift
            : throw new InvalidOperationException("Parameter 'shift' is required.");
}

public sealed class AtbashCipher : CipherAlgorithmBase
{
    public override string Code => "atbash";
    public override string Name => "Atbash";
    public override string Category => "Substitution";
    public override string Era => "Classical";
    public override int Difficulty => 1;
    public override IReadOnlyCollection<CipherParameter> Parameters => [];

    public override CipherExecutionResult Encrypt(string input, IReadOnlyDictionary<string, string> parameters) => Transform(input);
    public override CipherExecutionResult Decrypt(string input, IReadOnlyDictionary<string, string> parameters) => Transform(input);

    private static CipherExecutionResult Transform(string input)
    {
        var normalized = input.ToUpperInvariant();
        var buffer = normalized.ToCharArray();
        for (var index = 0; index < buffer.Length; index++)
        {
            var alphabetIndex = Alphabet.IndexOf(buffer[index]);
            if (alphabetIndex >= 0)
            {
                buffer[index] = Alphabet[Alphabet.Length - 1 - alphabetIndex];
            }
        }

        var output = new string(buffer);
        return new CipherExecutionResult(output, Array.Empty<string>(), [
            new CipherStep(1, "Mirror alphabet", "Replace every letter with its mirrored counterpart.", output)
        ]);
    }
}

public sealed class VigenereCipher : CipherAlgorithmBase
{
    public override string Code => "vigenere";
    public override string Name => "Vigenere";
    public override string Category => "Polyalphabetic";
    public override string Era => "Renaissance";
    public override int Difficulty => 3;
    public override IReadOnlyCollection<CipherParameter> Parameters => [new("key", "Key", "string", true, "Alphabetic key")];

    public override CipherExecutionResult Encrypt(string input, IReadOnlyDictionary<string, string> parameters) => Execute(input, parameters, encrypt: true);
    public override CipherExecutionResult Decrypt(string input, IReadOnlyDictionary<string, string> parameters) => Execute(input, parameters, encrypt: false);

    private static CipherExecutionResult Execute(string input, IReadOnlyDictionary<string, string> parameters, bool encrypt)
    {
        if (!parameters.TryGetValue("key", out var key) || string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Parameter 'key' is required.");
        }

        var normalizedKey = NormalizeLettersOnly(key);
        var inputChars = input.ToUpperInvariant().ToCharArray();
        var output = new char[inputChars.Length];
        var keyIndex = 0;

        for (var index = 0; index < inputChars.Length; index++)
        {
            var alphabetIndex = Alphabet.IndexOf(inputChars[index]);
            if (alphabetIndex < 0)
            {
                output[index] = inputChars[index];
                continue;
            }

            var shift = Alphabet.IndexOf(normalizedKey[keyIndex % normalizedKey.Length]);
            var offset = encrypt ? shift : -shift;
            output[index] = Alphabet[(alphabetIndex + offset + Alphabet.Length) % Alphabet.Length];
            keyIndex++;
        }

        return new CipherExecutionResult(new string(output), Array.Empty<string>(), [
            new CipherStep(1, "Normalize key", "Keep only alphabetic key characters and uppercase them.", normalizedKey),
            new CipherStep(2, encrypt ? "Encrypt by key shifts" : "Decrypt by reverse key shifts", "Apply one Caesar-like shift per key character.", new string(output))
        ]);
    }
}

public sealed class RailFenceCipher : CipherAlgorithmBase
{
    public override string Code => "rail-fence";
    public override string Name => "Rail Fence";
    public override string Category => "Transposition";
    public override string Era => "Modern";
    public override int Difficulty => 2;
    public override IReadOnlyCollection<CipherParameter> Parameters => [new("rails", "Rails", "integer", true, "Number of rails")];

    public override CipherExecutionResult Encrypt(string input, IReadOnlyDictionary<string, string> parameters)
    {
        var rails = ParseRails(parameters);
        var rows = Enumerable.Range(0, rails).Select(_ => new List<char>()).ToArray();
        var row = 0;
        var direction = 1;

        foreach (var character in input)
        {
            rows[row].Add(char.ToUpperInvariant(character));
            row += direction;
            if (row == 0 || row == rails - 1)
            {
                direction *= -1;
            }
        }

        var output = string.Concat(rows.SelectMany(item => item));
        var snapshot = string.Join(" | ", rows.Select((letters, index) => $"R{index + 1}:{new string(letters.ToArray())}"));
        return new CipherExecutionResult(output, Array.Empty<string>(), [
            new CipherStep(1, "Write zig-zag", "Distribute characters across rails in zig-zag order.", snapshot),
            new CipherStep(2, "Read row by row", "Concatenate rows to get the final cipher text.", output)
        ]);
    }

    public override CipherExecutionResult Decrypt(string input, IReadOnlyDictionary<string, string> parameters)
    {
        var rails = ParseRails(parameters);
        var pattern = BuildPattern(input.Length, rails);
        var railLengths = Enumerable.Range(0, rails).Select(rail => pattern.Count(item => item == rail)).ToArray();
        var segments = new List<char[]>();
        var cursor = 0;

        foreach (var length in railLengths)
        {
            segments.Add(input[cursor..(cursor + length)].ToUpperInvariant().ToCharArray());
            cursor += length;
        }

        var positions = new int[rails];
        var buffer = new char[input.Length];
        for (var index = 0; index < pattern.Count; index++)
        {
            var rail = pattern[index];
            buffer[index] = segments[rail][positions[rail]++];
        }

        var output = new string(buffer);
        return new CipherExecutionResult(output, Array.Empty<string>(), [
            new CipherStep(1, "Rebuild zig-zag pattern", "Compute how many letters belong to each rail.", string.Join(",", railLengths)),
            new CipherStep(2, "Restore original order", "Read letters back using the zig-zag traversal.", output)
        ]);
    }

    private static int ParseRails(IReadOnlyDictionary<string, string> parameters) =>
        parameters.TryGetValue("rails", out var value) && int.TryParse(value, out var rails) && rails >= 2
            ? rails
            : throw new InvalidOperationException("Parameter 'rails' must be an integer greater than 1.");

    private static List<int> BuildPattern(int length, int rails)
    {
        var pattern = new List<int>(length);
        var row = 0;
        var direction = 1;

        for (var index = 0; index < length; index++)
        {
            pattern.Add(row);
            row += direction;
            if (row == 0 || row == rails - 1)
            {
                direction *= -1;
            }
        }

        return pattern;
    }
}

public sealed class ColumnarTranspositionCipher : CipherAlgorithmBase
{
    public override string Code => "columnar";
    public override string Name => "Columnar Transposition";
    public override string Category => "Transposition";
    public override string Era => "Modern";
    public override int Difficulty => 3;
    public override IReadOnlyCollection<CipherParameter> Parameters => [new("key", "Key", "string", true, "Column ordering keyword")];

    public override CipherExecutionResult Encrypt(string input, IReadOnlyDictionary<string, string> parameters)
    {
        var key = ParseKey(parameters);
        var normalized = NormalizeLettersOnly(input);
        var columns = key.Length;
        var rows = (int)Math.Ceiling(normalized.Length / (double)columns);
        var grid = new char[rows, columns];
        var cursor = 0;

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                grid[row, column] = cursor < normalized.Length ? normalized[cursor++] : 'X';
            }
        }

        var order = GetColumnOrder(key);
        var buffer = new List<char>();
        foreach (var column in order)
        {
            for (var row = 0; row < rows; row++)
            {
                buffer.Add(grid[row, column]);
            }
        }

        var snapshot = BuildGridSnapshot(grid, rows, columns);
        var output = new string(buffer.ToArray());
        return new CipherExecutionResult(output, Array.Empty<string>(), [
            new CipherStep(1, "Fill grid", "Write normalized plaintext row by row into a grid.", snapshot),
            new CipherStep(2, "Read by sorted key columns", "Read columns in key order to form cipher text.", output)
        ]);
    }

    public override CipherExecutionResult Decrypt(string input, IReadOnlyDictionary<string, string> parameters)
    {
        var key = ParseKey(parameters);
        var normalized = NormalizeLettersOnly(input);
        var columns = key.Length;
        var rows = (int)Math.Ceiling(normalized.Length / (double)columns);
        var grid = new char[rows, columns];
        var order = GetColumnOrder(key);
        var cursor = 0;

        foreach (var column in order)
        {
            for (var row = 0; row < rows; row++)
            {
                if (cursor < normalized.Length)
                {
                    grid[row, column] = normalized[cursor++];
                }
            }
        }

        var buffer = new List<char>();
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                buffer.Add(grid[row, column]);
            }
        }

        var output = new string(buffer.ToArray()).TrimEnd('X');
        return new CipherExecutionResult(output, Array.Empty<string>(), [
            new CipherStep(1, "Restore ordered columns", "Fill each grid column according to sorted key order.", BuildGridSnapshot(grid, rows, columns)),
            new CipherStep(2, "Read row by row", "Read the grid row-wise to reconstruct plaintext.", output)
        ]);
    }

    private static string ParseKey(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("key", out var key) || string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Parameter 'key' is required.");
        }

        return NormalizeLettersOnly(key);
    }

    private static int[] GetColumnOrder(string key) =>
        key.Select((character, index) => new { character, index })
            .OrderBy(item => item.character)
            .ThenBy(item => item.index)
            .Select(item => item.index)
            .ToArray();

    private static string BuildGridSnapshot(char[,] grid, int rows, int columns)
    {
        var lines = new List<string>();
        for (var row = 0; row < rows; row++)
        {
            var buffer = new char[columns];
            for (var column = 0; column < columns; column++)
            {
                buffer[column] = grid[row, column];
            }

            lines.Add(new string(buffer));
        }

        return string.Join(" | ", lines);
    }
}
