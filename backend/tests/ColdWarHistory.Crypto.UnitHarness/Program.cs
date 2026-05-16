using ColdWarHistory.Crypto.Domain;

var tests = new CryptoAlgorithmTests();
tests.RunAll();

Console.WriteLine("Crypto unit harness completed successfully.");

internal sealed class CryptoAlgorithmTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyParameters = new Dictionary<string, string>();

    private readonly IReadOnlyCollection<ICipherAlgorithm> _algorithms =
    [
        new CaesarCipher(),
        new AtbashCipher(),
        new VigenereCipher(),
        new RailFenceCipher(),
        new ColumnarTranspositionCipher()
    ];

    public void RunAll()
    {
        CaesarUsesKnownVector();
        AtbashUsesKnownVector();
        VigenereUsesKnownVectorAndSkipsNonLetters();
        RailFenceUsesKnownVector();
        ColumnarUsesKnownVectorWithoutPaddingLoss();
        EveryCipherRoundTripsRepresentativeText();
        InvalidParametersFailFast();
        CatalogMetadataIsUserFacing();
    }

    private static void CaesarUsesKnownVector()
    {
        var cipher = new CaesarCipher();
        var parameters = new Dictionary<string, string> { ["shift"] = "3" };

        var encrypted = cipher.Encrypt("ATTACK AT DAWN!", parameters).Output;
        EnsureEqual("DWWDFN DW GDZQ!", encrypted, "Caesar encrypt known vector.");

        var decrypted = cipher.Decrypt(encrypted, parameters).Output;
        EnsureEqual("ATTACK AT DAWN!", decrypted, "Caesar decrypt known vector.");
    }

    private static void AtbashUsesKnownVector()
    {
        var cipher = new AtbashCipher();

        var encrypted = cipher.Encrypt("HELLO WORLD", EmptyParameters).Output;
        EnsureEqual("SVOOL DLIOW", encrypted, "Atbash encrypt known vector.");

        var decrypted = cipher.Decrypt(encrypted, EmptyParameters).Output;
        EnsureEqual("HELLO WORLD", decrypted, "Atbash decrypt known vector.");
    }

    private static void VigenereUsesKnownVectorAndSkipsNonLetters()
    {
        var cipher = new VigenereCipher();
        var parameters = new Dictionary<string, string> { ["key"] = "LEMON" };

        var encrypted = cipher.Encrypt("ATTACK AT DAWN!", parameters).Output;
        EnsureEqual("LXFOPV EF RNHR!", encrypted, "Vigenere encrypt known vector.");

        var decrypted = cipher.Decrypt(encrypted, parameters).Output;
        EnsureEqual("ATTACK AT DAWN!", decrypted, "Vigenere decrypt known vector.");
    }

    private static void RailFenceUsesKnownVector()
    {
        var cipher = new RailFenceCipher();
        var parameters = new Dictionary<string, string> { ["rails"] = "3" };

        var encrypted = cipher.Encrypt("WEAREDISCOVEREDFLEEATONCE", parameters).Output;
        EnsureEqual("WECRLTEERDSOEEFEAOCAIVDEN", encrypted, "Rail Fence encrypt known vector.");

        var decrypted = cipher.Decrypt(encrypted, parameters).Output;
        EnsureEqual("WEAREDISCOVEREDFLEEATONCE", decrypted, "Rail Fence decrypt known vector.");
    }

    private static void ColumnarUsesKnownVectorWithoutPaddingLoss()
    {
        var cipher = new ColumnarTranspositionCipher();
        var parameters = new Dictionary<string, string> { ["key"] = "ZEBRAS" };

        var encrypted = cipher.Encrypt("WEAREDISCOVEREDFLEEATONCE", parameters).Output;
        EnsureEqual("EVLNACDTESEAROFODEECWIREE", encrypted, "Columnar encrypt known vector.");

        var decrypted = cipher.Decrypt(encrypted, parameters).Output;
        EnsureEqual("WEAREDISCOVEREDFLEEATONCE", decrypted, "Columnar decrypt known vector.");
    }

    private void EveryCipherRoundTripsRepresentativeText()
    {
        foreach (var algorithm in _algorithms)
        {
            var parameters = ParametersFor(algorithm.Code);
            var input = algorithm.Code == "columnar"
                ? "MEET AT CHECKPOINT X"
                : "Meet at checkpoint X, 21:00.";

            var encrypted = algorithm.Encrypt(input, parameters).Output;
            var decrypted = algorithm.Decrypt(encrypted, parameters).Output;
            var expected = algorithm.Code == "columnar"
                ? "MEETATCHECKPOINTX"
                : input.ToUpperInvariant();

            EnsureEqual(expected, decrypted, $"{algorithm.Name} round-trip.");
        }
    }

    private static void InvalidParametersFailFast()
    {
        EnsureThrows(() => new CaesarCipher().Encrypt("HELLO", EmptyParameters), "Caesar requires shift.");
        EnsureThrows(() => new VigenereCipher().Encrypt("HELLO", new Dictionary<string, string> { ["key"] = "123" }), "Vigenere rejects keys without letters.");
        EnsureThrows(() => new RailFenceCipher().Encrypt("HELLO", new Dictionary<string, string> { ["rails"] = "1" }), "Rail Fence requires at least two rails.");
        EnsureThrows(() => new ColumnarTranspositionCipher().Encrypt("HELLO", new Dictionary<string, string> { ["key"] = "123" }), "Columnar rejects keys without letters.");
    }

    private void CatalogMetadataIsUserFacing()
    {
        var forbiddenFragments = new[]
        {
            "Works with",
            "Letter shift",
            "Alphabetic key",
            "Number of rails",
            "Column ordering",
            "Substitution",
            "Transposition"
        };

        foreach (var algorithm in _algorithms)
        {
            Ensure(!string.IsNullOrWhiteSpace(algorithm.Name), $"{algorithm.Code} should expose a name.");
            Ensure(!string.IsNullOrWhiteSpace(algorithm.Category), $"{algorithm.Code} should expose a category.");
            Ensure(!string.IsNullOrWhiteSpace(algorithm.Era), $"{algorithm.Code} should expose an era.");
            Ensure(algorithm.Limitations.Count > 0, $"{algorithm.Code} should document limitations.");

            var metadata = new[]
            {
                algorithm.Name,
                algorithm.Category,
                algorithm.Era
            }
            .Concat(algorithm.Limitations)
            .Concat(algorithm.Parameters.SelectMany(parameter => new[] { parameter.Label, parameter.Description ?? string.Empty }));

            foreach (var value in metadata)
            {
                Ensure(!forbiddenFragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase)), $"{algorithm.Code} metadata should not expose English draft text.");
            }
        }
    }

    private static IReadOnlyDictionary<string, string> ParametersFor(string cipherCode) =>
        cipherCode switch
        {
            "caesar" => new Dictionary<string, string> { ["shift"] = "7" },
            "vigenere" => new Dictionary<string, string> { ["key"] = "ORBIT" },
            "rail-fence" => new Dictionary<string, string> { ["rails"] = "3" },
            "columnar" => new Dictionary<string, string> { ["key"] = "RADIO" },
            _ => EmptyParameters
        };

    private static void EnsureEqual(string expected, string actual, string scenario)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{scenario} Expected '{expected}', got '{actual}'.");
        }
    }

    private static void Ensure(bool condition, string scenario)
    {
        if (!condition)
        {
            throw new InvalidOperationException(scenario);
        }
    }

    private static void EnsureThrows(Action action, string scenario)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException($"{scenario} Expected InvalidOperationException.");
    }
}
