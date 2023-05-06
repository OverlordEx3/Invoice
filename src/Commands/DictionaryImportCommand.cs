using System.ComponentModel;
using invoice.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace invoice.Commands;

internal sealed class DictionaryImportCommand : AsyncCommand<DictionaryImportCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        public Settings(string fileName)
        {
            FileName = fileName;
        }

        [CommandArgument(0, "[filename]")]
        [Description("File to be imported")]
        public string FileName { get; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var fileName = Path.GetFullPath(settings.FileName);
        if (false == File.Exists(fileName))
        {
            AnsiConsole.WriteLine($"File '{fileName}' not found");
            return -1;
        }

        var streamReader = File.OpenText(fileName);
        using (streamReader)
        {
            var dictionary = new InvoiceDictionary("Data Source=dict.db;");

            var line = await streamReader.ReadLineAsync();
            while (line is not null)
            {
                if (false == IsValidLine(line))
                {
                    AnsiConsole.WriteLine("Invalid line, skipping");
                    line = await streamReader.ReadLineAsync();
                    continue;
                }

                var (key, value) = ParseLine(line);
                if (string.IsNullOrWhiteSpace(key))
                {
                    AnsiConsole.WriteLine("Invalid key, skipping");
                    line = await streamReader.ReadLineAsync();
                    continue;
                }

                bool result;
                if (string.IsNullOrWhiteSpace(value))
                {
                    result = await dictionary.RemoveValue(key);
                    if (result)
                    {
                        AnsiConsole.WriteLine($"{key}: unset");
                    }
                    else
                    {
                        AnsiConsole.WriteLine($"{key}: failed to unset");
                    }
                }
                else
                {
                    result = await dictionary.SetValue(key, value);
                    if (result)
                    {
                        AnsiConsole.WriteLine($"{key}: imported");
                    }
                    else
                    {
                        AnsiConsole.WriteLine($"{key}: failed to import");
                    }
                }

                line = await streamReader.ReadLineAsync();
            }
        }

        return 0;
    }

    private static bool IsValidLine(string line) => line.Count(c => c == '=') == 1;

    private static KeyValuePair<string, string> ParseLine(string line)
    {
        var parts = line.Split('=', 2);
        return new KeyValuePair<string, string>(parts[0], parts[1]);
    }
}