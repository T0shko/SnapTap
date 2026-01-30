using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;
using System.Text;

namespace SnapTap
{
    public partial class SnapTap : Form
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x100;
        private const int WM_KEYUP = 0x101;
        private const int WM_SYSKEYDOWN = 0x104;
        private const int WM_SYSKEYUP = 0x105;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint LLKHF_INJECTED = 0x00000010;

        private static IntPtr _hookID = IntPtr.Zero;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static bool _isEnabled = true;
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;

        // Peak Performance: Use fixed-size storage to avoid heap allocations in the hook
        private static readonly Keys[][] GroupStacks = {
            new Keys[4], // Group 1 (A, D, etc)
            new Keys[4]  // Group 2 (W, S, etc)
        };
        private static readonly int[] GroupCounts = { 0, 0 };

        private static readonly Dictionary<Keys, int> KeyGroups = new Dictionary<Keys, int>
        {
            { Keys.A, 0 }, { Keys.D, 0 },
            { Keys.W, 1 }, { Keys.S, 1 }
        };

        private static readonly HashSet<string> GameWindowClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "UnityWndClass", "Valve001", "UnrealWindow", "SDL_app", "GLFW30", "R5Apex", "Overwatch", "Ghost_Window"
        };

        private static bool _inChatMode = false;
        private static IntPtr _lastForegroundWindow = IntPtr.Zero;
        private static bool _isTargetActive = false;

        public SnapTap()
        {
            // Boost process priority to ensure the hook is serviced immediately by Windows
            using (Process p = Process.GetCurrentProcess())
            {
                p.PriorityClass = ProcessPriorityClass.High;
            }

            InitializeComponent();
            InitializeTrayIcon();
            _hookID = SetHook(_proc);

            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }

        private void InitializeTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();
            var enabledItem = new ToolStripMenuItem("Enabled", null, OnToggleEnabled) { Checked = true };
            _trayMenu.Items.Add(enabledItem);
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("Exit", null, OnExit);

            _trayIcon = new NotifyIcon
            {
                Text = "SnapTap - Ultra Low Latency",
                Icon = SystemIcons.Application,
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
        }

        private void OnToggleEnabled(object sender, EventArgs e)
        {
            _isEnabled = !_isEnabled;
            ((ToolStripMenuItem)sender).Checked = _isEnabled;
            if (!_isEnabled) ResetAllKeys();
        }

        private static void ResetAllKeys()
        {
            for (int i = 0; i < GroupStacks.Length; i++)
            {
                for (int j = 0; j < GroupCounts[i]; j++)
                {
                    SimulateKey(GroupStacks[i][j], false);
                }
                GroupCounts[i] = 0;
            }
            _inChatMode = false;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static bool IsGameWindowActive()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;

            // Optimization: Only perform string lookups if the window actually changed
            if (fg != _lastForegroundWindow)
            {
                _lastForegroundWindow = fg;
                StringBuilder className = new StringBuilder(256);
                if (GetClassName(fg, className, className.Capacity) != 0)
                {
                    string cls = className.ToString();
                    _isTargetActive = GameWindowClasses.Contains(cls);

                    if (!_isTargetActive && (cls == "Window" || cls.Contains("Render")))
                    {
                        StringBuilder title = new StringBuilder(256);
                        GetWindowText(fg, title, title.Capacity);
                        string t = title.ToString().ToLower();
                        _isTargetActive = t.Contains("counter-strike") || t.Contains("valorant") || t.Contains("overwatch");
                    }
                }
                else
                {
                    _isTargetActive = false;
                }
            }
            return _isTargetActive;
        }

        private void OnExit(object sender, EventArgs e)
        {
            ResetAllKeys();
            if (_hookID != IntPtr.Zero) UnhookWindowsHookEx(_hookID);
            _trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!this.IsHandleCreated) { this.CreateHandle(); value = false; }
            base.SetVisibleCore(value);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isEnabled)
            {
                var kbStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                if ((kbStruct.flags & LLKHF_INJECTED) == 0 && IsGameWindowActive())
                {
                    var key = (Keys)kbStruct.vkCode;
                    bool isKD = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                    bool isKU = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

                    // Faster Chat Detection
                    if (isKD)
                    {
                        switch (key)
                        {
                            case Keys.Enter: _inChatMode = !_inChatMode; break;
                            case Keys.Escape: _inChatMode = false; break;
                            case Keys.T: case Keys.Y: case Keys.OemQuestion: _inChatMode = true; break;
                        }
                    }

                    if (!_inChatMode && KeyGroups.TryGetValue(key, out int gIdx))
                    {
                        Keys[] stack = GroupStacks[gIdx];
                        int count = GroupCounts[gIdx];

                        if (isKD)
                        {
                            // Optimized: Check if key is already in our virtual stack
                            bool alreadyHeld = false;
                            for (int i = 0; i < count; i++) if (stack[i] == key) { alreadyHeld = true; break; }

                            if (!alreadyHeld)
                            {
                                if (count > 0) SimulateKey(stack[count - 1], false);
                                stack[count++] = key;
                                GroupCounts[gIdx] = count;
                                return CallNextHookEx(_hookID, nCode, wParam, lParam);
                            }
                            return (IntPtr)1; // Suppress repeat
                        }
                        else if (isKU)
                        {
                            int foundIdx = -1;
                            for (int i = 0; i < count; i++) if (stack[i] == key) { foundIdx = i; break; }

                            if (foundIdx != -1)
                            {
                                Keys activeBefore = stack[count - 1];
                                // Remove by shifting
                                for (int i = foundIdx; i < count - 1; i++) stack[i] = stack[i + 1];
                                count--;
                                GroupCounts[gIdx] = count;

                                if (key == activeBefore)
                                {
                                    IntPtr res = CallNextHookEx(_hookID, nCode, wParam, lParam);
                                    if (count > 0) SimulateKey(stack[count - 1], true);
                                    return res;
                                }
                                return (IntPtr)1;
                            }
                        }
                    }
                }
                else if (GroupCounts[0] > 0 || GroupCounts[1] > 0)
                {
                    // Instant cleanup when window lost focus
                    ResetAllKeys();
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static void SimulateKey(Keys key, bool keyDown)
        {
            INPUT input = new INPUT { type = 1u };
            input.ki.wVk = (ushort)key;
            input.ki.wScan = (ushort)MapVirtualKey((uint)key, 0);
            input.ki.dwFlags = keyDown ? 0u : KEYEVENTF_KEYUP;
            SendInput(1u, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] private struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }
    }
}