using System.ComponentModel;
using invoice.Core;
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
        // Get file
        var inputFilePath = "./Assets/Invoice.html";

        var invoice = new Invoice(inputFilePath);

        var outputFilePath = settings.OutputFileName;
        if (string.IsNullOrEmpty(outputFilePath))
        {
            outputFilePath = "invoice.html";
        }

        var result = await invoice.GenerateInvoiceAsync(outputFilePath,
            replaceToken: (token) =>
            {
                // TODO add read from dictionary
                // Prompt name

                // prompt address

                // prompt state

                // Prompt country

                // prompt invoice to name

                // prompt invoice to address

                // prompt invoice to state

                // prompt amount

                // prompt payment instructions bank

                // prompt payment instructions routing

                // prompt payment instructions account
            });

        if (result == false)
        {
            AnsiConsole.WriteLine("Failed to write output file {0}", outputFilePath);
            return -1;
        }

        result = await PrintToPdfAsync(outputFilePath);
        if (result == false)
        {
            AnsiConsole.WriteLine("Failed to generate PDF", outputFilePath);
            return -1;
        }

        AnsiConsole.WriteLine("Generated PDF invoice");
        return result ? 0 : -1;
    }

    private async Task<bool> PrintToPdfAsync(string outputHtmlPath)
    {
        // TODO I promise
        return false;
    }
}