// See https://aka.ms/new-console-template for more information
using System.Net;

class UrlResult
{
    public HttpStatusCode? StatusCode { get; set; }
    public long ElapsedMilliseconds { get; set; }
}