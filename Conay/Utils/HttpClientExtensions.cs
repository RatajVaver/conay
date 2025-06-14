using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Conay.Utils;

public static class HttpClientExtensions
{
    public static async Task DownloadAsync(this HttpClient client, string requestUri, Stream destination,
        IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response =
            await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        long? contentLength = response.Content.Headers.ContentLength;

        await using Stream download = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (progress == null || !contentLength.HasValue)
        {
            await download.CopyToAsync(destination, cancellationToken);
            return;
        }

        var relativeProgress =
            new Progress<long>(totalBytes => progress.Report((float)totalBytes / contentLength.Value));

        await download.CopyToAsync(destination, 81920, relativeProgress, cancellationToken);
        progress.Report(1);
    }
}