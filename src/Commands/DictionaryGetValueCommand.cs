using System.ComponentModel;
using invoice.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace invoice.Commands;

internal sealed class DictionaryGetValueCommand : AsyncCommand<DictionaryGetValueCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        public Settings(string[] keys)
        {
            Keys = keys;
        }

        [Description("Key to be retrieved")]
        [CommandArgument(0, "[key]")]
        public string[] Keys { get; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var dictionary = new InvoiceDictionary("Data Source=dict.db;");

        foreach (var key in settings.Keys)
        {
            var result = await dictionary.GetValue(key);
            AnsiConsole.Write($"{key}: ");
            AnsiConsole.WriteLine(result ?? "null");
        }

        return 0;
    }
}