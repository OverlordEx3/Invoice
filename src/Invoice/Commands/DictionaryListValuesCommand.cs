using System.ComponentModel;
using Invoice.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Invoice.Commands;

internal sealed class DictionaryListValuesCommand : AsyncCommand<DictionaryListValuesCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        public Settings(int? offset, int? limit)
        {
            Offset = offset;
            Limit = limit;
        }

        [CommandOption("--offset")]
        [DefaultValue(0)]
        public int? Offset { get; }

        [CommandOption("--limit")]
        [DefaultValue(10)]
        public int? Limit { get; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.Offset < 0)
        {
            AnsiConsole.WriteLine("Invalid offset value");
            return -1;
        }

        if (settings.Limit < 0)
        {
            AnsiConsole.WriteLine("Invalid limit value");
            return -1;
        }

        var dictionary = new InvoiceDictionary("Data Source=dict.db;");
        var result = (await dictionary.GetValues(settings.Offset ?? 0, settings.Limit ?? 10)).ToList();

        AnsiConsole.WriteLine("Count: {0}", result.Count);
        AnsiConsole.WriteLine("----------------------");
        foreach (var (key, value) in result)
        {
            AnsiConsole.Write($"{key}: ");
            AnsiConsole.WriteLine(value ?? "null");
        }

        return 0;
    }
}