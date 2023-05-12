using System.ComponentModel;
using System.IO.Abstractions;
using Invoice.Core;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Invoice.Commands;

internal sealed class GenerateCommand : AsyncCommand<GenerateCommand.Settings>
{
    internal sealed class Settings : CommandSettings
    {
        [Description("Changes output file name. Defaults to \"invoice.pdf\"")]
        [CommandOption("-o|--output")]
        public string? OutputFileName { get; init; } = null;

        [Description("Prompts missing values")]
        [CommandOption("-i|--interactive")]
        public bool? IsInteractive { get; init; } = false;
    }

    private readonly IFileSystem _fileSystem;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly InvoiceDictionary _dictionary;

    public GenerateCommand(IFileSystem fileSystem, IDateTimeProvider dateTimeProvider, InvoiceDictionary dictionary)
    {
        _fileSystem = fileSystem;
        _dateTimeProvider = dateTimeProvider;
        _dictionary = dictionary;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        // Get file
        var inputFilePath = _fileSystem.Path.GetFullPath("./Assets/Invoice.html");

        var invoice = new Core.Invoice(inputFilePath, _fileSystem.File, _dateTimeProvider);

        var outputHtml = _fileSystem.Path.GetTempFileName();
        // Force the file to have .html extension to allow invoice generation to read the file // TODO I should change this to streams
        outputHtml = _fileSystem.Path.ChangeExtension(outputHtml, ".html");

        var result = await invoice.GenerateInvoiceAsync(outputHtml,
            replaceToken: async (token, ct) =>
            {
                var replacement = await _dictionary.GetValue(token.Key, ct);
                switch (token)
                {
                    case IncrementToken inc:
                        if (replacement is null)
                        {
                            // If user not requested interactivity, or console does not support it, cancel and leave
                            if (true != settings.IsInteractive || AnsiConsole.Profile.Capabilities.Interactive)
                            {
                                AnsiConsole.WriteLine("Token '{0}' not found.", token.Key);
                                token.Cancel();
                                return;
                            }

                            replacement = AnsiConsole.Prompt(new TextPrompt<int>($"[NUM] {inc.Key}: ")).ToString();
                            await _dictionary.SetValue(token.Key, replacement, ct);
                        }

                        if (false == int.TryParse(replacement, out var increment))
                        {
                            AnsiConsole.WriteLine("Token '{0}' has an invalid value format and cannot be parsed.", token.Key);
                            token.Cancel();
                            return;
                        }

                        inc.Increment(++increment);

                        await _dictionary.SetValue(token.Key, increment.ToString(), ct);
                        break;

                    case StringToken tk:
                        if (replacement is null)
                        {
                            // If user not requested interactivity, or console does not support it, cancel and leave
                            if (true != settings.IsInteractive || AnsiConsole.Profile.Capabilities.Interactive)
                            {
                                AnsiConsole.WriteLine("Token '{0}' not found", token.Key);
                                token.Cancel();
                                return;
                            }

                            replacement = AnsiConsole.Prompt(new TextPrompt<string>($"[ALPHANUM] {tk.Key}: "));
                            await _dictionary.SetValue(token.Key, replacement, ct);
                        }

                        tk.SetValue(replacement);
                        break;

                    default:
                        token.Cancel();
                        return;
                }
            });

        if (result == false)
        {
            AnsiConsole.WriteLine("Failed to write output file {0}", outputHtml);
            return -1;
        }

        var outputFilePath = settings.OutputFileName;
        if (string.IsNullOrEmpty(outputFilePath))
        {
            outputFilePath = _fileSystem.Path.Combine(_fileSystem.Directory.GetCurrentDirectory(), "invoice.pdf");
        }

        outputFilePath = _fileSystem.Path.GetFullPath(outputFilePath);
        // If output path given is not empty, but is not a file. We got nothing to do here :)
        if (_fileSystem.Path.EndsInDirectorySeparator(outputFilePath))
        {
            AnsiConsole.WriteLine("Output path {0} is not a valid file", outputFilePath);
            return -1;
        }

        //Validate extension. We expect pdf files for printing.
        if (_fileSystem.Path.GetExtension(outputFilePath).ToLowerInvariant() != ".pdf")
        {
            AnsiConsole.WriteLine("Output path {0} is not a pdf file", outputFilePath);
            return -1;
        }

        result = await PrintToPdfAsync(outputHtml, outputFilePath);
        if (result == false)
        {
            AnsiConsole.WriteLine("Failed to generate PDF at '{0}'", outputFilePath);
            return -1;
        }

        return 0;
    }

    private static Task<bool> PrintToPdfAsync(string outputHtmlPath, string outputPdf)
    {
        var uri = new Uri(outputHtmlPath);

        var driverOptions = new ChromeOptions
        {
            PageLoadStrategy = PageLoadStrategy.Eager,
        };
        // In headless mode, PDF writing is enabled by default
        driverOptions.AddArgument("--headless=new");
        var driver = new ChromeDriver(driverOptions);

        using (driver)
        {
            driver.Navigate().GoToUrl(uri);
            
            var printOptions = new PrintOptions
            {
                Orientation = PrintOrientation.Portrait,
                ScaleFactor = 1.0,
                PageDimensions =
                {
                    HeightInInches = 297 / 25.4,
                    WidthInInches = 210 / 25.4,
                },
                OutputBackgroundImages = true,
            };

            var printDocument = driver.Print(printOptions);
            printDocument.SaveAsFile(outputPdf);
            driver.Close();
        }

        return Task.FromResult(true);
    }
}