using System.Net;
using DlssSwapper.Linux.Cli.Core;

namespace DlssSwapper.Linux.Tests;

internal static class NgxModelServerTests
{
    public static async Task RunAsync()
    {
        const string prefix = "d6e9b45e-d4f6-4a84-a460-bf61decae3e8/";
        var calls = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            calls++;
            if (calls == 2 && request.RequestUri!.Query != "?marker=next") throw new Exception("Missing pagination marker.");
            return new(HttpStatusCode.OK) { Content = new StringContent(calls == 1
                ? $"<ListBucketResult xmlns='http://s3.amazonaws.com/doc/2006-03-01/'><IsTruncated>true</IsTruncated><NextMarker>next</NextMarker><Contents><Key>{prefix}dlss/versions/66051/files/160_E658700.bin</Key><Size>42</Size></Contents><Contents><Key>unrelated.bin</Key><Size>42</Size></Contents></ListBucketResult>"
                : $"<ListBucketResult><IsTruncated>false</IsTruncated><Contents><Key>{prefix}dlssd/versions/131844/files/160_E658700.bin</Key><Size>84</Size></Contents></ListBucketResult>") };
        }));
        var models = await NgxModelServer.ListAsync(http, default);
        if (calls != 2 || models.Count != 2 || models[0].Version != "1.2.3"
            || !models.Any(model => model.Type == DllType.DlssRayReconstruction && model.Version == "2.3.4"))
            throw new Exception("Model filtering, version decoding or pagination failed.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await NgxModelServer.ListAsync(http, cancelled.Token); throw new Exception("Cancellation ignored."); }
        catch (OperationCanceledException) { }
        using var invalid = new HttpClient(new Handler(_ => new(HttpStatusCode.OK)
        { Content = new StringContent("<!DOCTYPE x [<!ENTITY x 'bad'>]><ListBucketResult>&x;</ListBucketResult>") }));
        try { await NgxModelServer.ListAsync(invalid, default); throw new Exception("DTD accepted."); }
        catch (System.Xml.XmlException) { }
        using var failure = new HttpClient(new Handler(_ => new(HttpStatusCode.ServiceUnavailable)));
        try { await NgxModelServer.ListAsync(failure, default); throw new Exception("HTTP failure accepted."); }
        catch (HttpRequestException) { }
        using var malformed = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent("<wrong />") }));
        var language = new Translations("en-US");
        ((Dictionary<string, string>)language.Values)["Linux_NgxInvalidList"] = "INVALID MODEL LIST";
        try { await NgxModelServer.ListAsync(malformed, default, language); throw new Exception("Invalid list accepted."); }
        catch (IOException ex) when (ex.Message == "INVALID MODEL LIST") { }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return Task.FromResult(respond(request)); }
    }
}
