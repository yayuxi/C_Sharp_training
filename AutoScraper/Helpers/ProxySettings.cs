namespace ScraperTemplate.Helpers;

/// <summary>
/// Configure your proxy settings here.
/// Free proxies are unreliable — for production use a residential proxy
/// provider like Brightdata, Oxylabs, or Smartproxy.
/// </summary>
public class ProxySettings
{
    public string Server { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }

    /// <summary>
    /// Example residential proxy configuration.
    /// Replace with your actual proxy provider details.
    /// </summary>
    public static ProxySettings Default => new ProxySettings
    {
        Server = "http://your-proxy-server:port",
        Username = "your-proxy-username",
        Password = "your-proxy-password"
    };
}