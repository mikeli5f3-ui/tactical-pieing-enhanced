using System;
using System.Runtime.InteropServices;
class BridgeTest {
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryW(string path);
    [DllImport("kernel32.dll",CharSet=CharSet.Ansi,SetLastError=true)] static extern IntPtr GetProcAddress(IntPtr h,string name);
    [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr h);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Entry(IntPtr lua);
    static int Main(string[] args) {
        IntPtr h=LoadLibraryW(args[0]);
        if(h==IntPtr.Zero) { Console.WriteLine("LoadLibrary failed "+Marshal.GetLastWin32Error()); return 1; }
        try {
            IntPtr proc=GetProcAddress(h,"luaopen_MovingLeanBridge");
            if(proc==IntPtr.Zero) throw new Exception("Export missing");
            int result=((Entry)Marshal.GetDelegateForFunctionPointer(proc,typeof(Entry)))(IntPtr.Zero);
            Console.WriteLine("Bridge loaded, native launch entry returned "+result);
            return result;
        } finally { FreeLibrary(h); }
    }
}
