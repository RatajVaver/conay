using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Conay.Utils;

public static class Web
{
    public static async Task<string> Get(string url, TimeSpan? timeout = null)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Add("User-Agent", Meta.GetUserAgent());
        client.Timeout = timeout ?? TimeSpan.FromSeconds(5);
        return await client.GetStringAsync(url);
    }

    public static async Task<string> Post(string url, HttpContent? postData, TimeSpan? timeout = null)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Add("User-Agent", Meta.GetUserAgent());
        client.Timeout = timeout ?? TimeSpan.FromSeconds(5);
        HttpResponseMessage response = await client.PostAsync(url, postData);
        return await response.Content.ReadAsStringAsync();
    }

    public static async Task<bool> Download(string url, string filePath, IProgress<float>? progress = null)
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
            Console.WriteLine(ex);
            return false;
        }

        return true;
    }
}