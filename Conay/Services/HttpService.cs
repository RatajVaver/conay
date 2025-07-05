using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Conay.Utils;
using Microsoft.Extensions.Logging;

namespace Conay.Services;

public class HttpService
{
    private readonly ILogger<HttpService> _logger;
    private readonly SocketsHttpHandler _handler;

    public HttpService(ILogger<HttpService> logger)
    {
        _logger = logger;

        _handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                IPHostEntry entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork,
                    cancellationToken);
                Socket socket = new(SocketType.Stream, ProtocolType.Tcp);
                socket.NoDelay = true;

                try
                {
                    await socket.ConnectAsync(entry.AddressList, context.DnsEndPoint.Port, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
    }

    public async Task<string> Get(string url, TimeSpan? timeout = null)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Add("User-Agent", Meta.GetUserAgent());
        client.Timeout = timeout ?? TimeSpan.FromSeconds(10);

        try
        {
            return await client.GetStringAsync(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read data from: {Url}", url);
            return string.Empty;
        }
    }

    public async Task<string> Post(string url, HttpContent? postData, TimeSpan? timeout = null)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Add("User-Agent", Meta.GetUserAgent());
        client.Timeout = timeout ?? TimeSpan.FromSeconds(10);

        try
        {
            HttpResponseMessage response = await client.PostAsync(url, postData);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post data to: {Url}", url);
            return string.Empty;
        }
    }

    public async Task<bool> Download(string url, string filePath, IProgress<float>? progress = null)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Add("User-Agent", Meta.GetUserAgent());
        client.Timeout = TimeSpan.FromMinutes(5);

        try
        {
            using (FileStream file = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await client.DownloadAsync(url, file, progress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download: {Url}", url);
            return false;
        }

        return true;
    }
}