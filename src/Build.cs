using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

const string settingsFilePath = "settings.json";

var settingsContent = File.ReadAllText(settingsFilePath);
var settings =
    JsonSerializer.Deserialize(settingsContent, ThemeBuilder.SerializerContext.Default.Settings)
    ?? throw new InvalidOperationException($"Failed to parse '{settingsFilePath}'");

var paletteContent = File.ReadAllText(settings.PaletteFilePath);
var paletteLookup =
    JsonSerializer.Deserialize(
        paletteContent,
        ThemeBuilder.SerializerContext.Default.DictionaryStringString
    ) ?? throw new InvalidOperationException($"Failed to parse '{settings.PaletteFilePath}'");

var paletteSpanLookup = paletteLookup.GetAlternateLookup<ReadOnlySpan<char>>();

if (!Directory.Exists(settings.OutputDirectory))
    _ = Directory.CreateDirectory(settings.OutputDirectory);
foreach (var editorCode in ThemeBuilder.EditorCodes)
{
    var templateFilePath = string.Format(settings.TemplateFilePathFormat, editorCode);
    var templateContent = File.ReadAllText(templateFilePath);

    var outputFilePath = string.Format(settings.OutputFilePathFormat, settings.OutputDirectory, editorCode);
    using var themeFile = File.Create(outputFilePath);
    using var writer = new StreamWriter(themeFile);
    var matches = ThemeBuilder.TemplateRegex().EnumerateMatches(templateContent);

    var currentIndex = 0;
    foreach (var match in matches)
    {
        var precedingContent = templateContent.AsSpan(currentIndex..match.Index);
        writer.Write(precedingContent);
        currentIndex = match.Index + match.Length;

        var keyStartIndex = match.Index + ThemeBuilder.TemplateDelimiterLength;
        var keyEndIndex = keyStartIndex + match.Length - (ThemeBuilder.TemplateDelimiterLength * 2);
        var key = templateContent.AsSpan(keyStartIndex..keyEndIndex);
        if (!paletteSpanLookup.TryGetValue(key, out var value))
            throw new KeyNotFoundException($"Key '{key}' is missing.");

        writer.Write(value);
    }

    if (currentIndex < templateContent.Length)
        writer.Write(templateContent.AsSpan(currentIndex..));

    Console.WriteLine($"Successfully created theme file: {outputFilePath}");
}

internal static partial class ThemeBuilder
{
    public const int TemplateDelimiterLength = 2;
    public static string[] EditorCodes = { "vscode", "zed" };

    [GeneratedRegex(@"{{\w+}}")]
    public static partial Regex TemplateRegex();

    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(Settings))]
    public partial class SerializerContext : JsonSerializerContext;

    public sealed class Settings
    {
        public required string[] EditorCodes { get; init; }
        public required string OutputDirectory { get; init; }
        public required string OutputFilePathFormat { get; init; }
        public required string PaletteFilePath { get; init; }
        public required string TemplateFilePathFormat { get; init; }
    }
}
