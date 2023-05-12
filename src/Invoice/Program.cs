// See https://aka.ms/new-console-template for more information

using System.IO.Abstractions;
using System.Reflection;
using Invoice;
using Invoice.Commands;
using Invoice.Core;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

const string codeName = "Helium";

var serviceCollection = new ServiceCollection();

var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 1);

serviceCollection.AddSingleton(_ => new ApplicationVersion(version, codeName));
serviceCollection.AddSingleton<IFileSystem>(_ => new FileSystem());
serviceCollection.AddSingleton<IDateTimeProvider>(_ => DateTimeProvider.Instance);

var registrar = new TypeRegistrar(serviceCollection);

var app = new CommandApp(registrar);

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

    config.AddCommand<VersionCommand>("version").WithDescription("Get version");

#if DEBUG
    config.PropagateExceptions();
    config.ValidateExamples();
#endif
});


return await app.RunAsync(args);