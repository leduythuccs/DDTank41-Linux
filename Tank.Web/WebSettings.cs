using System;
using System.Configuration;
using System.IO;
using System.Linq;

namespace Tank.Web
{
    /// <summary>Host settings read from appSettings ("Web.*" keys) in Tank.Web.dll.config.</summary>
    public sealed class WebSettings
    {
        public static WebSettings Current { get; private set; }

        /// <summary>Kestrel listen URLs, e.g. "http://127.0.0.1:8080".</summary>
        public string Urls { get; private set; }

        /// <summary>Base URL the Flash client uses to reach this site. Replaces http://127.0.0.1/ in /flash/config.xml.</summary>
        public string PublicBaseUrl { get; private set; }

        /// <summary>Tank.Request folder: served at /Request/ (handlers + generated XML).</summary>
        public string RequestRoot { get; private set; }

        /// <summary>Tank.Flash folder: the login site served at /.</summary>
        public string SiteRoot { get; private set; }

        /// <summary>Flash client folder (Source Flash/FlashSV1): served at /flash/.</summary>
        public string FlashRoot { get; private set; }

        /// <summary>Game resource pack folder: served at /resource/ when it exists.</summary>
        public string ResourceRoot { get; private set; }

        /// <summary>Inject the Ruffle Flash emulator into the game page.</summary>
        public bool Ruffle { get; private set; }

        /// <summary>Ruffle script URL (pinned version).</summary>
        public string RuffleUrl { get; private set; }

        /// <summary>Game server host as the client sees it (Server_List.IP), routed through the WebSocket proxy.</summary>
        public string SocketHost { get; private set; }

        /// <summary>Where the WebSocket proxy actually connects to (Road.Service).</summary>
        public string SocketTarget { get; private set; }

        /// <summary>Game server ports the WebSocket proxy may forward to.</summary>
        public int[] SocketPorts { get; private set; }

        public static WebSettings Load()
        {
            Current = new WebSettings
            {
                Urls = Get("Web.Urls", "http://127.0.0.1:8080"),
                PublicBaseUrl = Get("Web.PublicBaseUrl", "http://127.0.0.1:8080").TrimEnd('/'),
                RequestRoot = ResolvePath(Get("Web.RequestRoot", "../../../../Tank.Request")),
                SiteRoot = ResolvePath(Get("Web.SiteRoot", "../../../../Tank.Flash")),
                FlashRoot = ResolvePath(Get("Web.FlashRoot", "../../../../Source Flash/FlashSV1")),
                ResourceRoot = ResolvePath(Get("Web.ResourceRoot", "../../../../resource")),
                Ruffle = !string.Equals(Get("Web.Ruffle", "true"), "false", StringComparison.OrdinalIgnoreCase),
                RuffleUrl = Get("Web.RuffleUrl", "https://unpkg.com/@ruffle-rs/ruffle@0.7.1/ruffle.js"),
                SocketHost = Get("Web.SocketHost", "127.0.0.1"),
                SocketTarget = Get("Web.SocketTarget", "127.0.0.1"),
                SocketPorts = Get("Web.SocketPorts", "9500").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => int.Parse(p.Trim())).ToArray()
            };
            return Current;
        }

        private static string Get(string key, string defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }

        /// <summary>Relative paths are resolved against the application (bin) directory.</summary>
        private static string ResolvePath(string path)
        {
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path));
        }
    }
}
