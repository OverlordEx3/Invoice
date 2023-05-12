using System.ComponentModel;
using System.IO.Abstractions;
using Invoice.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Invoice.Commands;

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


    private readonly IFileSystem _fileSystem;
    private readonly InvoiceDictionary _dictionary;

    public DictionaryImportCommand(IFileSystem fileSystem, InvoiceDictionary dictionary)
    {
        _fileSystem = fileSystem;
        _dictionary = dictionary;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var fileName = _fileSystem.Path.GetFullPath(settings.FileName);
        if (false == _fileSystem.File.Exists(fileName))
        {
            AnsiConsole.WriteLine($"File '{fileName}' not found");
            return -1;
        }

        var importedDictionary = new Dictionary<string, string>();
        var streamReader = _fileSystem.File.OpenText(fileName);
        using (streamReader)
        {
            // Read every file line
            var line = await streamReader.ReadLineAsync();
            while (line is not null)
            {
                // We consider a valid line all those who have a equals separator.
                if (false == IsValidLine(line))
                {
                    AnsiConsole.WriteLine("Invalid file format");
                    return -1;
                }

                var (key, value) = ParseLine(line);
                if (string.IsNullOrWhiteSpace(key))
                {
                    AnsiConsole.WriteLine("Invalid key format");
                    return -1;
                }

                importedDictionary.Add(key, string.IsNullOrWhiteSpace(value) ? string.Empty : value);

                line = await streamReader.ReadLineAsync();
            }
        }

        int imported = 0, failed = 0;
        foreach (var (key, value) in importedDictionary)
        {
            bool success;
            if (string.IsNullOrWhiteSpace(value))
            {
                success = await _dictionary.RemoveValue(key);
                if (success)
                {
                    imported++;
                }
                else
                {
                    failed++;
                }
            }
            else
            {
                success = await _dictionary.SetValue(key, value);
                if (success)
                {
                    imported++;
                }
                else
                {
                    failed++;
                }
            }
        }

        AnsiConsole.WriteLine("Imported {0} keys ({1} failed).", imported, failed);

        return 0;
    }

    private static bool IsValidLine(string line) => line.Count(c => c == '=') == 1;

    private static KeyValuePair<string, string> ParseLine(string line)
    {
        var parts = line.Split('=', 2);
        return new KeyValuePair<string, string>(parts[0], parts[1]);
    }
}