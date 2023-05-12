using System.ComponentModel;
using Invoice.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Invoice.Commands;

internal sealed class DictionarySetValueCommand : AsyncCommand<DictionarySetValueCommand.Settings>
{
    public class Settings : CommandSettings
    {
        public Settings(string[] values)
        {
            Values = values;
        }

        [Description("Values to be set")]
        [CommandArgument(0, "[values]")]
        public string[] Values { get; }

        public override ValidationResult Validate()
        {
            if (Values.Length == 0)
            {
                return ValidationResult.Error("No values provided");
            }

            // Validate every value is a key=value pair
            foreach (var value in Values)
            {
                if (value.Count(c => c == '=') != 1)
                {
                    return ValidationResult.Error($"Invalid value {value}");
                }
            }

            return ValidationResult.Success();
        }
    }

    private readonly InvoiceDictionary _dictionary;

    public DictionarySetValueCommand(InvoiceDictionary dictionary)
    {
        _dictionary = dictionary;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        foreach (var (key, value) in GetKeyValues(settings.Values))
        {
            // Remove empty values
            if (string.IsNullOrWhiteSpace(value))
            {
                await _dictionary.RemoveValue(key);
                continue;
            }

            await _dictionary.SetValue(key, value);
        }

        return 0;
    }

    /// <summary>
    /// Splits every value in <paramref name="values"/> to extract every key with their upsert value.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string>> GetKeyValues(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            // We consider only the first two parts
            var parts = value.Split('=', 2);
            yield return new KeyValuePair<string, string>(parts[0], parts[1]);
        }
    }
}