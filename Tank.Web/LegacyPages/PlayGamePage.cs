using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.SessionState;
using Tank.Flash;

namespace Tank.Web.LegacyPages
{
    /// <summary>
    /// Port of Tank.Flash/playgame.aspx. Renders the original playgame.aspx markup (filling the
    /// &lt;%= %&gt; expressions) so the page looks the same, and since browsers no longer ship a
    /// Flash plugin it can also inject the Ruffle emulator and shows the direct Loading.swf URL
    /// to open in a standalone Flash Player projector.
    /// </summary>
    public class PlayGamePage : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable => false;

        private static string Setting(string key) => ConfigurationManager.AppSettings[key];

        public void ProcessRequest(HttpContext context)
        {
            string loginOnUrl = Setting("LoginOnUrl");
            string username = context.Session["username"] as string;
            string sessionPassword = context.Session["password"] as string;
            string user = HttpUtility.UrlDecode(context.Request["user"]);
            string key = HttpUtility.UrlDecode(context.Request["key"]);
            // The original page tested `== null && ...ToString()`, which threw on an empty session.
            if (string.IsNullOrEmpty(username) || sessionPassword == null || !LoadingManager.Login(user, sessionPassword))
            {
                if (!string.IsNullOrEmpty(username))
                {
                    LoadingManager.Remove(username);
                }
                context.Response.Redirect(loginOnUrl, false);
                return;
            }
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(key))
            {
                context.Response.Redirect(loginOnUrl, false);
                return;
            }

            string editby = context.Request["editby"] == null ? "" : HttpUtility.UrlDecode(context.Request["editby"]);
            string content = "user=" + HttpUtility.UrlEncode(user) + "&key=" + HttpUtility.UrlEncode(key);
            string autoParam = "editby=" + HttpUtility.UrlEncode(editby);
            string flash = Setting("FlashSite");
            string config = Setting("FlashConfig");
            string swfUrl = flash + "Loading.swf?" + content + "&config=" + config;

            string template = File.ReadAllText(Path.Combine(WebSettings.Current.SiteRoot, "playgame.aspx"));
            template = Regex.Replace(template, @"<%@[^%]*%>\s*", "");
            string html = template
                .Replace("<%=Flash %>", WebUtility.HtmlEncode(flash))
                .Replace("<%=Content %>", WebUtility.HtmlEncode(content))
                .Replace("<%=Config %>", WebUtility.HtmlEncode(config))
                .Replace("<%=AutoParam %>", WebUtility.HtmlEncode(autoParam))
                .Replace(" runat=\"server\"", "");

            string extra =
                "<div style=\"position:fixed;left:0;right:0;bottom:0;padding:6px 10px;background:#000c;color:#ddd;font:12px sans-serif;word-break:break-all\">"
                + "Standalone Flash Player: <code style=\"user-select:all\">" + WebUtility.HtmlEncode(swfUrl) + "</code>"
                + (WebSettings.Current.Ruffle ? " &middot; running in Ruffle (Flash emulator)" : "")
                + "</div>";
            if (WebSettings.Current.Ruffle)
            {
                extra = "<script>window.RufflePlayer = window.RufflePlayer || {}; window.RufflePlayer.config = { allowScriptAccess: true, autoplay: \"on\", unmuteOverlay: \"hidden\", logLevel: \"info\", socketProxy: " + SocketProxy.RuffleConfig(WebSettings.Current) + " };</script>"
                    + "<script src=\"" + WebUtility.HtmlEncode(WebSettings.Current.RuffleUrl) + "\"></script>" + extra;
            }
            html = html.Replace("</body>", extra + "</body>");

            context.Response.ContentType = "text/html";
            context.Response.Write(html);
        }
    }
}
