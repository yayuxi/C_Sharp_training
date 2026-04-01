namespace træning;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

class Downloader
{
    static readonly HttpClient client = new HttpClient();

    public static readonly List<string> urls = new List<string>
    {
        "https://www.example.com",
        "https://www.wikipedia.org",
        "https://www.github.com",
        "https://www.microsoft.com",
        "https://www.stackoverflow.com"
    };

    public static async Task<string> DownloadAsync(string url)
    {
        HttpResponseMessage response = await client.GetAsync(url);
        return await response.Content.ReadAsStringAsync();
    }
}