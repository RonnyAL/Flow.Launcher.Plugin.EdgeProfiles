using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.EdgeProfiles
{
    /// <summary>
    /// Provides helper methods for window operations.
    /// </summary>
    public static class WindowHelper
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int SW_RESTORE = 9;
        private const int SW_SHOWMAXIMIZED = 3;
        private const int SW_SHOWMINIMIZED = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        /// <summary>
        /// Finds a window with the specified title pattern.
        /// </summary>
        /// <param name="windowTitlePattern">The title pattern of the window to find.</param>
        /// <returns>The handle to the window if found; otherwise, IntPtr.Zero.</returns>
        public static IntPtr FindWindow(string windowTitlePattern)
        {
            IntPtr foundWindow = IntPtr.Zero;
            string pattern = $@"^(.*–\s*)?{Regex.Escape(windowTitlePattern)}\s*–\s*Microsoft([^\x20-\x7E]|\?)?\s*Edge$";

            EnumWindows((hWnd, lParam) =>
            {
                int length = GetWindowTextLength(hWnd);
                if (length == 0) return true;

                StringBuilder builder = new StringBuilder(length);
                GetWindowText(hWnd, builder, length + 1);

                string windowTitle = builder.ToString();

                if (Regex.IsMatch(windowTitle, pattern, RegexOptions.IgnoreCase))
                {
                    foundWindow = hWnd;
                    return false; // Stop enumeration
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            return foundWindow;
        }

        /// <summary>
        /// Restores or maximizes the specified window.
        /// </summary>
        /// <param name="hWnd">The handle to the window.</param>
        public static void RestoreOrMaximizeWindow(IntPtr hWnd)
        {
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            GetWindowPlacement(hWnd, ref placement);

            if (placement.showCmd == SW_SHOWMINIMIZED)
            {
                ShowWindow(hWnd, placement.flags == SW_SHOWMAXIMIZED ? SW_SHOWMAXIMIZED : SW_RESTORE); // Restore the window to its previous state
            }

            SetForegroundWindow(hWnd);
        }
    }
}