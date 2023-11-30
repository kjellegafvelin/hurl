using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;

class UrlCommand : Command<UrlCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("The URL to create requests for")]
        [CommandArgument(0, "[Url]")]
        public string? Url { get; set; }

        [Description("Number of requests to make per runner")]
        [CommandOption("-c|--count")]
        [DefaultValue(1)]
        public int Count { get; set; }

        [Description("Number of runners to use in parallel")]
        [CommandOption("-r|--runners")]
        [DefaultValue(1)]
        public int Runners { get; set; }
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (!Uri.TryCreate(settings.Url, UriKind.Absolute, out _))
        {
            return ValidationResult.Error("Invalid URL");
        }

        if (settings.Count < 1)
        {
            return ValidationResult.Error("Count must be greater than 0");
        } 

        if (settings.Runners < 1)
        {
            return ValidationResult.Error("Runners must be greater than 0");
        }

        return ValidationResult.Success();
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine($"[bold]Hurl[/] - {GitVersionInformation.SemVer}");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"[bold]Running {settings.Count} requests/runner to '{settings.Url}' using {settings.Runners} runner(s)[/]");

        using (var client = new System.Net.Http.HttpClient())
        {

            List<Task<RunResult>> tasks = [];

            AnsiConsole.Progress()
            .AutoClear(false)
            .Columns([
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new SpinnerColumn()
              ])
            .Start(ctx =>
            {
                var index = 1;
                for (int i = 0; i < settings.Runners; i++)
                {
                    var task = ctx.AddTask($"Runner #{i + 1}", maxValue: settings.Count);

                    tasks.Add(Task.Run(() => Run(index++.ToString(), client, settings, task)));
                }

                Task.WaitAll(tasks.ToArray());
            });
             
            var table = new Table();
            table.AddColumn("Runner"); 
            table.AddColumn("Avg. Time (ms)", col => col.RightAligned());
            table.AddColumn("Min. Time (ms)", col => col.RightAligned());
            table.AddColumn("Max. Time (ms)", col => col.RightAligned());
            table.AddColumn("OK requests", col => col.RightAligned());

            tasks.OrderBy(x => x.Result.Id).ToList().ForEach(t =>
            {
                var runResult = t.Result;
                    var okResults = runResult.UrlResults.Where(x => x.StatusCode == HttpStatusCode.OK).ToList();

                    table.AddRow($"#{t.Result.Id}",
                        ((long)okResults.Average(x => x.ElapsedMilliseconds)).ToString(),
                        okResults.Min(x => x.ElapsedMilliseconds).ToString(),
                        okResults.Max(x => x.ElapsedMilliseconds).ToString(),
                        $"{okResults.Count}/{runResult.UrlResults.Count}");
            });

            AnsiConsole.Write(table);
        }


        return 0;
    }

    private static async Task<RunResult> Run(string id, HttpClient client, Settings settings, ProgressTask task)
    {
        var runResult = new RunResult
        {
            Id = id
        };

        var sw = new Stopwatch();

        for (int j = 0; j < settings.Count; j++)
        {
            var urlResult = new UrlResult();
            sw.Start();
            try
            {
                var response = await client.GetAsync(settings.Url);
                urlResult.StatusCode = response.StatusCode;
            }
            catch (Exception)
            {
                urlResult.StatusCode = null;
            }
            
            task.Increment(1);

            sw.Stop();

            urlResult.ElapsedMilliseconds = sw.ElapsedMilliseconds;

            runResult.UrlResults.Add(urlResult);
        }

        return runResult;
    }
}
