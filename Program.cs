using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;

namespace LPTBlock
{
    static class Program
    {
        #region hook
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                return (IntPtr)1;
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }        
        #endregion

        #region LPT
        [DllImport("WinIo.dll")]
        public static extern bool InitializeWinIo();
        [DllImport("WinIo.dll")]
        public static extern bool GetPortVal(Int16 wPortAddr, Int32[] pdwPortVal, byte bSize);
        [DllImport("WinIo.dll")]
        public static extern bool SetPortVal(Int16 wPortAddr, Int32 dwPortVal, byte bSize);
        [DllImport("WinIo.dll")]
        public static extern void ShutdownWinIo();
        static int Read(int register, int mask)
        {
            mask = mask & 0x000000ff;
            Int16 port_addr = (Int16)(0x378 + register);
            int[] buf = new int[] { 0 };
            if (!GetPortVal(port_addr, buf, 4))
                throw new Exception("Ошибка чтения");
            return buf[0] & mask;
        }
        static void Write(int register, int mask, int value)
        {
            mask = mask & 0x000000ff;
            int val = Read(register, ~0);
            Int16 port_addr = (Int16)(0x378 + register);
            if (!SetPortVal(port_addr, val & ~mask | value & mask, 4))
                throw new Exception("Ошибка записи");
        }
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (!InitializeWinIo())
            {
                MessageBox.Show("Порт не проинициализирован");
            }
            Write(2, 1 << 3, 0); // рег. упр., SelectIn#
            Form1 f = new Form1();
            f.label3.Text = string.Format("тумблер1:{0:x}, тумблер2:{1:x}, тумблер3:{2:x}, тумблер4:{3:x}, тумблер5:{4:x}",
                Read(1, 1 << 6) > 0 ? 1 : 0,
                Read(1, 1 << 7) > 0 ? 0 : 1,
                Read(1, 1 << 5) > 0 ? 1 : 0,
                Read(1, 1 << 4) > 0 ? 1 : 0,
                Read(1, 1 << 3) > 0 ? 1 : 0);
            f.ShowDialog();
            int code = int.Parse(f.textBox1.Text), value;
            _hookID = SetHook(_proc);
            MessageBox.Show(string.Format("Заблокировано. Код: {0}", code));
            do
            {
                Thread.Sleep(50);
                value = Read(1, 1 << 6) > 0 ? 16 : 0;   // тумблер1 - рег. сост., Ack#
                value += Read(1, 1 << 7) > 0 ? 0 : 8;   // тумблер2 - рег. сост., Busy
                value += Read(1, 1 << 5) > 0 ? 4 : 0;   // тумблер3 - рег. сост., Paper
                value += Read(1, 1 << 4) > 0 ? 2 : 0;   // тумблер4 - рег. сост., Select
                value += Read(1, 1 << 3) > 0 ? 1 : 0;   // тумблер5 - рег. сост., Error#
                Application.DoEvents();
            }
            while (value != code);
            MessageBox.Show("Разблокировано");
            UnhookWindowsHookEx(_hookID);
            Write(2, 1 << 2, 0);    // диод - рег. упр., Init#
            Thread.Sleep(2000);
            Write(2, 1 << 2, ~0);
            Thread.Sleep(2000);
            ShutdownWinIo();
        }        
        #endregion
    }
}
