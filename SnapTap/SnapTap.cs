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
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        private static IntPtr _hookID = IntPtr.Zero;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static bool _isEnabled = true;
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;

        // Track physical key states
        private static readonly HashSet<Keys> PhysicallyPressedKeys = new HashSet<Keys>();
        
        // Track active keys per group
        private static readonly Dictionary<int, Keys> ActiveKeys = new Dictionary<int, Keys>();
        
        // Track previous keys per group for instant switching
        private static readonly Dictionary<int, Keys> PreviousKeys = new Dictionary<int, Keys>();

        private static readonly Dictionary<Keys, int> KeyGroups = new Dictionary<Keys, int>
        {
            { Keys.A, 1 },
            { Keys.D, 1 },
            { Keys.W, 2 },
            { Keys.S, 2 }
        };

        // List of game window class names (can be expanded)
        private static readonly HashSet<string> GameWindowClasses = new HashSet<string>
        {
            "UnityWndClass",    // Unity games
            "Valve001",         // Source engine games
            "UnrealWindow",     // Unreal engine games
            "SDL_app"           // SDL-based games
        };

        private static bool _inChatMode = false;

        public SnapTap()
        {
            InitializeComponent();
            InitializeTrayIcon();
            _hookID = SetHook(_proc);
            
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();
        }

        private void InitializeTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Enabled", null, OnToggleEnabled);
            _trayMenu.Items.Add("-"); // Separator
            _trayMenu.Items.Add("Exit", null, OnExit);
            
            _trayIcon = new NotifyIcon
            {
                Text = "SnapTap",
                Icon = SystemIcons.Application,
                ContextMenuStrip = _trayMenu,
                Visible = true
            };

            (_trayMenu.Items[0] as ToolStripMenuItem).Checked = true;
        }

        private void OnToggleEnabled(object sender, EventArgs e)
        {
            _isEnabled = !_isEnabled;
            (_trayMenu.Items[0] as ToolStripMenuItem).Checked = _isEnabled;
            
            if (!_isEnabled)
            {
                // When disabling, release all virtual key states
                foreach (var key in PhysicallyPressedKeys)
                {
                    if (KeyGroups.ContainsKey(key))
                    {
                        SimulateKey(key, false);
                    }
                }
                PhysicallyPressedKeys.Clear();
                ActiveKeys.Clear();
                PreviousKeys.Clear();
            }
        }

        private static bool IsGameWindowActive()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero) return false;

            // Get the class name of the foreground window
            StringBuilder className = new StringBuilder(256);
            if (GetClassName(foregroundWindow, className, className.Capacity) == 0)
                return false;

            return GameWindowClasses.Contains(className.ToString());
        }

        private void OnExit(object sender, EventArgs e)
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
            }
            
            _trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
            }
            else
            {
                base.OnFormClosing(e);
            }
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
            if (nCode >= 0 && _isEnabled && IsGameWindowActive())
            {
                var kbStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                var key = (Keys)kbStruct.vkCode;
                bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN;

                // Check for chat mode toggle
                if (isKeyDown)
                {
                    if (key == Keys.Y)
                    {
                        _inChatMode = true;
                        return CallNextHookEx(_hookID, nCode, wParam, lParam);
                    }
                    else if (key == Keys.Enter && _inChatMode)
                    {
                        _inChatMode = false;
                        return CallNextHookEx(_hookID, nCode, wParam, lParam);
                    }
                }

                // If in chat mode, let all keys pass through normally
                if (_inChatMode)
                {
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                // Only handle if it's not a simulated key event
                if ((kbStruct.flags & 0x10) == 0)
                {
                    // Check if this is a key we care about
                    if (KeyGroups.TryGetValue(key, out int group))
                    {
                        bool isKeyUp = wParam == (IntPtr)WM_KEYUP;

                        if (isKeyDown)
                        {
                            var currentKeyInfo = PhysicallyPressedKeys;
                            var currentGroupInfo = ActiveKeys;

                            if (!currentKeyInfo.Contains(key))
                            {
                                currentKeyInfo.Add(key);

                                if (currentGroupInfo.TryGetValue(group, out Keys activeKey))
                                {
                                    if (activeKey != key)
                                    {
                                        // Store previous key
                                        PreviousKeys[group] = activeKey;
                                        // Release previous key
                                        PostMessage(GetForegroundWindow(), WM_KEYUP, (IntPtr)((int)activeKey), IntPtr.Zero);
                                    }
                                }

                                // Set as active key
                                currentGroupInfo[group] = key;
                                // Send the key down
                                PostMessage(GetForegroundWindow(), WM_KEYDOWN, (IntPtr)((int)key), IntPtr.Zero);
                            }
                            return (IntPtr)1; // Block original keydown
                        }
                        else if (isKeyUp)
                        {
                            if (PhysicallyPressedKeys.Contains(key))
                            {
                                PhysicallyPressedKeys.Remove(key);

                                if (ActiveKeys.TryGetValue(group, out Keys activeKey) && activeKey == key)
                                {
                                    // Send key up
                                    PostMessage(GetForegroundWindow(), WM_KEYUP, (IntPtr)((int)key), IntPtr.Zero);

                                    if (PreviousKeys.TryGetValue(group, out Keys prevKey) && 
                                        PhysicallyPressedKeys.Contains(prevKey))
                                    {
                                        // Activate previous key
                                        ActiveKeys[group] = prevKey;
                                        PreviousKeys.Remove(group);
                                        PostMessage(GetForegroundWindow(), WM_KEYDOWN, (IntPtr)((int)prevKey), IntPtr.Zero);
                                    }
                                    else
                                    {
                                        ActiveKeys.Remove(group);
                                    }
                                }
                            }
                            return (IntPtr)1; // Block original keyup
                        }
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static void SendKey(Keys key, bool keyDown)
        {
            var input = new INPUT
            {
                type = 1u,
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)key,
                    wScan = (ushort)MapVirtualKey((uint)key, 0),
                    dwFlags = keyDown ? 0u : KEYEVENTF_KEYUP,
                    time = 0u,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            SendInput(1u, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        private static void SimulateKey(Keys key, bool keyDown)
        {
            var input = new INPUT
            {
                type = 1u,
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)key,
                    wScan = 0,
                    dwFlags = keyDown ? 0u : 0x0002u,
                    time = 0u,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            SendInput(1u, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        private static bool IsTextInputActive()
        {
            IntPtr focusedWindow = GetFocus();
            if (focusedWindow != IntPtr.Zero)
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(focusedWindow, className, className.Capacity);
                string classNameStr = className.ToString().ToLower();
                
                // Common text input class names
                return classNameStr.Contains("edit") || 
                       classNameStr.Contains("text") || 
                       classNameStr.Contains("input") ||
                       classNameStr.Contains("richedit");
            }
            return false;
        }

        private static bool IsGameChatActive()
        {
            // Check if any of these keys are pressed (common chat activation keys)
            bool chatKeyPressed = 
                (GetAsyncKeyState((int)Keys.Enter) & 0x8000) != 0 ||
                (GetAsyncKeyState((int)Keys.T) & 0x8000) != 0 ||
                (GetAsyncKeyState((int)Keys.Y) & 0x8000) != 0;

            return chatKeyPressed || IsTextInputActive();
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }
}
