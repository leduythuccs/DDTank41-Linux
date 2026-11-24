using log4net;

namespace SqlDataProvider.BaseClass
{
    /// <summary>Database errors. They went to the Windows event log, which does not exist on Linux: log4net instead.</summary>
    public static class ApplicationLog
    {
        private static readonly ILog log = LogManager.GetLogger("SqlDataProvider");

        public static void WriteError(string message)
        {
            log.Error(message);
        }
    }
}
