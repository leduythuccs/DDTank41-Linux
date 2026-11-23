using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace GameAdmin.Web
{
    /// <summary>
    /// Renders the original GameAdmin .aspx/.master markup without WebForms. The pages only use a few
    /// server controls, which are handled here:
    ///   &lt;%@ ... %&gt;                      directives: removed
    ///   asp:Content / asp:ContentPlaceHolder   the page's Content blocks go into the master's placeholders
    ///   asp:Label ID="X"                       &lt;span id="X"&gt;text&lt;/span&gt; (the text comes from the ported code-behind)
    ///   asp:Menu                               a &lt;ul&gt; of links, like WebForms' list rendering
    ///   runat="server", "~/"                   removed, resolved to the site root
    /// </summary>
    public sealed class WebFormsLite
    {
        private static readonly Regex Directive = new Regex(@"<%@.*?%>\s*", RegexOptions.Singleline);

        private static readonly Regex MasterFile = new Regex(@"<%@\s*Page[^%]*MasterPageFile=""~/([^""]+)""", RegexOptions.IgnoreCase);

        private static readonly Regex Content = new Regex(
            @"<asp:Content\b[^>]*ContentPlaceHolderID=""(\w+)""[^>]*>(.*?)</asp:Content>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex Placeholder = new Regex(
            @"<asp:ContentPlaceHolder\b[^>]*ID=""(\w+)""[^>]*?(?:/>|>(.*?)</asp:ContentPlaceHolder>)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex Label = new Regex(
            @"<asp:Label\b([^>]*?)(?:/>|>(.*?)</asp:Label>)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex Menu = new Regex(
            @"<asp:Menu\b([^>]*)>(.*?)</asp:Menu>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex MenuItem = new Regex(@"<asp:MenuItem\b([^>]*)/>", RegexOptions.IgnoreCase);

        private static readonly Regex Runat = new Regex(@"\s+runat=""server""", RegexOptions.IgnoreCase);

        private static readonly Regex AppRelative = new Regex(@"(?<=(?:href|src|action)="")~/", RegexOptions.IgnoreCase);

        private static readonly Regex ServerForm = new Regex(@"<form\b([^>]*)runat=""server""([^>]*)>", RegexOptions.IgnoreCase);

        private readonly string root;

        public WebFormsLite(string root)
        {
            this.root = root;
        }

        /// <summary>Renders a page (path relative to the site root, e.g. "Admin/popup.aspx").</summary>
        /// <param name="labels">text of the asp:Label controls by ID; missing IDs render empty, as in WebForms</param>
        public string Render(string page, IDictionary<string, string> labels = null)
        {
            string markup = File.ReadAllText(Path.Combine(root, page));
            Match master = MasterFile.Match(markup);
            if (master.Success)
            {
                Dictionary<string, string> contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match m in Content.Matches(markup))
                {
                    contents[m.Groups[1].Value] = m.Groups[2].Value;
                }
                markup = File.ReadAllText(Path.Combine(root, FindFile(master.Groups[1].Value)));
                markup = Placeholder.Replace(markup, m =>
                    contents.TryGetValue(m.Groups[1].Value, out string content) ? content : m.Groups[2].Value);
            }

            markup = Directive.Replace(markup, "");
            markup = Label.Replace(markup, m =>
            {
                string id = Attribute(m.Groups[1].Value, "ID");
                string text = labels != null && id != null && labels.TryGetValue(id, out string value)
                    ? value
                    : WebUtility.HtmlDecode(Attribute(m.Groups[1].Value, "Text") ?? m.Groups[2].Value);
                return id == null ? "<span>" + text + "</span>" : "<span id=\"" + id + "\">" + text + "</span>";
            });
            markup = Menu.Replace(markup, RenderMenu);
            // WebForms posted the form back to the page; nothing here handles a postback.
            markup = ServerForm.Replace(markup, m => "<form" + m.Groups[1].Value + m.Groups[2].Value + " onsubmit=\"return false;\">");
            markup = Runat.Replace(markup, "");
            markup = AppRelative.Replace(markup, "/");
            // WebForms injected its menu script, which Site.Master calls (new Sys.WebForms.Menu(...)) for
            // hover pop-outs; the menus here are flat, so a no-op stands in for it.
            int head = markup.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (head >= 0 && markup.Contains("Sys.WebForms"))
            {
                markup = markup.Insert(head, "<script type=\"text/javascript\">window.Sys = window.Sys || { WebForms: { Menu: function () { } } };</script>\n");
            }
            return markup;
        }

        private static string RenderMenu(Match menu)
        {
            string id = Attribute(menu.Groups[1].Value, "ID");
            string css = Attribute(menu.Groups[1].Value, "CssClass");
            string html = "<div" + (css != null ? " class=\"" + css + "\"" : "") + (id != null ? " id=\"" + id + "\"" : "") + "><ul class=\"level1\">";
            foreach (Match item in MenuItem.Matches(menu.Groups[2].Value))
            {
                string url = (Attribute(item.Groups[1].Value, "NavigateUrl") ?? "#").Replace("~/", "/");
                html += "<li><a class=\"level1\" href=\"" + url + "\">" + Attribute(item.Groups[1].Value, "Text") + "</a></li>";
            }
            return html + "</ul></div>";
        }

        private static string Attribute(string attributes, string name)
        {
            Match m = Regex.Match(attributes, @"\b" + name + @"=""([^""]*)""", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>The markup links "Site.master" while the file is Site.Master: look the name up case-insensitively.</summary>
        private string FindFile(string relative)
        {
            if (File.Exists(Path.Combine(root, relative)))
            {
                return relative;
            }
            string dir = Path.GetDirectoryName(relative) ?? "";
            string name = Path.GetFileName(relative);
            foreach (string file in Directory.GetFiles(Path.Combine(root, dir)))
            {
                if (string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase))
                {
                    return Path.Combine(dir, Path.GetFileName(file));
                }
            }
            return relative;
        }
    }
}
