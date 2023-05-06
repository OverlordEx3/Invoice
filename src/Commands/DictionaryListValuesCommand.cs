using System.ComponentModel;
using invoice.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace invoice.Commands;

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
        var dictionary = new InvoiceDictionary("Data Source=dict.db;");
        var result = await dictionary.GetValues(settings.Offset ?? 0, settings.Limit ?? 10);

        foreach (var (key, value) in result)
        {
            AnsiConsole.Write($"{key}: ");
            AnsiConsole.WriteLine(value ?? "null");
        }

        return 0;
    }
}