using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Tank.Web
{
    /// <summary>
    /// The site was served by IIS, where paths are case-insensitive, and the client requests
    /// files with different casing than on disk (e.g. TemplateAllList.xml vs templatealllist.xml).
    /// Falls back to a case-insensitive lookup, segment by segment, when the exact path is missing.
    /// Paths containing bin/obj/... are never served.
    /// </summary>
    public sealed class CaseInsensitiveFileProvider : IFileProvider
    {
        private static readonly string[] BlockedSegments = { "bin", "obj", "properties", "packages", ".vs", "app_data", "service references" };

        private readonly string root;

        private readonly PhysicalFileProvider inner;

        public CaseInsensitiveFileProvider(string root)
        {
            this.root = Path.GetFullPath(root);
            inner = new PhysicalFileProvider(this.root);
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            if (IsBlocked(subpath))
            {
                return new NotFoundFileInfo(subpath);
            }
            IFileInfo info = inner.GetFileInfo(subpath);
            if (info.Exists)
            {
                return info;
            }
            string resolved = Resolve(subpath);
            return resolved == null ? info : inner.GetFileInfo(resolved);
        }

        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

        public IChangeToken Watch(string filter) => inner.Watch(filter);

        private static bool IsBlocked(string subpath)
        {
            return subpath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => BlockedSegments.Contains(segment, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>Returns the on-disk relative path matching <paramref name="subpath"/> ignoring case, or null.</summary>
        private string Resolve(string subpath)
        {
            string current = root;
            string[] segments = subpath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == "." || segments[i] == "..")
                {
                    return null;
                }
                bool last = i == segments.Length - 1;
                string match = null;
                try
                {
                    var candidates = last ? Directory.EnumerateFiles(current) : Directory.EnumerateDirectories(current);
                    match = candidates.FirstOrDefault(p => string.Equals(Path.GetFileName(p), segments[i], StringComparison.OrdinalIgnoreCase));
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                if (match == null)
                {
                    return null;
                }
                current = match;
            }
            return Path.GetRelativePath(root, current).Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
