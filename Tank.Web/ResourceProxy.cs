using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using log4net;
using Microsoft.AspNetCore.Http;

namespace Tank.Web
{
    /// <summary>
    /// Optional pass-through for /resource/: files missing locally are fetched from Web.ResourceUpstream
    /// and streamed to the client. The client keeps loading from this host, which matters under Ruffle:
    /// it loads through the browser's fetch, so a cross-origin resource site would need CORS headers.
    ///
    /// With a cache folder (Web.ResourceCacheRoot), every complete 200 response is also saved there
    /// (written to a temp file, then renamed), and the static file handler serves it next time without
    /// contacting the upstream. The cache key is the path only: the client appends random cache-busters
    /// such as ?lv=0.65. Upstream misses are remembered for a while because the client retries missing
    /// files in a loop.
    /// Disabled unless Web.ResourceUpstream is set; files in the resource folder always win.
    /// </summary>
    public sealed class ResourceProxy
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] PassedHeaders = { "Content-Type", "Content-Length", "Last-Modified", "ETag", "Cache-Control" };

        private static readonly TimeSpan MissTtl = TimeSpan.FromMinutes(10);

        private readonly Uri upstream;

        private readonly string cacheRoot;

        private readonly HttpClient http;

        private readonly ConcurrentDictionary<string, DateTime> misses = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public ResourceProxy(string upstreamUrl, string cacheRoot)
        {
            upstream = new Uri(upstreamUrl.EndsWith("/") ? upstreamUrl : upstreamUrl + "/");
            this.cacheRoot = string.IsNullOrEmpty(cacheRoot) ? null : Path.GetFullPath(cacheRoot);
            http = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            if (this.cacheRoot != null)
            {
                Directory.CreateDirectory(this.cacheRoot);
            }
            log.InfoFormat("Resources missing locally are fetched from {0}{1}", upstream,
                this.cacheRoot == null ? " (not cached)" : " and cached in " + this.cacheRoot);
        }

        /// <summary>Handles GET/HEAD /resource/{path}; returns false when the request is not for it.</summary>
        public async Task<bool> TryHandle(HttpContext context)
        {
            PathString path = context.Request.Path;
            if (!path.StartsWithSegments("/resource", StringComparison.OrdinalIgnoreCase, out PathString rest)
                || !(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                || !rest.HasValue || rest.Value.Contains("..") || rest.Value.Contains('\\'))
            {
                return false;
            }
            string relative = rest.Value.TrimStart('/');
            Uri target = new Uri(upstream, relative + context.Request.QueryString.Value);
            if (!string.Equals(target.Host, upstream.Host, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (misses.TryGetValue(relative, out DateTime missedAt) && DateTime.UtcNow - missedAt < MissTtl)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return true;
            }

            string cacheFile = CachePath(relative);
            string tempFile = null;
            FileStream cache = null;
            try
            {
                bool isGet = HttpMethods.IsGet(context.Request.Method);
                using HttpRequestMessage request = new HttpRequestMessage(isGet ? HttpMethod.Get : HttpMethod.Head, target);
                using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
                context.Response.StatusCode = (int)response.StatusCode;
                if (!response.IsSuccessStatusCode)
                {
                    // No headers/body from the upstream error page (its Content-Length would not match an empty body).
                    if ((int)response.StatusCode == StatusCodes.Status404NotFound)
                    {
                        misses[relative] = DateTime.UtcNow;
                    }
                    log.WarnFormat("Upstream {0} for {1}", (int)response.StatusCode, target);
                    return true;
                }
                foreach (string name in PassedHeaders)
                {
                    if (response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values))
                    {
                        context.Response.Headers[name] = string.Join(",", values);
                    }
                }
                if (!isGet)
                {
                    return true;
                }

                if (cacheFile != null && response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cacheFile));
                    tempFile = cacheFile + "." + Guid.NewGuid().ToString("N") + ".part";
                    cache = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                }

                long expected = response.Content.Headers.ContentLength ?? -1;
                long total = 0;
                byte[] buffer = new byte[81920];
                using (Stream body = await response.Content.ReadAsStreamAsync(context.RequestAborted))
                {
                    int read;
                    while ((read = await body.ReadAsync(buffer, context.RequestAborted)) > 0)
                    {
                        total += read;
                        if (cache != null)
                        {
                            await cache.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted);
                        }
                        await context.Response.Body.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted);
                    }
                }

                if (cache != null)
                {
                    await cache.FlushAsync();
                    cache.Dispose();
                    cache = null;
                    if (expected < 0 || total == expected)
                    {
                        File.Move(tempFile, cacheFile, overwrite: true);
                        tempFile = null;
                        log.InfoFormat("Cached {0} ({1} bytes)", relative, total);
                    }
                    else
                    {
                        log.WarnFormat("Not caching {0}: got {1} of {2} bytes", relative, total, expected);
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException || ex is IOException)
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status502BadGateway;
                }
                log.WarnFormat("Upstream fetch failed for {0}: {1}", target, ex.Message);
            }
            finally
            {
                cache?.Dispose();
                if (tempFile != null)
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch (IOException)
                    {
                    }
                }
            }
            return true;
        }

        /// <summary>Cache location for a request path, or null when caching is off or the path escapes the folder.</summary>
        private string CachePath(string relative)
        {
            if (cacheRoot == null || relative.Length == 0 || relative.EndsWith("/"))
            {
                return null;
            }
            string full = Path.GetFullPath(Path.Combine(cacheRoot, relative));
            return full.StartsWith(cacheRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
        }
    }
}
