using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Conay.Utils;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class HttpService(ILogger<HttpService> logger)
{
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        HttpClient client = new() { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.Add("User-Agent", Meta.GetUserAgent());
        return client;
    }

    public async Task<string> Get(string url, TimeSpan? timeout = null)
    {
        using CancellationTokenSource cts = new(timeout ?? TimeSpan.FromSeconds(10));
        try
        {
            return await Client.GetStringAsync(url, cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read data from: {Url}", url);
            return string.Empty;
        }
    }

    public async Task<string> Post(string url, HttpContent? postData, TimeSpan? timeout = null)
    {
        using CancellationTokenSource cts = new(timeout ?? TimeSpan.FromSeconds(10));
        try
        {
            HttpResponseMessage response = await Client.PostAsync(url, postData, cts.Token);
            return await response.Content.ReadAsStringAsync(cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to post data to: {Url}", url);
            return string.Empty;
        }
    }

    public async Task<bool> Download(string url, string filePath, IProgress<float>? progress = null)
    {
        using CancellationTokenSource cts = new(TimeSpan.FromMinutes(5));
        try
        {
            await using FileStream file = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await Client.DownloadAsync(url, file, progress, cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download: {Url}", url);
            return false;
        }

        return true;
    }
}