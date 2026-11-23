using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web;
using log4net;

namespace Tank.Web
{
    /// <summary>
    /// Runs a legacy IHttpHandler the way IIS did: a new instance per request (several handlers keep
    /// per-request state in fields) and UTF-8 output without a BOM. The adapters' default writer
    /// starts every body with a BOM, which breaks exact comparisons such as result == "0" in the
    /// login handshake and confuses the Flash client's parsers.
    /// </summary>
    public sealed class LegacyHandlerShim : IHttpHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly Type handlerType;

        public LegacyHandlerShim(Type handlerType)
        {
            this.handlerType = handlerType;
        }

        public bool IsReusable => true;

        public void ProcessRequest(HttpContext context)
        {
            // HttpResponse.ContentEncoding cannot be set before a Content-Type exists (it throws), so
            // replace the writer instead; its default is Encoding.UTF8, which emits a BOM.
            context.Response.Output = new StreamWriter(context.Response.OutputStream, Utf8NoBom, -1, leaveOpen: true)
            {
                AutoFlush = true
            };
            try
            {
                IHttpHandler handler = (IHttpHandler)Activator.CreateInstance(handlerType);
                handler.ProcessRequest(context);
            }
            catch (Exception ex)
            {
                log.Error("Unhandled exception in " + handlerType.FullName + " for " + context.Request.RawUrl, ex);
                throw;
            }
        }
    }
}
