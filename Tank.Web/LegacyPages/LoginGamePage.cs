using System;
using System.Configuration;
using System.Web;
using System.Web.SessionState;
using Bussiness.Interface;
using Tank.Flash;

namespace Tank.Web.LegacyPages
{
    /// <summary>
    /// Port of Tank.Flash/LoginGame.aspx. Called by loading.htm after checkuser.ashx stored the
    /// account in the session: creates a one-time game key, registers it through
    /// Request/CreateLogin.aspx, and writes the playgame.aspx URL for the browser to open.
    /// </summary>
    public class LoginGamePage : IHttpHandler, IRequiresSessionState
    {
        public bool IsReusable => false;

        private static string LoginOnUrl => ConfigurationManager.AppSettings["LoginOnUrl"];

        private static string FlashUrl => ConfigurationManager.AppSettings["FlashUrl"];

        public void ProcessRequest(HttpContext context)
        {
            string username = context.Session["username"] as string;
            string sessionPassword = context.Session["password"] as string;
            // The original page tested `== null && ...ToString()`, which threw on an empty session.
            if (string.IsNullOrEmpty(username) || sessionPassword == null || !LoadingManager.Login(username, sessionPassword))
            {
                context.Response.Redirect(LoginOnUrl, false);
                return;
            }
            try
            {
                string name = username;
                string password = Guid.NewGuid().ToString();
                string time = BaseInterface.ConvertDateTimeInt(DateTime.Now).ToString();
                string key = BaseInterface.GetLoginKey;
                string v = BaseInterface.md5(name + password + time + key);
                // The original page sent "name|password|srv|login|time|md5", an older format; the
                // BaseInterface.UnEncryptLogin in this repo verifies "name|password|time|md5(name+password+time+key)".
                string url = BaseInterface.LoginUrl + "?content=" + HttpUtility.UrlEncode(name + "|" + password + "|" + time + "|" + v);
                string result = BaseInterface.RequestContent(url);
                if (result == "0")
                {
                    context.Response.Write(FlashUrl + "?user=" + HttpUtility.UrlEncode(name) + "&key=" + HttpUtility.UrlEncode(password.ToUpper()));
                }
                else
                {
                    context.Response.Write(result);
                }
            }
            catch (Exception ex)
            {
                context.Response.Write(ex.ToString());
            }
        }
    }
}
