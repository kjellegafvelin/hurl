using Spectre.Console;
using Spectre.Console.Cli;

AnsiConsole.MarkupLine("[bold]Hurl[/] - A simple HTTP load testing tool");
AnsiConsole.MarkupLine("Version 1.0.0");
AnsiConsole.WriteLine();

var app = new CommandApp<UrlCommand>();

return app.Run(args);
