using Invoice.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Invoice.Commands;

internal sealed class VersionCommand : Command
{
    private readonly ApplicationVersion _applicationVersion;

    public VersionCommand(ApplicationVersion applicationVersion)
    {
        _applicationVersion = applicationVersion;
    }

    public override int Execute(CommandContext context)
    {
        // Version (Version, build number, codename)
        AnsiConsole.WriteLine($"{_applicationVersion.Version.ToString(3)} \"{_applicationVersion.Codename}\"");

        // Copyright
        AnsiConsole.WriteLine("Copyright (c) 2023 Exequiel Beker - MIT License");

        return 0;
    }
}