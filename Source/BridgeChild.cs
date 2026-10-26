using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
class BridgeChild {
    [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
    static void Main() {
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"bridge_child.txt"),
            "console="+GetConsoleWindow().ToInt64()+Environment.NewLine+"path="+Process.GetCurrentProcess().MainModule.FileName);
    }
}
