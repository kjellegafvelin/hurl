using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<UrlCommand>();

app.WithDescription("Generates a burst of HTTP requests to the specified URL.")
   .Configure(config =>
   {
       config.SetApplicationVersion(GitVersionInformation.FullSemVer);
       config.SetApplicationName("hurl");
   });

return app.Run(args);
