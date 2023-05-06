using System.ComponentModel;
using invoice.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace invoice.Commands;

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

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var dictionary = new InvoiceDictionary("Data Source=dict.db;");

        foreach (var (key, value) in GetKeyValues(settings.Values))
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                await dictionary.RemoveValue(key);
                continue;
            }

            await dictionary.SetValue(key, value);
        }

        return 0;
    }

    private static IEnumerable<KeyValuePair<string, string>> GetKeyValues(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            var parts = value.Split('=', 2);
            yield return new KeyValuePair<string, string>(parts[0], parts[1]);
        }
    }
}