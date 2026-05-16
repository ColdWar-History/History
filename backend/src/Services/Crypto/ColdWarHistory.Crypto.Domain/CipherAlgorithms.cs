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
    IReadOnlyCollection<string> Limitations { get; }
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
    public abstract IReadOnlyCollection<string> Limitations { get; }

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
    public override string Name => "Шифр Цезаря";
    public override string Category => "Подстановка";
    public override string Era => "Античность";
    public override int Difficulty => 1;
    public override IReadOnlyCollection<CipherParameter> Parameters => [new("shift", "Сдвиг", "integer", true, "Величина сдвига букв")];
    public override IReadOnlyCollection<string> Limitations =>
    [
        "Работает с латинским алфавитом A-Z; буквы приводятся к верхнему регистру.",
        "Нелатинские символы остаются без изменений, потому что не входят в таблицу алфавита.",
        "Это учебный исторический шифр: его легко взломать перебором сдвига или частотным анализом."
    ];

    public override CipherExecutionResult Encrypt(string input, IReadOnlyDictionary<string, string> parameters)
    {
        var shift = ParseShift(parameters);
        var normalized = input.ToUpperInvariant();
        var output = ShiftBy(normalized, shift);
        return new CipherExecutionResult(output, Array.Empty<string>(), [
            new CipherStep(1, "Нормализация", "Приводим текст к верхнему регистру для предсказуемой обработки.", normalized),
            new CipherStep(2, "Сдвиг алфавита", $"Сдвигаем каждую латинскую букву на {shift} позиций.", output)
        ]);
    }

    public override CipherExecutionResult Decrypt(string input, IReadOnlyDictionary<string, string> parameters)
    {
        var shift = ParseShift(parameters);
        var normalized = input.ToUpperInvariant();
        var output = ShiftBy(normalized, -shift);
        return new CipherExecutionResult(output, Array.Empty<string>(), [
            new CipherStep(1, "Нормализация", "Приводим текст к верхнему регистру.", normalized),
            new CipherStep(2, "Обратный сдвиг", $"Сдвигаем буквы назад на {shift} позиций.", output)
        ]);
    }

    private static int ParseShift(IReadOnlyDictionary<string, string> parameters) =>
        parameters.TryGetValue("shift", out var value) && int.TryParse(value, out var shift)
            ? shift
            : throw new InvalidOperationException("Параметр 'shift' обязателен.");
}

public sealed class AtbashCipher : CipherAlgorithmBase
{
    public override string Code => "atbash";
    public override string Name => "Атбаш";
    public override string Category => "Подстановка";
    public override string Era => "Классические шифры";
    public override int Difficulty => 1;
    public override IReadOnlyCollection<CipherParameter> Parameters => [];
    public override IReadOnlyCollection<string> Limitations =>
    [
        "Работает с латинским алфавитом A-Z; буквы приводятся к верхнему регистру.",
        "Секретного ключа нет, поэтому распознанный Атбаш сразу обращается обратно.",
        "Нелатинские символы остаются без изменений, потому что не входят в таблицу алфавита."
    ];

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
            new CipherStep(1, "Зеркальная замена", "Заменяем каждую латинскую букву на симметричную букву из конца алфавита.", output)
        ]);
    }
}

public sealed class VigenereCipher : CipherAlgorithmBase
{
    public override string Code => "vigenere";
    public override string Name => "Шифр Виженера";
    public override string Category => "Полиалфавитная подстановка";
    public override string Era => "Ренессанс";
    public override int Difficulty => 3;
    public override IReadOnlyCollection<CipherParameter> Parameters => [new("key", "Ключ", "string", true, "Буквенный ключ")];
    public override IReadOnlyCollection<string> Limitations =>
    [
        "Ключ должен содержать хотя бы одну латинскую букву; остальные символы ключа игнорируются.",
        "Сдвигаются только латинские буквы A-Z; пробелы, цифры и пунктуация сохраняются и не продвигают ключ.",
        "Повторяющийся ключ уязвим к классическому криптоанализу при достаточном объёме текста."
    ];

    public override CipherExecutionResult Encrypt(string input, IReadOnlyDictionary<string, string> parameters) => Execute(input, parameters, encrypt: true);
    public override CipherExecutionResult Decrypt(string input, IReadOnlyDictionary<string, string> parameters) => Execute(input, parameters, encrypt: false);

    private static CipherExecutionResult Execute(string input, IReadOnlyDictionary<string, string> parameters, bool encrypt)
    {
        if (!parameters.TryGetValue("key", out var key) || string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Параметр 'key' обязателен.");
        }

        var normalizedKey = NormalizeLettersOnly(key);
        if (normalizedKey.Length == 0)
        {
            throw new InvalidOperationException("Параметр 'key' должен содержать хотя бы одну букву.");
        }

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
            new CipherStep(1, "Нормализация ключа", "Оставляем в ключе только буквы и приводим их к верхнему регистру.", normalizedKey),
            new CipherStep(2, encrypt ? "Шифрование по сдвигам ключа" : "Расшифровка обратными сдвигами", "Для каждой буквы применяем сдвиг, похожий на шифр Цезаря.", new string(output))
        ]);
    }
}

public sealed class RailFenceCipher : CipherAlgorithmBase
{
    public override string Code => "rail-fence";
    public override string Name => "Рельсовая изгородь";
    public override string Category => "Перестановка";
    public override string Era => "Классические шифры";
    public override int Difficulty => 2;
    public override IReadOnlyCollection<CipherParameter> Parameters => [new("rails", "Рельсы", "integer", true, "Количество рельсов")];
    public override IReadOnlyCollection<string> Limitations =>
    [
        "Параметр рельсов должен быть целым числом больше 1.",
        "Символы приводятся к верхнему регистру, но пробелы, пунктуация и цифры тоже участвуют в перестановке.",
        "Это только шифр перестановки: если известно число рельсов, восстановление обычно несложное."
    ];

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
            new CipherStep(1, "Запись зигзагом", "Распределяем символы по рельсам в порядке зигзага.", snapshot),
            new CipherStep(2, "Чтение по рельсам", "Соединяем рельсы, чтобы получить итоговый шифртекст.", output)
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
            new CipherStep(1, "Восстановление зигзага", "Считаем, сколько символов относится к каждому рельсу.", string.Join(",", railLengths)),
            new CipherStep(2, "Восстановление порядка", "Читаем символы обратно по траектории зигзага.", output)
        ]);
    }

    private static int ParseRails(IReadOnlyDictionary<string, string> parameters) =>
        parameters.TryGetValue("rails", out var value) && int.TryParse(value, out var rails) && rails >= 2
            ? rails
            : throw new InvalidOperationException("Параметр 'rails' должен быть целым числом больше 1.");

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
    public override string Name => "Столбцовая перестановка";
    public override string Category => "Перестановка";
    public override string Era => "Классические шифры";
    public override int Difficulty => 3;
    public override IReadOnlyCollection<CipherParameter> Parameters => [new("key", "Ключ", "string", true, "Ключевое слово для порядка столбцов")];
    public override IReadOnlyCollection<string> Limitations =>
    [
        "Ключ должен содержать хотя бы одну латинскую букву; повторяющиеся буквы упорядочиваются слева направо.",
        "Входной текст нормализуется до латинских букв, поэтому пробелы, пунктуация, цифры и нелатинские символы удаляются.",
        "Реализация использует неровную таблицу без добивки, поэтому расшифровка сохраняет настоящий завершающий X."
    ];

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
                if (cursor < normalized.Length)
                {
                    grid[row, column] = normalized[cursor++];
                }
            }
        }

        var order = GetColumnOrder(key);
        var buffer = new List<char>();
        foreach (var column in order)
        {
            for (var row = 0; row < rows; row++)
            {
                if (HasCell(row, column, columns, normalized.Length))
                {
                    buffer.Add(grid[row, column]);
                }
            }
        }

        var snapshot = BuildGridSnapshot(grid, rows, columns);
        var output = new string(buffer.ToArray());
        return new CipherExecutionResult(output, Array.Empty<string>(), [
            new CipherStep(1, "Заполнение таблицы", "Записываем нормализованный открытый текст в таблицу построчно.", snapshot),
            new CipherStep(2, "Чтение по ключу", "Читаем столбцы в порядке ключа, чтобы получить шифртекст.", output)
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
        var columnLengths = Enumerable.Range(0, columns)
            .Select(column => CountColumnCells(column, rows, columns, normalized.Length))
            .ToArray();
        var cursor = 0;

        foreach (var column in order)
        {
            for (var row = 0; row < columnLengths[column]; row++)
            {
                grid[row, column] = normalized[cursor++];
            }
        }

        var buffer = new List<char>();
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (HasCell(row, column, columns, normalized.Length))
                {
                    buffer.Add(grid[row, column]);
                }
            }
        }

        var output = new string(buffer.ToArray());
        return new CipherExecutionResult(output, Array.Empty<string>(), [
            new CipherStep(1, "Восстановление столбцов", "Заполняем каждый столбец согласно отсортированному порядку ключа.", BuildGridSnapshot(grid, rows, columns)),
            new CipherStep(2, "Чтение по строкам", "Читаем таблицу построчно, чтобы восстановить открытый текст.", output)
        ]);
    }

    private static string ParseKey(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("key", out var key) || string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Параметр 'key' обязателен.");
        }

        var normalizedKey = NormalizeLettersOnly(key);
        if (normalizedKey.Length == 0)
        {
            throw new InvalidOperationException("Параметр 'key' должен содержать хотя бы одну букву.");
        }

        return normalizedKey;
    }

    private static int[] GetColumnOrder(string key) =>
        key.Select((character, index) => new { character, index })
            .OrderBy(item => item.character)
            .ThenBy(item => item.index)
            .Select(item => item.index)
            .ToArray();

    private static int CountColumnCells(int column, int rows, int columns, int totalLength)
    {
        var count = 0;
        for (var row = 0; row < rows; row++)
        {
            if (HasCell(row, column, columns, totalLength))
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasCell(int row, int column, int columns, int totalLength) =>
        row * columns + column < totalLength;

    private static string BuildGridSnapshot(char[,] grid, int rows, int columns)
    {
        var lines = new List<string>();
        for (var row = 0; row < rows; row++)
        {
            var buffer = new char[columns];
            for (var column = 0; column < columns; column++)
            {
                buffer[column] = grid[row, column] == '\0' ? ' ' : grid[row, column];
            }

            lines.Add(new string(buffer).TrimEnd());
        }

        return string.Join(" | ", lines);
    }
}
