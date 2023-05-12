using System.ComponentModel;
using Invoice.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Invoice.Commands;

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

    private readonly InvoiceDictionary _dictionary;

    public DictionaryGetValueCommand(InvoiceDictionary dictionary)
    {
        _dictionary = dictionary;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {

        foreach (var key in settings.Keys.DistinctBy(s => s.ToLowerInvariant()))
        {
            var result = await _dictionary.GetValue(key);
            AnsiConsole.Write($"{key}: ");
            AnsiConsole.WriteLine(result ?? "null");
        }

        return 0;
    }
}