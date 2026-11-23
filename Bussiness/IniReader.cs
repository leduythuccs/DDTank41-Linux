using System;
using System.Collections.Generic;
using System.IO;

namespace Bussiness
{
    /// <summary>
    /// Managed replacement for kernel32 GetPrivateProfileString (Windows-only).
    /// Sections and keys are case-insensitive, ';' / '#' lines are comments, and a value
    /// wrapped in matching quotes is unquoted, like the Win32 API. The file is re-parsed
    /// whenever it changes on disk, so runtime edits are picked up as before.
    /// </summary>
    public class IniReader
    {
        private string FilePath;

        private Dictionary<string, Dictionary<string, string>> sections;

        private DateTime loadedWriteTime;

        private readonly object syncRoot = new object();

        public IniReader(string _FilePath)
        {
			FilePath = _FilePath;
        }

        public string GetIniString(string Section, string Key)
        {
			lock (syncRoot)
			{
				Load();
				if (sections != null && Section != null && Key != null
					&& sections.TryGetValue(Section.Trim(), out Dictionary<string, string> values)
					&& values.TryGetValue(Key.Trim(), out string value))
				{
					return value;
				}
				return "";
			}
        }

        private void Load()
        {
			if (!File.Exists(FilePath))
			{
				sections = null;
				return;
			}
			DateTime writeTime = File.GetLastWriteTimeUtc(FilePath);
			if (sections != null && writeTime == loadedWriteTime)
			{
				return;
			}
			Dictionary<string, Dictionary<string, string>> parsed = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> current = null;
			foreach (string rawLine in File.ReadAllLines(FilePath))
			{
				string line = rawLine.Trim();
				if (line.Length == 0 || line[0] == ';' || line[0] == '#')
				{
					continue;
				}
				if (line[0] == '[')
				{
					int end = line.IndexOf(']');
					string name = (end > 0 ? line.Substring(1, end - 1) : line.Substring(1)).Trim();
					if (!parsed.TryGetValue(name, out current))
					{
						current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
						parsed[name] = current;
					}
					continue;
				}
				int eq = line.IndexOf('=');
				if (current == null || eq <= 0)
				{
					continue;
				}
				string key = line.Substring(0, eq).Trim();
				string value = line.Substring(eq + 1).Trim();
				if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[value.Length - 1] == value[0])
				{
					value = value.Substring(1, value.Length - 2);
				}
				if (!current.ContainsKey(key))
				{
					current[key] = value;
				}
			}
			sections = parsed;
			loadedWriteTime = writeTime;
        }
    }
}
