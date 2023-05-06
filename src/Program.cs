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
#if DEBUG
    config.PropagateExceptions();
    config.ValidateExamples();
#endif
});


return await app.RunAsync(args);