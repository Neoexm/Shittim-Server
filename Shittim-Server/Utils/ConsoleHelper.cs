using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Shittim.Utils
{
    /// <summary>
    /// Prevents the server from freezing due to Windows console behaviour.
    /// 
    /// Two independent problems are addressed:
    /// 
    /// 1. **QuickEdit mode** (direct console launch via cmd / autorun.ps1)
    ///    Windows enables QuickEdit by default.  An accidental click on the
    ///    console window puts it into *selection* mode, which **pauses all
    ///    console output**.  Because ASP.NET request-handling threads write
    ///    log lines through <c>Console.WriteLine</c>, every request-handling
    ///    thread blocks until the user presses Escape – freezing the server.
    ///
    /// 2. **Stdout pipe buffer saturation** (GUI launcher / piped output)
    ///    When the server is spawned as a child process whose stdout is a
    ///    pipe (e.g. the Shittim Console GUI), the OS pipe buffer is only
    ///    ~4 KB on Windows.  If the consumer cannot drain the pipe quickly
    ///    enough, <c>Console.Out.Write</c> blocks the calling thread.
    ///    Replacing <c>Console.Out</c> with an <see cref="AsyncConsoleWriter"/>
    ///    that enqueues lines onto a background channel eliminates the risk
    ///    of request-handling threads blocking on pipe writes.
    /// </summary>
    public static class ConsoleHelper
    {
        // ── Win32 constants ────────────────────────────────────────────
        private const int STD_INPUT_HANDLE = -10;
        private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        private const uint ENABLE_EXTENDED_FLAGS = 0x0080;

        // ── Win32 imports ──────────────────────────────────────────────
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        /// <summary>
        /// Applies all console hardening in a single call.
        /// Safe to call on any OS; non-Windows platforms are silently skipped.
        /// </summary>
        public static void Harden()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DisableQuickEditMode();
            }

            InstallAsyncConsoleWriter();
        }

        // ── QuickEdit ──────────────────────────────────────────────────

        [SupportedOSPlatform("windows")]
        private static void DisableQuickEditMode()
        {
            try
            {
                var handle = GetStdHandle(STD_INPUT_HANDLE);
                if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                    return;

                if (!GetConsoleMode(handle, out uint mode))
                    return;

                mode &= ~ENABLE_QUICK_EDIT_MODE;  // turn off QuickEdit
                mode |= ENABLE_EXTENDED_FLAGS;     // required for the change to take effect
                SetConsoleMode(handle, mode);
            }
            catch
            {
                // Not fatal – swallow (e.g. running without a console at all).
            }
        }

        // ── Async Console.Out ──────────────────────────────────────────

        private static void InstallAsyncConsoleWriter()
        {
            try
            {
                var original = Console.Out;
                var asyncWriter = new AsyncConsoleWriter(original);
                Console.SetOut(asyncWriter);
            }
            catch
            {
                // Not fatal – keep default Console.Out.
            }
        }
    }
}
