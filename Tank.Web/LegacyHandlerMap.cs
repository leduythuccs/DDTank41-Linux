using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;

namespace Tank.Web
{
    /// <summary>
    /// Builds the URL -> handler type table the way IIS did: every .ashx file holds a
    /// &lt;%@ WebHandler Class="..." %&gt; directive naming the IHttpHandler to run.
    /// </summary>
    public static class LegacyHandlerMap
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly Regex ClassDirective = new Regex("Class\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);

        private static readonly string[] SkippedFolders = { "bin", "obj", "packages" };

        /// <summary>Returns (url path, handler type) for every .ashx under <paramref name="root"/>, mounted at <paramref name="urlPrefix"/>.</summary>
        public static IEnumerable<(string Path, Type Handler)> Scan(string root, string urlPrefix, Assembly assembly)
        {
            if (!Directory.Exists(root))
            {
                log.WarnFormat("Handler root not found: {0}", root);
                yield break;
            }
            foreach (string file in Directory.EnumerateFiles(root, "*.ashx", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                string relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                if (relative.Split('/').Any(s => SkippedFolders.Contains(s, StringComparer.OrdinalIgnoreCase)))
                {
                    continue;
                }
                Match match = ClassDirective.Match(File.ReadAllText(file));
                Type type = match.Success ? assembly.GetType(match.Groups[1].Value.Trim(), throwOnError: false, ignoreCase: true) : null;
                if (type == null || !typeof(System.Web.IHttpHandler).IsAssignableFrom(type))
                {
                    log.WarnFormat("Skipping {0}{1}: handler class {2} is not compiled", urlPrefix, relative, match.Success ? match.Groups[1].Value : "(no directive)");
                    continue;
                }
                yield return (urlPrefix + relative, type);
            }
        }
    }
}
