using System.IO;
using Microsoft.Web.WebView2.Core;

namespace Peeklet.Services;

public static class WebViewEnvironmentProvider
{
    private static readonly Lazy<Task<CoreWebView2Environment>> EnvironmentTask = new(CreateEnvironmentAsync);

    public static Task<CoreWebView2Environment> GetAsync()
    {
        return EnvironmentTask.Value;
    }

    public static async Task PreloadAsync()
    {
        await GetAsync();
    }

    private static Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Peeklet", "WebView2");
        return CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
    }
}