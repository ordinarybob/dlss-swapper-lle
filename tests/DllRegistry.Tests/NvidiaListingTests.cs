using DLSS_Swapper.Data.NVIDIA;
using System.Text;

internal static class NvidiaListingTests
{
    public static async Task RunAsync()
    {
        const string root = "https://example.invalid";
        static string Page(bool more, string key, string marker = "") =>
            $"<ListBucketResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\"><IsTruncated>{more.ToString().ToLowerInvariant()}</IsTruncated><NextMarker>{marker}</NextMarker><Contents><Key>{key}</Key><Size>1</Size></Contents></ListBucketResult>";
        void Check(bool value)
        {
            if (!value) throw new Exception("NVIDIA pagination contract failed.");
        }
        var requests = new List<string>();
        async Task Download(string url, Stream output, CancellationToken token)
        {
            requests.Add(url);
            await output.WriteAsync(Encoding.UTF8.GetBytes(requests.Count == 1
                ? Page(true, "first/key", "next/key") : Page(false, "second/key")), token);
        }
        var result = await ListBucketResult.ReadAllAsync(root, Download, default);
        Check(result.Count == 2 && result[1].Key == "second/key");
        Check(requests.SequenceEqual(new[] { root, root + "?marker=next%2Fkey" }));
        requests.Clear();
        result = await ListBucketResult.ReadAllAsync(root, async (url, output, token) =>
        {
            requests.Add(url);
            await output.WriteAsync(Encoding.UTF8.GetBytes(Page(requests.Count == 1, "last/key")), token);
        }, default);
        Check(requests[1] == root + "?marker=last%2Fkey");
        try
        {
            await ListBucketResult.ReadAllAsync(root, async (_, output, token) =>
                await output.WriteAsync(Encoding.UTF8.GetBytes(Page(true, "same")), token), default);
            throw new Exception("Repeated marker was accepted.");
        }
        catch (InvalidDataException) { }
        using var cancel = new CancellationTokenSource();
        var calls = 0;
        try
        {
            await ListBucketResult.ReadAllAsync(root, async (_, output, token) =>
            {
                calls++;
                await output.WriteAsync(Encoding.UTF8.GetBytes(Page(true, "first")), token);
                cancel.Cancel();
            }, cancel.Token);
            throw new Exception("Cancellation was ignored.");
        }
        catch (OperationCanceledException) { Check(calls == 1); }
        Console.WriteLine("NVIDIA listing: multiple pages, escaped markers, last-key fallback, repeated-marker rejection and cancellation passed.");
    }
}
