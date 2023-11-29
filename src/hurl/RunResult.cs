// See https://aka.ms/new-console-template for more information
class RunResult
{
    public List<UrlResult> UrlResults { get; set; } = new List<UrlResult>();
    public string Id { get; internal set; } = string.Empty;
}
