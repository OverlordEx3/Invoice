// See https://aka.ms/new-console-template for more information

using invoice.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.AddCommand<GenerateCommand>("generate")
        .WithAlias("gen")
        .WithDescription("Generates a new invoice based on saved dictionary data. Prompts for missing values if --interactive is set.")
        .WithExample(new [] {"generate", "--interactive"});

    config.AddBranch("dictionary", configurator =>
    {
        configurator.SetDescription("Configures the replacement dictionary");

        configurator.AddCommand<DictionarySetValueCommand>("set")
            .WithDescription("Sets one or more values into dictionary");
        configurator.AddCommand<DictionaryGetValueCommand>("get")
            .WithDescription("Get value from dictionary");
        configurator.AddCommand<DictionaryListValuesCommand>("list")
            .WithDescription("List values in dictionary");
        configurator.AddCommand<DictionaryImportCommand>("import")
            .WithDescription("Imports data into dictionary");
    }); 

#if DEBUG
    config.PropagateExceptions();
    config.ValidateExamples();
#endif
});


return await app.RunAsync(args);