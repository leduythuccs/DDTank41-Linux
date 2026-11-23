using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.SessionState;
using Bussiness;
using log4net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.SystemWebAdapters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Tank.Web.LegacyPages;

namespace Tank.Web
{
    public static class Program
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly ILog requestLog = LogManager.GetLogger("Tank.Web.Requests");

        private const string PermissivePolicy =
            "<?xml version=\"1.0\"?><cross-domain-policy><allow-access-from domain=\"*\" /></cross-domain-policy>";

        public static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure(LogManager.GetRepository(Assembly.GetEntryAssembly()),
                new FileInfo(Path.Combine(AppContext.BaseDirectory, "logconfig.xml")));
            WebSettings settings = WebSettings.Load();
            log.InfoFormat("Request root: {0}", settings.RequestRoot);
            log.InfoFormat("Site root:    {0}", settings.SiteRoot);
            log.InfoFormat("Flash root:   {0}", settings.FlashRoot);
            log.InfoFormat("Resources:    {0}{1}", settings.ResourceRoot, Directory.Exists(settings.ResourceRoot) ? "" : " (missing: /resource/ requests will 404)");

            InitLegacyApplication(settings);

            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls(settings.Urls.Split(';', StringSplitOptions.RemoveEmptyEntries));
            // The legacy handlers write to the response synchronously.
            builder.WebHost.ConfigureKestrel(options => options.AllowSynchronousIO = true);

            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSystemWebAdapters()
                .AddJsonSessionSerializer(options =>
                {
                    // Every session key the legacy handlers use must be registered with its type.
                    options.RegisterKey<string>("username");
                    options.RegisterKey<string>("password");
                    options.RegisterKey<string>("CheckCode");
                    options.RegisterKey<string>("user");
                    options.RegisterKey<bool?>("Loading");
                })
                .AddWrappedAspNetCoreSession(options =>
                {
                    options.Cookie.Name = "ASP.NET_SessionId";
                    options.IdleTimeout = TimeSpan.FromMinutes(60);
                });
            builder.Services.Configure<SystemWebAdaptersOptions>(options =>
            {
                // Server.MapPath("~") in Tank.Request code resolves to the Tank.Request folder.
                options.AppDomainAppVirtualPath = "/Request/";
                options.AppDomainAppPath = settings.RequestRoot + Path.DirectorySeparatorChar;
            });

            WebApplication app = builder.Build();

            app.Use(LogRequests);
            app.UseWebSockets();
            app.UseSession();
            app.UseSystemWebAdapters();

            MapHandlers(app, settings);

            FileExtensionContentTypeProvider contentTypes = new FileExtensionContentTypeProvider();
            contentTypes.Mappings[".swf"] = "application/x-shockwave-flash";
            contentTypes.Mappings[".xml"] = "text/xml";
            contentTypes.Mappings[".map"] = "application/octet-stream";
            ServeStatic(app, "/flash", settings.FlashRoot, contentTypes);
            ServeStatic(app, "/resource", settings.ResourceRoot, contentTypes);
            ServeStatic(app, "/Request", settings.RequestRoot, contentTypes);
            ServeStatic(app, "", settings.SiteRoot, contentTypes);

            // Flash asks for crossdomain.xml on every host/folder it loads from; answer the ones that do not exist.
            app.Use(async (context, next) =>
            {
                if (context.GetEndpoint() == null && context.Request.Path.Value.EndsWith("/crossdomain.xml", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.ContentType = "text/xml";
                    await context.Response.WriteAsync(PermissivePolicy);
                    return;
                }
                await next();
            });

            log.InfoFormat("Tank.Web listening on {0} (public URL {1})", settings.Urls, settings.PublicBaseUrl);
            log.InfoFormat("Login page: {0}/index.htm", settings.PublicBaseUrl);
            app.Run();
        }

        /// <summary>What Tank.Request/Global.asax Application_Start did.</summary>
        private static void InitLegacyApplication(WebSettings settings)
        {
            string requestRoot = settings.RequestRoot + Path.DirectorySeparatorChar;
            if (!LanguageMgr.Setup(requestRoot))
            {
                log.Error("LanguageMgr setup failed (check LanguagePath)");
            }
            Tank.Request.StaticsMgr.Setup(requestRoot);
            Tank.Request.PlayerManager.Setup();
        }

        private static void MapHandlers(WebApplication app, WebSettings settings)
        {
            Assembly assembly = typeof(Program).Assembly;
            int count = 0;
            foreach (var (path, handler) in LegacyHandlerMap.Scan(settings.RequestRoot, "/Request/", assembly))
            {
                MapHandler(app, path, handler);
                count++;
            }
            foreach (var (path, handler) in LegacyHandlerMap.Scan(settings.SiteRoot, "/", assembly))
            {
                MapHandler(app, path, handler);
                count++;
            }
            // WebForms pages needed by the login flow, ported to handlers.
            MapHandler(app, "/Request/CreateLogin.aspx", typeof(CreateLoginPage));
            MapHandler(app, "/LoginGame.aspx", typeof(LoginGamePage));
            MapHandler(app, "/playgame.aspx", typeof(PlayGamePage));
            log.InfoFormat("Mapped {0} legacy handlers", count + 3);

            app.MapGet("/", context =>
            {
                context.Response.Redirect("/index.htm");
                return System.Threading.Tasks.Task.CompletedTask;
            });
            // WebSocket -> TCP bridge so the game socket works under Ruffle in a browser.
            app.Map("/socket/{port:int}", context => SocketProxy.Handle(context, settings));
            // config.xml hardcodes http://127.0.0.1/ for every site; point it at this host instead.
            app.MapGet("/flash/config.xml", async context =>
            {
                string xml = await File.ReadAllTextAsync(Path.Combine(settings.FlashRoot, "config.xml"));
                xml = xml.Replace("http://127.0.0.1/", settings.PublicBaseUrl + "/");
                // SITE (all game art) pointed at the official Gunny CDN; serve it from our own /resource/.
                xml = System.Text.RegularExpressions.Regex.Replace(xml, "<SITE value=\"[^\"]*\"", "<SITE value=\"" + settings.PublicBaseUrl + "/resource/\"");
                context.Response.ContentType = "text/xml; charset=utf-8";
                await context.Response.WriteAsync(xml, Encoding.UTF8);
            });
        }

        private static void MapHandler(WebApplication app, string path, Type handler)
        {
            IEndpointConventionBuilder endpoint = app.MapHttpHandler(path, new LegacyHandlerShim(handler))
                .PreBufferRequestStream()
                .BufferResponseStream();
            if (typeof(IRequiresSessionState).IsAssignableFrom(handler))
            {
                endpoint.RequireSystemWebAdapterSession();
            }
        }

        private static void ServeStatic(WebApplication app, string requestPath, string root, FileExtensionContentTypeProvider contentTypes)
        {
            if (!Directory.Exists(root))
            {
                return;
            }
            app.UseStaticFiles(new StaticFileOptions
            {
                RequestPath = requestPath,
                FileProvider = new CaseInsensitiveFileProvider(root),
                ContentTypeProvider = contentTypes,
                ServeUnknownFileTypes = false
            });
        }

        private static async System.Threading.Tasks.Task LogRequests(HttpContext context, Func<System.Threading.Tasks.Task> next)
        {
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                await next();
            }
            finally
            {
                int status = context.Response.StatusCode;
                string line = string.Format("{0} {1} {2}{3} {4}ms", status, context.Request.Method, context.Request.Path, context.Request.QueryString.HasValue && context.Request.QueryString.Value.Length < 120 ? context.Request.QueryString.Value : "", watch.ElapsedMilliseconds);
                if (status >= 400)
                {
                    requestLog.Warn(line);
                }
                else
                {
                    requestLog.Info(line);
                }
            }
        }
    }
}
