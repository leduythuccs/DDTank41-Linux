// Compile-time stand-ins for ASP.NET (System.Web) pieces that the legacy .ashx code
// references but never actually uses. This lets the original handler sources compile
// unchanged on ASP.NET Core; nothing here has runtime behavior.
using System;

namespace System.Web.Services
{
    /// <summary>Visual Studio template attribute on the handlers; has no effect on an IHttpHandler.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class WebServiceAttribute : Attribute
    {
        public string Namespace { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
    public sealed class WebServiceBindingAttribute : Attribute
    {
        public WsiProfiles ConformsTo { get; set; }
        public string Name { get; set; }
        public string Namespace { get; set; }
        public bool EmitConformanceClaims { get; set; }
    }

    public enum WsiProfiles
    {
        None,
        BasicProfile1_1
    }
}

// Namespaces imported by `using` directives only (unused WebForms imports).
namespace System.Web.Services.Protocols { internal static class NamespaceMarker { } }
namespace System.Web.UI { internal static class NamespaceMarker { } }
namespace System.Web.UI.HtmlControls { internal static class NamespaceMarker { } }
namespace System.Web.UI.WebControls { internal static class NamespaceMarker { } }
namespace System.Web.UI.WebControls.WebParts { internal static class NamespaceMarker { } }
