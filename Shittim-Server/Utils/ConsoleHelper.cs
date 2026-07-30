using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Shittim.Utils
{
    /// <summary>
    /// Stops two Windows console behaviours from freezing the server. QuickEdit mode, on by default
    /// for a direct cmd / autorun.ps1 launch, puts the window into selection mode on a stray click
    /// and pauses console output, blocking every ASP.NET request thread that logs through
    /// <c>Console.WriteLine</c> until someone presses Escape. Separately, when the process is spawned
    /// by the GUI launcher its stdout is a ~4 KB pipe buffer, and a consumer that cannot drain it
    /// fast enough blocks the caller inside <c>Console.Out.Write</c>; an
    /// <see cref="AsyncConsoleWriter"/> moves that write onto a background channel.
    /// </summary>
    public static class ConsoleHelper
    {
        // Win32 constants
        private const int STD_INPUT_HANDLE = -10;
        private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        private const uint ENABLE_EXTENDED_FLAGS = 0x0080;

        // Win32 imports
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

        // QuickEdit

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
                // Not fatal - swallow (e.g. running without a console at all).
            }
        }

        // Async Console.Out

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
                // Not fatal - keep default Console.Out.
            }
        }
    }
}
