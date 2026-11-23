using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Game.Base
{
    /// <summary>
    /// Cross-platform helpers for the console front-ends (Road/Center/Fighting.Service).
    /// Replaces the kernel32 SetConsoleCtrlHandler P/Invoke with POSIX signal handling,
    /// and supports running headless (no stdin, e.g. under systemd or docker).
    /// </summary>
    public static class ConsoleHost
    {
        private static readonly List<PosixSignalRegistration> registrations = new List<PosixSignalRegistration>();

        private static int stopping;

        /// <summary>
        /// Runs <paramref name="onShutdown"/> once on SIGINT (Ctrl+C), SIGTERM or SIGQUIT, then exits the process.
        /// </summary>
        public static void RegisterShutdown(Action onShutdown)
        {
            lock (registrations)
            {
                if (registrations.Count > 0)
                {
                    return;
                }
                foreach (PosixSignal signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGTERM, PosixSignal.SIGQUIT })
                {
                    registrations.Add(PosixSignalRegistration.Create(signal, context =>
                    {
                        context.Cancel = true;
                        Shutdown(onShutdown);
                    }));
                }
            }
        }

        private static void Shutdown(Action onShutdown)
        {
            if (Interlocked.Exchange(ref stopping, 1) != 0)
            {
                return;
            }
            Console.WriteLine("Shutdown signal received, stopping server...");
            try
            {
                onShutdown?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during shutdown: " + ex);
            }
            Environment.Exit(0);
        }

        /// <summary>
        /// Console.ReadLine that never returns null. When stdin is closed (headless), it blocks
        /// forever; the process is then stopped through a signal (see <see cref="RegisterShutdown"/>).
        /// </summary>
        public static string ReadLine()
        {
            string line = Console.ReadLine();
            if (line != null)
            {
                return line;
            }
            Console.WriteLine("stdin closed: running headless. Send SIGTERM/SIGINT to stop.");
            Thread.Sleep(Timeout.Infinite);
            return string.Empty;
        }

        public static void SetTitle(string title)
        {
            try
            {
                if (!Console.IsOutputRedirected)
                {
                    Console.Title = title;
                }
            }
            catch
            {
            }
        }
    }
}
