using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using log4net;
using Microsoft.AspNetCore.Http;

namespace Tank.Web
{
    /// <summary>
    /// Optional pass-through for /resource/: files missing from the local resource folder are fetched
    /// from Web.ResourceUpstream and streamed to the client (nothing is stored on disk). The client keeps
    /// loading from this host, which matters under Ruffle: it loads through the browser's fetch, so a
    /// cross-origin resource site would need CORS headers.
    /// Disabled unless Web.ResourceUpstream is set; local files always win.
    /// </summary>
    public sealed class ResourceProxy
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] PassedHeaders = { "Content-Type", "Content-Length", "Last-Modified", "ETag", "Cache-Control" };

        private readonly Uri upstream;

        private readonly HttpClient http;

        public ResourceProxy(string upstreamUrl)
        {
            upstream = new Uri(upstreamUrl.EndsWith("/") ? upstreamUrl : upstreamUrl + "/");
            http = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            log.InfoFormat("Resources missing locally are fetched from {0}", upstream);
        }

        /// <summary>Handles GET/HEAD /resource/{path}; returns false when the request is not for it.</summary>
        public async Task<bool> TryHandle(HttpContext context)
        {
            PathString path = context.Request.Path;
            if (!path.StartsWithSegments("/resource", StringComparison.OrdinalIgnoreCase, out PathString rest)
                || !(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                || !rest.HasValue || rest.Value.Contains(".."))
            {
                return false;
            }
            Uri target = new Uri(upstream, rest.Value.TrimStart('/') + context.Request.QueryString.Value);
            if (!string.Equals(target.Host, upstream.Host, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethods.IsHead(context.Request.Method) ? HttpMethod.Head : HttpMethod.Get, target);
                using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
                context.Response.StatusCode = (int)response.StatusCode;
                foreach (string name in PassedHeaders)
                {
                    if (response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values))
                    {
                        context.Response.Headers[name] = string.Join(",", values);
                    }
                }
                if (!response.IsSuccessStatusCode)
                {
                    log.WarnFormat("Upstream {0} for {1}", (int)response.StatusCode, target);
                }
                if (HttpMethods.IsGet(context.Request.Method))
                {
                    await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status502BadGateway;
                }
                log.WarnFormat("Upstream fetch failed for {0}: {1}", target, ex.Message);
            }
            return true;
        }
    }
}
