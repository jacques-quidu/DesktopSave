using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using DesktopSave.Models;

namespace DesktopSave.Services
{
    /// <summary>
    /// Lit et écrit la position des icônes du bureau Windows via le ListView du Shell.
    /// </summary>
    internal static class DesktopIconService
    {
        private const int LVM_FIRST = 0x1000;
        private const int LVM_GETITEMCOUNT = LVM_FIRST + 4;
        private const int LVM_SETITEMPOSITION = LVM_FIRST + 15;
        private const int LVM_GETITEMPOSITION = LVM_FIRST + 16;
        private const int LVM_GETITEMTEXTW = LVM_FIRST + 115;
        private const int LVM_REDRAWITEMS = LVM_FIRST + 21;

        private const int LVIF_TEXT = 0x0001;

        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 0x04;

        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LVITEM
        {
            public uint mask;
            public int iItem;
            public int iSubItem;
            public uint state;
            public uint stateMask;
            public IntPtr pszText;
            public int cchTextMax;
            public int iImage;
            public IntPtr lParam;
            public int iIndent;
            public int iGroupId;
            public uint cColumns;
            public IntPtr puColumns;
            public IntPtr piColFmt;
            public int iGroup;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesRead);

        /// <summary>
        /// Retourne le handle du ListView contenant les icônes du bureau.
        /// </summary>
        private static IntPtr GetDesktopListViewHandle()
        {
            IntPtr progman = FindWindow("Progman", null);
            IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

            if (defView == IntPtr.Zero)
            {
                // Avec le diaporama de fond d'écran actif, SHELLDLL_DefView est hébergé par un WorkerW.
                IntPtr found = IntPtr.Zero;
                EnumWindows((hWnd, _) =>
                {
                    IntPtr candidate = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (candidate != IntPtr.Zero)
                    {
                        found = candidate;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
                defView = found;
            }

            if (defView == IntPtr.Zero)
            {
                throw new InvalidOperationException("Impossible de localiser le bureau Windows (SHELLDLL_DefView).");
            }

            IntPtr listView = FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
            if (listView == IntPtr.Zero)
            {
                throw new InvalidOperationException("Impossible de localiser la liste des icônes du bureau (SysListView32).");
            }

            return listView;
        }

        /// <summary>
        /// Lit la disposition actuelle des icônes du bureau.
        /// </summary>
        public static List<DesktopIcon> ReadIcons()
        {
            IntPtr listView = GetDesktopListViewHandle();
            int count = (int)SendMessage(listView, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            var icons = new List<DesktopIcon>(Math.Max(count, 0));
            if (count <= 0)
            {
                return icons;
            }

            using var memory = new RemoteMemory(listView);
            for (int i = 0; i < count; i++)
            {
                string name = memory.GetItemText(i);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                POINT position = memory.GetItemPosition(i);
                icons.Add(new DesktopIcon { Name = name, X = position.X, Y = position.Y });
            }

            return icons;
        }

        /// <summary>
        /// Applique une disposition sauvegardée. Retourne le nombre d'icônes repositionnées.
        /// </summary>
        public static int ApplyIcons(IReadOnlyList<DesktopIcon> icons)
        {
            IntPtr listView = GetDesktopListViewHandle();
            int count = (int)SendMessage(listView, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            if (count <= 0)
            {
                return 0;
            }

            var wanted = new Dictionary<string, DesktopIcon>(StringComparer.OrdinalIgnoreCase);
            foreach (DesktopIcon icon in icons)
            {
                wanted[icon.Name] = icon;
            }

            int applied = 0;
            using (var memory = new RemoteMemory(listView))
            {
                for (int i = 0; i < count; i++)
                {
                    string name = memory.GetItemText(i);
                    if (string.IsNullOrEmpty(name) || !wanted.TryGetValue(name, out DesktopIcon? target))
                    {
                        continue;
                    }

                    SendMessage(listView, LVM_SETITEMPOSITION, (IntPtr)i, MakeLParam(target.X, target.Y));
                    applied++;
                }
            }

            SendMessage(listView, LVM_REDRAWITEMS, IntPtr.Zero, (IntPtr)(count - 1));
            return applied;
        }

        private static IntPtr MakeLParam(int x, int y)
            => (IntPtr)((y << 16) | (x & 0xFFFF));

        /// <summary>
        /// Tampon alloué dans le processus explorer.exe, nécessaire pour les messages LVM qui échangent des pointeurs.
        /// </summary>
        private sealed class RemoteMemory : IDisposable
        {
            private const int TextCapacity = 520; // 260 caractères Unicode

            private readonly IntPtr _listView;
            private readonly IntPtr _process;
            private readonly IntPtr _remoteBuffer;
            private readonly int _bufferSize;
            private bool _disposed;

            public RemoteMemory(IntPtr listView)
            {
                _listView = listView;
                GetWindowThreadProcessId(listView, out uint processId);

                _process = OpenProcess(
                    PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_QUERY_INFORMATION,
                    false,
                    processId);

                if (_process == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Impossible d'accéder au processus du bureau (explorer.exe).");
                }

                _bufferSize = Marshal.SizeOf<LVITEM>() + TextCapacity;
                _remoteBuffer = VirtualAllocEx(_process, IntPtr.Zero, (IntPtr)_bufferSize, MEM_COMMIT, PAGE_READWRITE);
                if (_remoteBuffer == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    CloseHandle(_process);
                    throw new Win32Exception(error, "Impossible d'allouer la mémoire dans le processus du bureau.");
                }
            }

            public POINT GetItemPosition(int index)
            {
                SendMessage(_listView, LVM_GETITEMPOSITION, (IntPtr)index, _remoteBuffer);

                int size = Marshal.SizeOf<POINT>();
                IntPtr local = Marshal.AllocHGlobal(size);
                try
                {
                    if (!ReadProcessMemory(_process, _remoteBuffer, local, (IntPtr)size, out _))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Lecture de la position de l'icône impossible.");
                    }

                    return Marshal.PtrToStructure<POINT>(local);
                }
                finally
                {
                    Marshal.FreeHGlobal(local);
                }
            }

            public string GetItemText(int index)
            {
                int itemSize = Marshal.SizeOf<LVITEM>();
                IntPtr remoteText = _remoteBuffer + itemSize;

                var item = new LVITEM
                {
                    mask = LVIF_TEXT,
                    iItem = index,
                    iSubItem = 0,
                    pszText = remoteText,
                    cchTextMax = TextCapacity / 2,
                };

                IntPtr localItem = Marshal.AllocHGlobal(itemSize);
                IntPtr localText = Marshal.AllocHGlobal(TextCapacity);
                try
                {
                    Marshal.StructureToPtr(item, localItem, false);
                    if (!WriteProcessMemory(_process, _remoteBuffer, localItem, (IntPtr)itemSize, out _))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Écriture dans la mémoire du bureau impossible.");
                    }

                    int length = (int)SendMessage(_listView, LVM_GETITEMTEXTW, (IntPtr)index, _remoteBuffer);
                    if (length <= 0)
                    {
                        return string.Empty;
                    }

                    if (!ReadProcessMemory(_process, remoteText, localText, (IntPtr)TextCapacity, out _))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Lecture du nom de l'icône impossible.");
                    }

                    int maxChars = Math.Min(length, (TextCapacity / 2) - 1);
                    return Marshal.PtrToStringUni(localText, maxChars) ?? string.Empty;
                }
                finally
                {
                    Marshal.FreeHGlobal(localItem);
                    Marshal.FreeHGlobal(localText);
                }
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (_remoteBuffer != IntPtr.Zero)
                {
                    VirtualFreeEx(_process, _remoteBuffer, IntPtr.Zero, MEM_RELEASE);
                }

                if (_process != IntPtr.Zero)
                {
                    CloseHandle(_process);
                }
            }
        }
    }
}
