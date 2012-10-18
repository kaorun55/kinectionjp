using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace training10_MonogusaMouse
{
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    internal struct INPUT
    {
        public int type;
        public MOUSEINPUT mi;
    }

    public static class NativeWrapper
    {
        public const int INPUT_MOUSE = 0;
        public const int MOUSEEVENTF_MOVE = 0x01;

        [DllImport( "user32.dll", SetLastError = true )]
        private static extern uint SendInput( uint nInputs, INPUT[] pInputs,
                                             int cbSize );

        public static int sendMouseMove( int moveX, int moveY )
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0] = new INPUT();
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dx = moveX;
            inputs[0].mi.dy = moveY;
            inputs[0].mi.mouseData = 0;
            inputs[0].mi.dwFlags = MOUSEEVENTF_MOVE;
            inputs[0].mi.time = 0;
            inputs[0].mi.dwExtraInfo = IntPtr.Zero;
            uint result = SendInput( 1, inputs, Marshal.SizeOf( inputs[0] ) );
            return result == 0 ? Marshal.GetLastWin32Error() : 0;
        }
    }
}
