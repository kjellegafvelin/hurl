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

        [Description("Ramp up time in milliseconds between each runner")]
        [CommandOption("--ramp-up")]
        public int? RampUp { get; set; }
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (!Uri.TryCreate(settings.Url, UriKind.Absolute, out _))
        {
            return ValidationResult.Error("Missing or invalid URL");
        }

        if (settings.Count < 1)
        {
            return ValidationResult.Error("Count must be greater than 0");
        }

        if (settings.Runners < 1)
        {
            return ValidationResult.Error("Runners must be greater than 0");
        }

        if (settings?.RampUp < 1)
        {
               return ValidationResult.Error("Ramp up must be greater than 0");
        }

        if (settings?.RampUp > 0 && settings.Runners == 1)
        {
            return ValidationResult.Error("Ramp up requires more than 1 runner");
        }

        return ValidationResult.Success();
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine($"[bold]Hurl[/] - {GitVersionInformation.SemVer}");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"[bold]Running {settings.Count} requests/runner to '{settings.Url}' using {settings.Runners} runner(s)[/]");

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

                tasks.Add(Task.Run(() => Run(index++, settings, task)));
            }

            Task.WaitAll(tasks.ToArray());
        });

        long totalSize = tasks.SelectMany(x => x.Result.UrlResults).Sum(x => x.ContentLength);
        var totalElapsed = tasks.Sum(x => x.Result.ElapsedMilliseconds);
        var totalOk = tasks.SelectMany(x => x.Result.UrlResults).Count(x => x.StatusCode == HttpStatusCode.OK);
        var totalRequests = tasks.SelectMany(x => x.Result.UrlResults).Count();
        var totalAvg = tasks.SelectMany(x => x.Result.UrlResults).Average(x => x.ElapsedMilliseconds);
        var totalMin = tasks.SelectMany(x => x.Result.UrlResults).Min(x => x.ElapsedMilliseconds);
        var totalMax = tasks.SelectMany(x => x.Result.UrlResults).Max(x => x.ElapsedMilliseconds);
        var totalReqPerSec = totalOk / (totalElapsed / 1000d);

        var table = new Table()
        .AddColumn("Runner", col => col.Footer("Sum"))
        .AddColumn("Avg. Time (ms)", col => { col.RightAligned(); col.Footer(((long)totalAvg).ToString()); })
        .AddColumn("Min. Time (ms)", col => { col.RightAligned(); col.Footer(totalMin.ToString()); })
        .AddColumn("Max. Time (ms)", col => { col.RightAligned(); col.Footer(totalMax.ToString()); })
        .AddColumn("Req/s", col => { col.RightAligned(); col.Footer(totalReqPerSec.ToString("#.#", System.Globalization.NumberFormatInfo.InvariantInfo)); })
        .AddColumn("OK requests", col => { col.RightAligned(); col.Footer($"{totalOk}/{totalRequests}"); })
        .AddColumn("Downloaded", col => { col.RightAligned(); col.Footer(CalcSize(totalSize)); });

        tasks.OrderBy(x => x.Result.Id).ToList().ForEach(t =>
        {
            var runResult = t.Result;
            var okResults = runResult.UrlResults.Where(x => x.StatusCode == HttpStatusCode.OK).ToList();

            table.AddRow($"#{t.Result.Id}",
                    ((long)okResults.Average(x => x.ElapsedMilliseconds)).ToString(),
                    okResults.Min(x => x.ElapsedMilliseconds).ToString(),
                    okResults.Max(x => x.ElapsedMilliseconds).ToString(),
                    (okResults.Count / (runResult.ElapsedMilliseconds / 1000d)).ToString("#.#", System.Globalization.NumberFormatInfo.InvariantInfo),
                    $"{okResults.Count}/{runResult.UrlResults.Count}",
                    CalcSize(runResult.UrlResults.Sum(x => x.ContentLength)));
        });
        
        AnsiConsole.Write(table);

        return 0;
    }

    private static string CalcSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }
        else if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:0.##} KB";
        }
        else if (bytes < 1024 * 1024 * 1024)
        {
            return $"{bytes / (1024d * 1024d):0.##} MB";
        }
        else
        {
            return $"{bytes / (1024d * 1024d * 1024d):0.##} GB";
        }
    }   

    private static async Task<RunResult> Run(int id, Settings settings, ProgressTask task)
    {
        if (settings?.RampUp > 0 && settings.Runners > 1)
        {
            await Task.Delay((int)((settings.RampUp * id) - settings.RampUp) /** 1000*/);
        }

        var runResult = new RunResult
        {
            Id = id
        };

        using (var client = new System.Net.Http.HttpClient())
        {
            var swTotal = new Stopwatch();
            var swReq = new Stopwatch();

            swTotal.Start();

            for (int j = 0; j < settings.Count; j++)
            {
                var urlResult = new UrlResult();
                swReq.Start();
                try
                {
                    using (var response = await client.GetAsync(settings.Url))
                    {
                        urlResult.StatusCode = response.StatusCode;

                        var content = await response.Content.ReadAsByteArrayAsync();
                        urlResult.ContentLength = content.Length;
                        content = null;
                    }
                }
                catch (Exception)
                {
                    urlResult.StatusCode = null;
                }

                task.Increment(1);

                swReq.Stop();

                urlResult.ElapsedMilliseconds = swReq.ElapsedMilliseconds;

                runResult.UrlResults.Add(urlResult);
            }

            swTotal.Stop();
            runResult.ElapsedMilliseconds = swTotal.ElapsedMilliseconds;

            return runResult;
        }
    }
}
