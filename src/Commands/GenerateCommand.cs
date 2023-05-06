using System.ComponentModel;
using invoice.Core;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Spectre.Console;
using Spectre.Console.Cli;

namespace invoice.Commands;

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

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var dictionary = new InvoiceDictionary("Data Source=dict.db;");

        // Get file
        var inputFilePath = Path.GetFullPath("./Assets/Invoice.html");

        var invoice = new Invoice(inputFilePath);

        var outputHtml = Path.GetTempFileName();
        outputHtml = Path.ChangeExtension(outputHtml, ".html");

        var result = await invoice.GenerateInvoiceAsync(outputHtml,
            replaceToken: async (token, ct) =>
            {
                var replacement = await dictionary.GetValue(token.Key, ct);
                switch (token)
                {
                    case IncrementToken inc:
                        if (replacement is null)
                        {
                            // If user not requested interactivity, or console does not support it, cancel and leave
                            if (true != settings.IsInteractive || AnsiConsole.Profile.Capabilities.Interactive)
                            {
                                token.Cancel();
                                return;
                            }

                            replacement = AnsiConsole.Prompt(new TextPrompt<int>($"[NUM] {inc.Key}: ")).ToString();
                            await dictionary.SetValue(token.Key, replacement, ct);
                        }

                        if (false == int.TryParse(replacement, out var increment))
                        {
                            token.Cancel();
                            return;
                        }

                        inc.Increment(++increment);

                        await dictionary.SetValue(token.Key, increment.ToString(), ct);
                        break;

                    case StringToken tk:
                        if (replacement is null)
                        {
                            // If user not requested interactivity, or console does not support it, cancel and leave
                            if (true != settings.IsInteractive || AnsiConsole.Profile.Capabilities.Interactive)
                            {
                                token.Cancel();
                                return;
                            }

                            replacement = AnsiConsole.Prompt(new TextPrompt<string>($"[ALPHANUM] {tk.Key}: "));
                            await dictionary.SetValue(token.Key, replacement, ct);
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
            outputFilePath = Path.Combine(Directory.GetCurrentDirectory(), "invoice.pdf");
        }
        outputFilePath = Path.GetFullPath(outputFilePath);



        result = await PrintToPdfAsync(outputHtml, outputFilePath);
        if (result == false)
        {
            AnsiConsole.WriteLine("Failed to generate PDF at '{0}'", outputFilePath);
            return -1;
        }

        AnsiConsole.WriteLine("Generated PDF invoice");
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