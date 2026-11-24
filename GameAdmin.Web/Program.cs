using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Bussiness;
using Bussiness.Managers;
using log4net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tank.Web;

namespace GameAdmin.Web
{
    public static class Program
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Dictionary<string, MethodInfo> PageMethods = typeof(AdminMethods)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);

        public static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure(LogManager.GetRepository(Assembly.GetEntryAssembly()),
                new FileInfo(Path.Combine(AppContext.BaseDirectory, "logconfig.xml")));
            string root = ResolvePath(ConfigurationManager.AppSettings["Admin.Root"] ?? "../../../../GameAdmin");
            string urls = ConfigurationManager.AppSettings["Admin.Urls"] ?? "http://127.0.0.1:8081";
            string password = ConfigurationManager.AppSettings["Admin.Password"] ?? "";
            log.InfoFormat("Site root: {0}", root);
            if (password.Length == 0)
            {
                log.Error("Admin.Password is not set (appSettings.local.config, see deploy/configure-local.sh): every login is refused");
            }

            if (!LanguageMgr.Setup(AppContext.BaseDirectory))
            {
                log.Error("LanguageMgr setup failed (check LanguagePath)");
            }
            // Item templates are needed to send items; load them now instead of on the first mail.
            Task.Run(() => log.Info(ItemMgr.Init() ? "Item templates loaded" : "Loading item templates failed"));

            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls(urls.Split(';', StringSplitOptions.RemoveEmptyEntries));
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = "GameAdmin";
                    options.Cookie.HttpOnly = true;
                    // Strict: other sites cannot make the browser call the page methods with this cookie.
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.LoginPath = "/login";
                    options.ExpireTimeSpan = TimeSpan.FromHours(12);
                    // The page methods are called by jQuery: answer 401 there instead of the login page.
                    options.Events.OnRedirectToLogin = context =>
                    {
                        if (context.Request.Path.Value.Contains("/mainRequest.ashx/", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        }
                        else
                        {
                            context.Response.Redirect(context.RedirectUri);
                        }
                        return Task.CompletedTask;
                    };
                });
            builder.Services.AddAuthorization();

            WebApplication app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGet("/login", (HttpContext context) => Results.Content(LoginPage(context.Request.Query["ReturnUrl"], false), "text/html; charset=utf-8"));
            app.MapPost("/login", async (HttpContext context) =>
            {
                IFormCollection form = await context.Request.ReadFormAsync();
                string returnUrl = form["ReturnUrl"];
                if (!CheckPassword(form["password"], password))
                {
                    log.WarnFormat("Failed login from {0}", context.Connection.RemoteIpAddress);
                    await Task.Delay(1000);
                    return Results.Content(LoginPage(returnUrl, true), "text/html; charset=utf-8");
                }
                ClaimsIdentity identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "admin") }, CookieAuthenticationDefaults.AuthenticationScheme);
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                return Results.Redirect(IsLocalUrl(returnUrl) ? returnUrl : "/Admin/sendMail5Item.aspx");
            });
            app.MapGet("/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.Redirect("/login");
            });

            WebFormsLite renderer = new WebFormsLite(root);
            ItemPages pages = new ItemPages(root);
            MapPage(app, renderer, new[] { "/", "/Default.aspx" }, "Default.aspx", (query, labels) => "");
            MapPage(app, renderer, new[] { "/Admin/sendMail5Item.aspx" }, "Admin/sendMail5Item.aspx", (query, labels) =>
            {
                pages.SendMail5Item(labels);
                return "";
            });
            MapPage(app, renderer, new[] { "/Admin/serverConfig.aspx" }, "Admin/serverConfig.aspx", (query, labels) => "");
            MapPage(app, renderer, new[] { "/Admin/popup.aspx" }, "Admin/popup.aspx", (query, labels) => pages.Popup(query["IDs"], query["keys"], labels));
            MapPage(app, renderer, new[] { "/Admin/popupInfo.aspx" }, "Admin/popupInfo.aspx", (query, labels) => pages.PopupInfo(query["IDs"], query["keys"], labels));
            MapPage(app, renderer, new[] { "/Admin/customItem.aspx" }, "Admin/customItem.aspx", (query, labels) => pages.CustomItem(query["IDs"], query["keys"], labels));
            app.MapPost("/Admin/mainRequest.ashx/{method}", CallPageMethod).RequireAuthorization();
            app.MapPost("/mainRequest.ashx/{method}", CallPageMethod).RequireAuthorization();

            foreach (string folder in new[] { "Images", "Scripts", "Styles" })
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new CaseInsensitiveFileProvider(Path.Combine(root, folder)),
                    RequestPath = "/" + folder
                });
            }

            app.Lifetime.ApplicationStarted.Register(() => log.InfoFormat("GameAdmin listening on {0}", urls));
            app.Run();
        }

        private delegate string PageCodeBehind(IQueryCollection query, Dictionary<string, string> labels);

        private static void MapPage(WebApplication app, WebFormsLite renderer, string[] routes, string page, PageCodeBehind codeBehind)
        {
            foreach (string route in routes)
            {
                app.MapGet(route, (HttpContext context) =>
                {
                    Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    string written = codeBehind(context.Request.Query, labels);
                    return Results.Content(written + renderer.Render(page, labels), "text/html; charset=utf-8");
                }).RequireAuthorization();
            }
        }

        /// <summary>
        /// ASP.NET AJAX page method call: the body is a JSON object of arguments (the pages build it by hand,
        /// with unquoted names and single-quoted strings, which Newtonsoft accepts) and the answer is {"d": result}.
        /// </summary>
        private static async Task<IResult> CallPageMethod(HttpContext context, string method)
        {
            if (!PageMethods.TryGetValue(method, out MethodInfo target))
            {
                return Error(404, "Unknown web method " + method + ".");
            }
            string body;
            using (StreamReader reader = new StreamReader(context.Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }
            JObject args;
            try
            {
                args = string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body);
            }
            catch (JsonException e)
            {
                return Error(500, "Invalid JSON primitive: " + e.Message);
            }
            ParameterInfo[] parameters = target.GetParameters();
            object[] values = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                JToken token = args.GetValue(parameters[i].Name, StringComparison.OrdinalIgnoreCase);
                if (token == null)
                {
                    return Error(500, "Invalid web service call, missing value for parameter: '" + parameters[i].Name + "'.");
                }
                try
                {
                    values[i] = token.ToObject(parameters[i].ParameterType);
                }
                catch (Exception e) when (e is FormatException || e is ArgumentException || e is JsonException || e is OverflowException)
                {
                    return Error(500, "Cannot convert " + token + " to " + parameters[i].ParameterType.Name + ".");
                }
            }
            try
            {
                object result = await Task.Run(() => target.Invoke(null, values));
                return Results.Content(JsonConvert.SerializeObject(new { d = result }), "application/json; charset=utf-8");
            }
            catch (TargetInvocationException e)
            {
                log.Error("Page method " + method, e.InnerException);
                return Error(500, e.InnerException?.Message ?? e.Message);
            }
        }

        private static IResult Error(int status, string message)
        {
            return Results.Content(JsonConvert.SerializeObject(new { Message = message }), "application/json; charset=utf-8", Encoding.UTF8, status);
        }

        private static bool CheckPassword(string given, string expected)
        {
            if (string.IsNullOrEmpty(expected) || given == null)
            {
                return false;
            }
            byte[] a = SHA256.HashData(Encoding.UTF8.GetBytes(given));
            byte[] b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        private static bool IsLocalUrl(string url)
        {
            return !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");
        }

        private static string LoginPage(string returnUrl, bool failed)
        {
            return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Tank Manager - Login</title>"
                + "<link href=\"/Styles/Site.css\" rel=\"stylesheet\" type=\"text/css\" /></head><body>"
                + "<div class=\"page\" style=\"padding:20px\"><h2>Tank Manager</h2>"
                + (failed ? "<p style=\"color:#FF0000\">Sai mật khẩu.</p>" : "")
                + "<form method=\"post\" action=\"/login\"><input type=\"hidden\" name=\"ReturnUrl\" value=\"" + WebUtility.HtmlEncode(returnUrl ?? "") + "\" />"
                + "<label>Mật khẩu: <input type=\"password\" name=\"password\" autofocus /></label> <input type=\"submit\" value=\"Đăng nhập\" />"
                + "</form></div></body></html>";
        }

        private static string ResolvePath(string path)
        {
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path));
        }
    }
}
