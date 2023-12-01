class RunResult
{
    public int Id { get; internal set; }

    public List<UrlResult> UrlResults { get; set; } = new List<UrlResult>();

    public long ElapsedMilliseconds { get; set; }
}
