namespace Jonnxor.Admin.Tests;

/// <summary>
/// Scripted <see cref="HttpMessageHandler"/> double: every request is recorded on
/// <see cref="Requests"/> and answered by the responder the test supplies (which may
/// throw to simulate network failure or timeout).
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => _respond = respond;

    /// <summary>Every request received, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_respond(request));
    }

    /// <summary>Convenience factory for a fixed status + JSON body responder.</summary>
    public static FakeHttpMessageHandler Json(System.Net.HttpStatusCode status, string body)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        });
}
