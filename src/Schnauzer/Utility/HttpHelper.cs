namespace Schnauzer;
public static class HttpHelper
{
    private static readonly HttpClient _http = new();

    public static Task<string> DownloadStringAsync(string requestUri)
    {
        return _http.GetStringAsync(requestUri);
    }
}
