using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

public class WindowsClientPolicy : IDisposable {
    [StructLayout(LayoutKind.Sequential)] public struct PowerState { public uint Version,ControlMask,StateMask; }
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetProcessAffinityMask(IntPtr handle,out UIntPtr process,out UIntPtr system);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetProcessAffinityMask(IntPtr handle,UIntPtr mask);
    [DllImport("kernel32.dll",SetLastError=true)] static extern uint GetPriorityClass(IntPtr handle);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetPriorityClass(IntPtr handle,uint priority);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetProcessInformation(IntPtr handle,int type,ref PowerState state,uint size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetProcessInformation(IntPtr handle,int type,ref PowerState state,uint size);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle,uint timeout);
    readonly IntPtr handle;
    public readonly ulong OriginalAffinity;
    public readonly uint OriginalPriority;
    public readonly PowerState OriginalPower;
    readonly ulong targetMask;
    public ulong TargetAffinity {get {return targetMask;}}
    bool changed;
    public int Pid {get;private set;}
    public static ulong FourProcessors(ulong available) {
        ulong chosen=0; int count=0;
        for(int bit=0;bit<64&&count<4;bit++) { ulong mask=1UL<<bit; if((available&mask)!=0) { chosen|=mask; count++; } }
        return chosen;
    }
    public WindowsClientPolicy(int pid,Func<ulong,ulong> allocate=null) {
        Pid=pid; handle=OpenProcess(0x101200,false,pid); // query-limited, set-information, synchronize
        if(handle==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try {
            UIntPtr affinity,system;
            if(!GetProcessAffinityMask(handle,out affinity,out system)) throw new Win32Exception(Marshal.GetLastWin32Error());
            OriginalAffinity=affinity.ToUInt64();
            ulong allowed=system.ToUInt64()&OriginalAffinity;
            targetMask=allocate==null ? FourProcessors(allowed) : allocate(allowed);
            if(targetMask==0 || (targetMask&~allowed)!=0) throw new InvalidOperationException("Requested affinity exceeds the available processors.");
            OriginalPriority=GetPriorityClass(handle); if(OriginalPriority==0) throw new Win32Exception(Marshal.GetLastWin32Error());
            var power=new PowerState {Version=1};
            if(!GetProcessInformation(handle,4,ref power,12)) throw new Win32Exception(Marshal.GetLastWin32Error());
            OriginalPower=power;
        } catch { CloseHandle(handle); throw; }
    }
    public bool Alive {get {return WaitForSingleObject(handle,0)==258;} }
    public bool Applied() {
        UIntPtr affinity,system; var state=new PowerState {Version=1};
        return Alive && GetProcessAffinityMask(handle,out affinity,out system) && affinity.ToUInt64()==targetMask && GetPriorityClass(handle)==0x40 && GetProcessInformation(handle,4,ref state,12) && (state.ControlMask&1)!=0 && (state.StateMask&1)!=0;
    }
    public void Apply() {
        if(!Alive) return;
        var power=OriginalPower; power.Version=1; power.ControlMask|=1; power.StateMask|=1;
        changed=true;
        try {
            if(!SetProcessAffinityMask(handle,new UIntPtr(targetMask)) || !SetPriorityClass(handle,0x40) || !SetProcessInformation(handle,4,ref power,12)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if(!Applied()) throw new InvalidOperationException("Windows did not retain the requested process policy.");
        } catch { Restore(); throw; }
    }
    public bool Restore() {
        if(!changed || !Alive) {changed=false;return true;}
        var power=OriginalPower;
        bool restoredPower=SetProcessInformation(handle,4,ref power,12);
        bool restoredPriority=SetPriorityClass(handle,OriginalPriority);
        bool restoredAffinity=SetProcessAffinityMask(handle,new UIntPtr(OriginalAffinity));
        UIntPtr affinity,system; var verified=new PowerState {Version=1};
        bool verifiedRestore=restoredPower && restoredPriority && restoredAffinity && GetProcessAffinityMask(handle,out affinity,out system) && affinity.ToUInt64()==OriginalAffinity && GetPriorityClass(handle)==OriginalPriority && GetProcessInformation(handle,4,ref verified,12) && verified.ControlMask==OriginalPower.ControlMask && verified.StateMask==OriginalPower.StateMask;
        changed=!verifiedRestore; return verifiedRestore;
    }
    public void Dispose() { Restore(); CloseHandle(handle); }
}
public class WindowsPolicyController : IDisposable {
    List<ulong> topology;
    readonly Dictionary<int,WindowsClientPolicy> clients=new Dictionary<int,WindowsClientPolicy>();
    readonly Dictionary<int,DateTime> failed=new Dictionary<int,DateTime>();
    public string LastStatus {get;private set;}
    public string Assignments() {
        if(clients.Count==0) return "No clients are currently tracked. Enable low resource mode and launch Roblox first.";
        return String.Join(Environment.NewLine,clients.OrderBy(pair=>pair.Key).Select(pair=>"PID "+pair.Key+" → CPU "+String.Join(", ",Enumerable.Range(0,64).Where(bit=>(pair.Value.TargetAffinity&(1UL<<bit))!=0))+" | "+(pair.Value.Applied()?"verified":"not verified")));
    }
    public void Scan() {
        if(topology==null) {
            try {topology=AffinityPlanner.PhysicalCores();}
            catch {LastStatus="CPU topology unavailable; rotating affinity was not applied.";return;}
        }
        foreach(var pair in clients.ToArray()) if(!pair.Value.Alive) {pair.Value.Dispose();clients.Remove(pair.Key);}
        foreach(var process in Process.GetProcessesByName("RobloxPlayerBeta")) using(process) {
            try {
                if(clients.ContainsKey(process.Id)) {
                    if(!clients[process.Id].Applied()) clients[process.Id].Apply();
                    continue;
                }
                if(failed.ContainsKey(process.Id) && DateTime.UtcNow-failed[process.Id]<TimeSpan.FromSeconds(30)) continue;
                var policy=new WindowsClientPolicy(process.Id,allowed=>AffinityPlanner.Pair(allowed,topology,clients.Values.Select(client=>client.TargetAffinity))); clients[process.Id]=policy; policy.Apply(); failed.Remove(process.Id);
            } catch(Exception error) {failed[process.Id]=DateTime.UtcNow; LastStatus="Windows policy failed for PID "+process.Id+" ("+error.GetType().Name+").";}
        }
        LastStatus=clients.Values.Count(client=>client.Applied())+" clients verified: Efficiency mode + balanced processor pairs"+(failed.Count>0?" • "+failed.Count+" failed":"");
        foreach(int pid in failed.Keys.ToArray()) {
            try {using(var process=Process.GetProcessById(pid)) {if(process.HasExited) failed.Remove(pid);}} catch {failed.Remove(pid);}
        }
    }
    public bool RestoreAll() {
        bool success=true;
        foreach(var pair in clients.ToArray()) {
            if(pair.Value.Restore()) {pair.Value.Dispose();clients.Remove(pair.Key);} else success=false;
        }
        if(success) failed.Clear(); return success;
    }
    public void Dispose() { RestoreAll(); foreach(var client in clients.Values) client.Dispose(); clients.Clear(); }
}
public partial class Manager {
    WindowsPolicyController windowsPolicies=new WindowsPolicyController();
    bool runtimePolicyTesting;
    void SetupWindowsPolicies() {
        Shown+=delegate {if(!runtimePolicyTesting && lowResource.Checked) windowsPolicies.Scan();};
        FormClosing+=delegate {
            if(!windowsPolicies.RestoreAll()) System.Windows.Forms.MessageBox.Show("Windows denied restoration for a running client. Close that Roblox client to reset its process settings.","Process settings");
            windowsPolicies.Dispose();
        };
    }
}
