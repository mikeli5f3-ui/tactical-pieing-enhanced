// Build with the installed .NET Framework csc, /platform:x64.
// Patches process memory only. Never writes the game executable on disk.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

class Native {
    [DllImport("kernel32.dll", SetLastError=true)] public static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct ProcessEntry {
        public uint size, usage, pid; public UIntPtr heap; public uint module, threads, parent; public int priority; public uint flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string name;
    }
    [DllImport("kernel32.dll", SetLastError=true)] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr address, UIntPtr size, uint allocation, uint protection);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool VirtualFreeEx(IntPtr h, IntPtr address, UIntPtr size, uint kind);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool ReadProcessMemory(IntPtr h, IntPtr address, byte[] data, UIntPtr size, out UIntPtr count);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool WriteProcessMemory(IntPtr h, IntPtr address, byte[] data, UIntPtr size, out UIntPtr count);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool VirtualProtectEx(IntPtr h, IntPtr address, UIntPtr size, uint protection, out uint old);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool FlushInstructionCache(IntPtr h, IntPtr address, UIntPtr size);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern IntPtr OpenThread(uint access, bool inherit, uint tid);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern uint SuspendThread(IntPtr h);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern uint ResumeThread(IntPtr h);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    public static Exception Error(string message) { return new Exception(message + " Win32=" + Marshal.GetLastWin32Error()); }
    public static byte[] Read(IntPtr h, long addr, int length) {
        byte[] data = new byte[length]; UIntPtr n;
        if (!ReadProcessMemory(h, new IntPtr(addr), data, (UIntPtr)length, out n) || n.ToUInt64() != (ulong)length) throw Error("Read failed");
        return data;
    }
    public static void Write(IntPtr h, long addr, byte[] data) {
        UIntPtr n;
        if (!WriteProcessMemory(h, new IntPtr(addr), data, (UIntPtr)data.Length, out n) || n.ToUInt64() != (ulong)data.Length) throw Error("Write failed");
    }
    public static bool Equal(byte[] a, byte[] b) {
        if (a.Length != b.Length) return false;
        for (int i=0;i<a.Length;i++) if (a[i]!=b[i]) return false;
        return true;
    }
}
class Site {
    public long Offset; public byte[] Original; public bool Negative;
    public Site(long offset, string bytes, bool negative) {
        Offset=offset; Negative=negative;
        string[] parts=bytes.Split(' '); Original=new byte[parts.Length];
        for(int i=0;i<parts.Length;i++) Original[i]=Convert.ToByte(parts[i],16);
    }
}
sealed class Frozen : IDisposable {
    List<IntPtr> handles = new List<IntPtr>();
    public Frozen(Process p) {
        try {
            p.Refresh();
            foreach(ProcessThread t in p.Threads) {
                IntPtr h=Native.OpenThread(0x0002, false, (uint)t.Id);
                if(h==IntPtr.Zero) throw Native.Error("OpenThread failed");
                if(Native.SuspendThread(h)==UInt32.MaxValue) { Native.CloseHandle(h); throw Native.Error("SuspendThread failed"); }
                handles.Add(h);
            }
            if(handles.Count==0) throw new Exception("No threads");
        } catch { Dispose(); throw; }
    }
    public void Dispose() { for(int i=handles.Count-1;i>=0;i--) { Native.ResumeThread(handles[i]); Native.CloseHandle(handles[i]); } handles.Clear(); }
}
sealed class PatchSession : IDisposable {
    Process process; IntPtr handle; long image; Site[] sites; long data; byte[][] patched;
    public bool Active { get; private set; }
    public PatchSession(Process p, long imageBase, long imageSize, Site[] specs, float amount) {
        process=p; image=imageBase; sites=specs;
        handle=Native.OpenProcess(0x0038|0x1000,false,p.Id);
        if(handle==IntPtr.Zero) throw Native.Error("OpenProcess failed");
        try {
            Verify(false);
            long start=(image+imageSize+0xffff)&~0xffffL;
            for(long candidate=start;candidate<image+0x70000000L;candidate+=0x10000) {
                IntPtr a=Native.VirtualAllocEx(handle,new IntPtr(candidate),(UIntPtr)4096,0x3000,0x04);
                if(a!=IntPtr.Zero) { data=a.ToInt64(); break; }
            }
            if(data==0) throw new Exception("No nearby allocation available");
            byte[] values=new byte[8]; Array.Copy(BitConverter.GetBytes(amount),0,values,0,4); Array.Copy(BitConverter.GetBytes(-amount),0,values,4,4);
            Native.Write(handle,data,values);
            patched=new byte[sites.Length][];
            for(int i=0;i<sites.Length;i++) {
                Site s=sites[i]; byte[] code=(byte[])s.Original.Clone();
                long displacement=data+(s.Negative?4:0)-(image+s.Offset+code.Length);
                if(displacement<Int32.MinValue||displacement>Int32.MaxValue) throw new Exception("Relative displacement out of range");
                Array.Copy(BitConverter.GetBytes((int)displacement),0,code,code.Length-4,4); patched[i]=code;
            }
        } catch { Dispose(); throw; }
    }
    void Verify(bool expectPatch) {
        for(int i=0;i<sites.Length;i++) {
            byte[] expected=expectPatch?patched[i]:sites[i].Original;
            if(!Native.Equal(Native.Read(handle,image+sites[i].Offset,expected.Length),expected))
                throw new Exception("Instruction mismatch at RVA 0x"+sites[i].Offset.ToString("X")+". Refusing to patch.");
        }
    }
    void WriteCode(int i, byte[] bytes) {
        long address=image+sites[i].Offset; uint old;
        if(!Native.VirtualProtectEx(handle,new IntPtr(address),(UIntPtr)bytes.Length,0x40,out old)) throw Native.Error("Protect failed");
        try { Native.Write(handle,address,bytes); }
        finally {
            uint ignored;
            if(!Native.VirtualProtectEx(handle,new IntPtr(address),(UIntPtr)bytes.Length,old,out ignored)) throw Native.Error("Restore protection failed");
        }
        if(!Native.FlushInstructionCache(handle,new IntPtr(address),(UIntPtr)bytes.Length)) throw Native.Error("Cache flush failed");
    }
    public void Set(bool active) {
        if(active==Active) return;
        using(new Frozen(process)) {
            Verify(Active);
            try {
                for(int i=0;i<sites.Length;i++) WriteCode(i,active?patched[i]:sites[i].Original);
                Verify(active); Active=active;
            } catch(Exception failure) {
                List<Exception> errors=new List<Exception>(); errors.Add(failure);
                for(int i=0;i<sites.Length;i++) { try { WriteCode(i,Active?patched[i]:sites[i].Original); } catch(Exception e) { errors.Add(e); } }
                if(errors.Count>1) throw new AggregateException("Rollback incomplete; exit the game to reset process memory.",errors);
                throw;
            }
        }
    }
    public float[] ReadTargets() {
        float[] values=new float[sites.Length];
        for(int i=0;i<sites.Length;i++) {
            byte[] code=Native.Read(handle,image+sites[i].Offset,sites[i].Original.Length);
            long dest=image+sites[i].Offset+code.Length+BitConverter.ToInt32(code,code.Length-4);
            values[i]=BitConverter.ToSingle(Native.Read(handle,dest,4),0);
        }
        return values;
    }
    public void Dispose() {
        if(handle==IntPtr.Zero) return;
        bool safe=true;
        try { if(Active&&!process.HasExited) Set(false); }
        catch(Exception e) { safe=false; Console.Error.WriteLine("RESTORE ERROR: "+e.Message+". Exit the game to reset memory."); }
        if(data!=0&&safe) Native.VirtualFreeEx(handle,new IntPtr(data),UIntPtr.Zero,0x8000);
        Native.CloseHandle(handle); handle=IntPtr.Zero;
    }
}
class Program {
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate float FloatThunk();
    static string Game;
    const string Hash="ff445331e7144a2dc79ec50c2e6fd42f2a2e7b49ae7b03ae1a6076dcfc769262";
    static string logPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MovingLean","Logs","patcher_"+Process.GetCurrentProcess().Id+".log");
    static Site[] GameSites() { return new Site[] {
        new Site(0x4e792e8,"f3 0f 10 35 60 6e 5e 01",false),
        new Site(0x4e792f2,"f3 0f 10 35 2a 6f 5e 01",true),
        new Site(0x4e793aa,"f3 0f 10 1d 9e 6d 5e 01",false),
        new Site(0x4e793e7,"0f 2e 05 62 6d 5e 01",false)}; }
    static void Log(string s) {
        Console.WriteLine(s);
        try { Directory.CreateDirectory(Path.GetDirectoryName(logPath)); File.AppendAllText(logPath,DateTime.Now.ToString("s")+" "+s+Environment.NewLine); }
        catch(IOException) {} catch(UnauthorizedAccessException) {}
    }
    static void Check(bool value,string label) { if(!value) throw new Exception("TEST FAILED: "+label); Log("PASS "+label); }
    static float ReadAmount() {
        string path=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","LeanAmount.txt"));
        if(!File.Exists(path)) return 24f;
        float amount;
        if(!Single.TryParse(File.ReadAllText(path).Trim(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out amount)||Single.IsNaN(amount)||amount<15f||amount>30f)
            throw new Exception("LeanAmount.txt must contain one number from 15 to 30. No changes applied.");
        return amount;
    }
    static void VerifyFile() {
        if(!File.Exists(Game)) throw new Exception("Game executable missing: "+Game);
        string actual; using(SHA256 sha=SHA256.Create()) using(FileStream f=File.OpenRead(Game)) actual=BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();
        if(actual!=Hash) throw new Exception("Unsupported executable SHA256. Nothing changed. Actual="+actual);
        byte[] bytes=File.ReadAllBytes(Game);
        int pe=BitConverter.ToInt32(bytes,0x3c), optional=pe+24;
        int count=BitConverter.ToUInt16(bytes,pe+6), optSize=BitConverter.ToUInt16(bytes,pe+20);
        long imageBase=BitConverter.ToInt64(bytes,optional+24);
        foreach(Site site in GameSites()) {
            int raw=-1;
            for(int i=0;i<count;i++) {
                int s=optional+optSize+i*40;
                uint rva=BitConverter.ToUInt32(bytes,s+12), size=BitConverter.ToUInt32(bytes,s+16), start=BitConverter.ToUInt32(bytes,s+20);
                if(site.Offset>=rva&&site.Offset+site.Original.Length<=rva+size) raw=checked((int)(start+site.Offset-rva));
            }
            if(raw<0) throw new Exception("Site is outside file sections");
            byte[] original=new byte[site.Original.Length]; Array.Copy(bytes,raw,original,0,original.Length);
            if(!Native.Equal(original,site.Original)) throw new Exception("On-disk instruction mismatch");
            long target=site.Offset+original.Length+BitConverter.ToInt32(original,original.Length-4);
            int targetRaw=-1;
            for(int i=0;i<count;i++) {
                int s=optional+optSize+i*40;
                uint rva=BitConverter.ToUInt32(bytes,s+12), size=BitConverter.ToUInt32(bytes,s+16), start=BitConverter.ToUInt32(bytes,s+20);
                if(target>=rva&&target+4<=rva+size) targetRaw=checked((int)(start+target-rva));
            }
            if(targetRaw<0||BitConverter.ToSingle(bytes,targetRaw)!=(site.Negative?-15f:15f)) throw new Exception("Unexpected original constant");
            Log("VERIFIED RVA 0x"+site.Offset.ToString("X")+" original="+(site.Negative?"-15":"15")+" targetVA=0x"+(imageBase+target).ToString("X"));
        }
        Log("Executable SHA256 and all four original instructions/constants verified. No game process accessed.");
    }
    static void Fixture() {
        IntPtr h=Native.OpenProcess(0x0038|0x1000,false,Process.GetCurrentProcess().Id);
        IntPtr mem=Native.VirtualAllocEx(h,IntPtr.Zero,(UIntPtr)4096,0x3000,0x40);
        if(mem==IntPtr.Zero) throw Native.Error("Fixture allocation failed");
        long address=mem.ToInt64(); Site[] specs=GameSites();
        for(int i=0;i<specs.Length;i++) {
            byte[] code=(byte[])specs[i].Original.Clone(); long site=i*32;
            Array.Copy(BitConverter.GetBytes((int)(512+(specs[i].Negative?4:0)-site-code.Length)),0,code,code.Length-4,4);
            Native.Write(h,address+site,code);
        }
        Native.Write(h,address+512,BitConverter.GetBytes(15f)); Native.Write(h,address+516,BitConverter.GetBytes(-15f));
        // Wrap the first original instruction in a callable x64 thunk, preserving nonvolatile XMM6.
        Native.Write(h,address+768,new byte[] {0x48,0x83,0xec,0x28,0x0f,0x11,0x74,0x24,0x10});
        Native.Write(h,address+777,new byte[] {0xe9,0xf2,0xfc,0xff,0xff}); // jmp address: -782
        Native.Write(h,address+8,new byte[] {0x0f,0x28,0xc6,0x0f,0x10,0x74,0x24,0x10,0x48,0x83,0xc4,0x28,0xc3});
        FloatThunk thunk=(FloatThunk)Marshal.GetDelegateForFunctionPointer(new IntPtr(address+768),typeof(FloatThunk));
        Console.WriteLine(address.ToString("X")); Console.Out.Flush();
        string command;
        while((command=Console.ReadLine())=="read") { Console.WriteLine(thunk().ToString(System.Globalization.CultureInfo.InvariantCulture)); Console.Out.Flush(); }
        Native.VirtualFreeEx(h,mem,UIntPtr.Zero,0x8000); Native.CloseHandle(h);
    }
    static void SelfTest() {
        ProcessStartInfo psi=new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--fixture");
        psi.UseShellExecute=false; psi.RedirectStandardOutput=true; psi.RedirectStandardInput=true; psi.CreateNoWindow=true;
        using(Process child=Process.Start(psi)) {
            try {
                long address=Convert.ToInt64(child.StandardOutput.ReadLine(),16);
                IntPtr h=Native.OpenProcess(0x0038|0x1000,false,child.Id);
                Site[] specs=GameSites();
                for(int i=0;i<specs.Length;i++) { specs[i].Offset=i*32; specs[i].Original=Native.Read(h,address+i*32,specs[i].Original.Length); }
                byte[] baseline=Native.Read(h,address,128);
                try {
                    Site[] bad=GameSites(); for(int i=0;i<bad.Length;i++) { bad[i].Offset=specs[i].Offset; bad[i].Original=(byte[])specs[i].Original.Clone(); }
                    bad[0].Original[0]^=1;
                    bool rejected=false;
                    try { using(PatchSession s=new PatchSession(child,address,4096,bad,24)) {} } catch { rejected=true; }
                    Check(rejected&&Native.Equal(baseline,Native.Read(h,address,128)),"mismatched instruction rejected without code changes");
                    using(PatchSession s=new PatchSession(child,address,4096,specs,24)) {
                        Check(s.ReadTargets()[0]==15&&s.ReadTargets()[1]==-15,"baseline targets resolved");
                        child.StandardInput.WriteLine("read"); Check(child.StandardOutput.ReadLine()=="15","native x64 thunk executes original value");
                        s.Set(true); float[] v=s.ReadTargets();
                        Check(v[0]==24&&v[1]==-24&&v[2]==24&&v[3]==24,"all four patched references read back correctly");
                        child.StandardInput.WriteLine("read"); Check(child.StandardOutput.ReadLine()=="24","native x64 thunk executes patched value");
                        s.Set(false); Check(Native.Equal(baseline,Native.Read(h,address,128)),"original code restored exactly");
                        child.StandardInput.WriteLine("read"); Check(child.StandardOutput.ReadLine()=="15","native x64 thunk executes restored value");
                        for(int i=0;i<10;i++) { s.Set(true); s.Set(false); }
                        Check(Native.Equal(baseline,Native.Read(h,address,128)),"ten toggle cycles restored exactly");
                        s.Set(true);
                    }
                    Check(Native.Equal(baseline,Native.Read(h,address,128)),"Dispose restores active patch");
                } finally { Native.CloseHandle(h); }
                Log("SELF TEST COMPLETE. No game process was started or accessed.");
            } finally { if(!child.HasExited) { child.StandardInput.WriteLine("exit"); child.WaitForExit(5000); } }
        }
    }
    static int ParentPid() {
        IntPtr snapshot=Native.CreateToolhelp32Snapshot(2,0);
        if(snapshot==new IntPtr(-1)) throw Native.Error("Process snapshot failed");
        try {
            Native.ProcessEntry entry=new Native.ProcessEntry(); entry.size=(uint)Marshal.SizeOf(typeof(Native.ProcessEntry));
            uint self=(uint)Process.GetCurrentProcess().Id;
            if(Native.Process32FirstW(snapshot,ref entry)) do { if(entry.pid==self) return (int)entry.parent; } while(Native.Process32NextW(snapshot,ref entry));
            throw new Exception("Cannot identify parent process");
        } finally { Native.CloseHandle(snapshot); }
    }
    static int AutoRun() {
        using(Process p=Process.GetProcessById(ParentPid())) {
            if(!String.Equals(p.ProcessName,"ReadyOrNotSteam-Win64-Shipping",StringComparison.OrdinalIgnoreCase)) throw new Exception("Parent is not the supported game process. No changes applied.");
            Game=p.MainModule.FileName;
            bool created;
            using(Mutex mutex=new Mutex(true,"Local\\MovingLeanAuto_"+p.Id,out created)) {
                if(!created) { Log("Already active for this process; duplicate skipped."); return 0; }
                try {
                    float amount=ReadAmount();
                    VerifyFile();
                    using(PatchSession session=new PatchSession(p,p.MainModule.BaseAddress.ToInt64(),p.MainModule.ModuleMemorySize,GameSites(),amount)) {
                        session.Set(true); Log("AUTO ON amount="+amount+" pid="+p.Id+" four instructions verified");
                        bool previous=false;
                        while(!p.HasExited) {
                            uint foreground; Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out foreground);
                            bool chord=(Native.GetAsyncKeyState(0x11)&0x8000)!=0&&(Native.GetAsyncKeyState(0x12)&0x8000)!=0&&(Native.GetAsyncKeyState(0x77)&0x8000)!=0;
                            if(chord&&!previous&&foreground==(uint)p.Id) { session.Set(!session.Active); Log(session.Active?"AUTO ON amount="+amount:"AUTO OFF original=15"); }
                            previous=chord; Thread.Sleep(40);
                        }
                    }
                    Log("Game exited. AUTO session complete.");
                } finally { mutex.ReleaseMutex(); }
            }
        }
        return 0;
    }
    static int Main(string[] args) {
        try {
            if(args.Length>0&&args[0]=="--fixture") { Fixture(); return 0; }
            if(args.Length>0&&args[0]=="--self-test") { SelfTest(); return 0; }
            if(args.Length>0&&args[0]=="--verify-only") {
                if(args.Length!=2) throw new Exception("Usage: --verify-only GAME_EXE");
                Game=args[1]; VerifyFile(); return 0;
            }
            return AutoRun();

        } catch(Exception e) {
            Log("ERROR: "+e.ToString());
            return 1;
        }
    }
}
