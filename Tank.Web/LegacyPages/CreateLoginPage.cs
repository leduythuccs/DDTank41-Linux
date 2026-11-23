using System;
using System.Reflection;
using System.Web;
using Bussiness.Interface;
using log4net;
using Tank.Request;

namespace Tank.Web.LegacyPages
{
    /// <summary>
    /// Port of Tank.Request/CreateLogin.aspx (WebForms pages do not run on ASP.NET Core).
    /// Called by the login site with a signed "content" parameter; registers the
    /// one-time game password for the account in PlayerManager. Writes 0 on success.
    /// </summary>
    public class CreateLoginPage : IHttpHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public bool IsReusable => false;

        public void ProcessRequest(HttpContext context)
        {
            int result = 1;
            try
            {
                string content = HttpUtility.UrlDecode(context.Request["content"]);
                string site = context.Request["site"] == null ? "" : HttpUtility.UrlDecode(context.Request["site"]).ToLower();
                BaseInterface inter = BaseInterface.CreateInterface();
                string[] str = inter.UnEncryptLogin(content, ref result, site);
                if (str.Length > 3)
                {
                    string name = str[0].Trim().ToLower();
                    string password = str[1].Trim().ToLower();
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(password))
                    {
                        name = BaseInterface.GetNameBySite(name, site);
                        PlayerManager.Add(name, password);
                        result = 0;
                    }
                    else
                    {
                        result = -91010;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("CreateLogin:", ex);
            }
            context.Response.Write(result);
        }
    }
}
