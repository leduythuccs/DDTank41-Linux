using System;
using System.Configuration;
using System.Reflection;
using System.Web;
using Bussiness;
using log4net;

namespace Tank.Flash.auth
{
    /// <summary>
    /// "Đăng ký nhanh" on the login page: creates an account from a username and a password only (no email,
    /// no password confirmation, no captcha). Answers "ok", "exit" (the username is taken) or an error message.
    /// </summary>
    public class register : IHttpHandler
    {
        protected static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int MaxUsernameLength = 50;

        protected string SiteTitle
        {
            get
            {
                return ConfigurationManager.AppSettings["SiteTitle"] ?? "DanDanTang";
            }
        }

        public void ProcessRequest(HttpContext context)
        {
            string username = (context.Request["username"] ?? "").Trim();
            string password = context.Request["password"] ?? "";
            string message;
            if (username.Length == 0)
            {
                message = LanguageMgr.GetTranslation("Tank.Flash.auth.register.NoUsername");
            }
            else if (username.Length > MaxUsernameLength || username.Contains(","))
            {
                message = LanguageMgr.GetTranslation("Tank.Flash.auth.register.BadUsername");
            }
            else if (password.Length == 0)
            {
                message = LanguageMgr.GetTranslation("Tank.Flash.auth.register.NoPassword");
            }
            else
            {
                try
                {
                    using MemberShipBussiness db = new MemberShipBussiness();
                    if (db.ExistsUsername(username))
                    {
                        message = "exit";
                    }
                    else if (db.CreateUsername(SiteTitle, username, Bussiness.Interface.BaseInterface.md5(password), "", "1", "MD5", false))
                    {
                        message = "ok";
                    }
                    else
                    {
                        message = LanguageMgr.GetTranslation("Tank.Flash.auth.register.Fail");
                    }
                }
                catch (Exception e)
                {
                    log.Error("register", e);
                    message = LanguageMgr.GetTranslation("Tank.Flash.auth.register.Fail");
                }
            }
            context.Response.ContentType = "text/plain";
            context.Response.Write(message);
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}
