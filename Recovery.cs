using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

public static class OwnedClient {
    public static Process Launch(string uri) {
        string command=null;
        using(var key=Registry.ClassesRoot.OpenSubKey(@"roblox-player\shell\open\command")) if(key!=null) command=key.GetValue("") as string;
        if(String.IsNullOrEmpty(command)) throw new LaunchException("Install Roblox's desktop launcher first.");
        string executable=null,arguments=null;
        if(command.StartsWith("\"")) { int end=command.IndexOf('"',1); if(end>1) { executable=command.Substring(1,end-1); arguments=command.Substring(end+1).Trim(); } }
        if(executable!=null && File.Exists(executable) && String.Equals(Path.GetFileName(executable),"RobloxPlayerBeta.exe",StringComparison.OrdinalIgnoreCase) && (arguments=="%1" || arguments=="\"%1\"")) {
            return Process.Start(new ProcessStartInfo(executable,"\""+uri+"\"") { UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(executable) });
        }
        using(var launcher=Process.Start(new ProcessStartInfo(uri) { UseShellExecute=true })) { }
        return null; // A bootstrapper or custom launcher cannot be assigned safely to an account.
    }
}
public interface IClientLife : IDisposable {
    bool Exited { get; }
    int ExitCode { get; }
}
public class ClientLife : IClientLife {
    Process process;
    public ClientLife(Process owned) { process=owned; }
    public bool Exited { get { return process.HasExited; } }
    public int ExitCode { get { return process.ExitCode; } }
    public void Dispose() { process.Dispose(); }
}
public class RecoveryPolicy {
    readonly Queue<DateTime> attempts=new Queue<DateTime>();
    public DateTime Due { get; private set; }
    public bool Pending { get; private set; }
    public int Count { get { return attempts.Count; } }
    public bool QueueCrash(int exitCode,DateTime now) {
        Pending=false;
        while(attempts.Count>0 && now-attempts.Peek()>=TimeSpan.FromMinutes(10)) attempts.Dequeue();
        if(exitCode==0 || attempts.Count>=3) return false;
        Due=now.AddSeconds(20*Math.Pow(2,attempts.Count)); Pending=true; return true;
    }
    public void BeginAttempt(DateTime now) { Pending=false; attempts.Enqueue(now); }
}
public class RecoveryJob : IDisposable {
    public Account Account; public ulong Place; public bool Follow; public IClientLife Client;
    public RecoveryPolicy Policy=new RecoveryPolicy();
    public void Dispose() { if(Client!=null) { Client.Dispose(); Client=null; } }
}
public class RecoveryController : IDisposable {
    readonly Dictionary<string,RecoveryJob> jobs=new Dictionary<string,RecoveryJob>();
    public bool Enabled { get; set; }
    bool checking; int generation;
    public Action<string> Report;
    public Func<bool> CanRestart;
    public Func<Account,ulong,bool,Func<bool>,Task<IClientLife>> Relaunch;
    public Func<DateTime> Now=()=>DateTime.UtcNow;
    void Say(string message) { if(Report!=null) Report(message); }
    public void Track(Account account,ulong place,IClientLife client,bool follow=false) {
        if(!Enabled || client==null) { if(client!=null) client.Dispose(); return; }
        RecoveryJob old;
        if(jobs.TryGetValue(account.Id,out old)) {
            if(old.Client!=null && !old.Client.Exited) { client.Dispose(); Say("Recovery already watches @"+account.Username+". The additional client is not watched."); return; }
            old.Dispose();
        }
        jobs[account.Id]=new RecoveryJob { Account=account,Place=place,Client=client,Follow=follow };
    }
    public void Stop() { Enabled=false; generation++; foreach(var job in jobs.Values) job.Dispose(); jobs.Clear(); }
    public async Task Tick() {
        if(!Enabled || checking) return;
        checking=true; int version=generation;
        try {
            foreach(var job in jobs.Values.ToArray()) {
                if(!Enabled || generation!=version) break;
                if(job.Client!=null) {
                    try {
                        if(!job.Client.Exited) continue;
                        int code=job.Client.ExitCode; job.Dispose();
                        if(!job.Policy.QueueCrash(code,Now())) {
                            jobs.Remove(job.Account.Id);
                            Say(code==0 ? "@"+job.Account.Username+" closed normally; no restart." : "Recovery stopped for @"+job.Account.Username+": retry limit reached."); continue;
                        }
                        Say("@"+job.Account.Username+" exited abnormally. Recovery waits "+Math.Max(0,(int)(job.Policy.Due-Now()).TotalSeconds)+" seconds.");
                    } catch { job.Dispose(); jobs.Remove(job.Account.Id); Say("Recovery stopped for @"+job.Account.Username+": process status unavailable."); continue; }
                }
                if(!job.Policy.Pending || Now()<job.Policy.Due) continue;
                if(CanRestart!=null && !CanRestart()) { Say("Recovery paused: queue busy, CPU/RAM load high, or settings need attention."); continue; }
                job.Policy.BeginAttempt(Now());
                try {
                    IClientLife replacement=await Relaunch(job.Account,job.Place,job.Follow,()=>Enabled && generation==version && jobs.ContainsKey(job.Account.Id) && Object.ReferenceEquals(jobs[job.Account.Id],job));
                    if(!Enabled || generation!=version) { if(replacement!=null) replacement.Dispose(); break; }
                    if(!jobs.ContainsKey(job.Account.Id) || !Object.ReferenceEquals(jobs[job.Account.Id],job)) { if(replacement!=null) replacement.Dispose(); continue; }
                    if(replacement==null) throw new LaunchException("The launcher could not supply a trackable Roblox process.");
                    job.Client=replacement; Say("Restarted @"+job.Account.Username+" (attempt "+job.Policy.Count+"/3 within ten minutes).");
                } catch(Exception error) {
                    if(!Enabled || generation!=version) break;
                    if(!jobs.ContainsKey(job.Account.Id) || !Object.ReferenceEquals(jobs[job.Account.Id],job)) continue;
                    jobs.Remove(job.Account.Id); job.Dispose();
                    Say("Recovery stopped for @"+job.Account.Username+": "+(error is LaunchException?error.Message:"restart failed ("+error.GetType().Name+")."));
                }
            }
        } finally { checking=false; }
    }
    public void Dispose() { Stop(); }
    public void Forget(string accountId) { RecoveryJob job; if(jobs.TryGetValue(accountId,out job)) { job.Dispose(); jobs.Remove(accountId); } }
}
public partial class Manager {
    System.Windows.Forms.CheckBox recoverCrashes=new AudioCheckBox();
    RecoveryController recovery=new RecoveryController();
    System.Windows.Forms.Timer recoveryTimer=new System.Windows.Forms.Timer();
    void SetupRecovery() {
        recovery.Report=message=> { if(!IsDisposed) status.Text=message; };
        recovery.CanRestart=()=> {
            if(launching || ResourceUsage.MemoryLow() || (usage.SystemReady && usage.SystemCpu>=75)) return false;
            try { return (!resourceMode.Active || resourceMode.MatchesPreset()) && (!audioMode.Active || audioMode.MatchesPreset()); } catch { return false; }
        };
        recovery.Relaunch=async (account,placeId,follow,stillActive)=> {
            string ticket=await DirectLaunch.GetTicket(account,placeId,follow);
            if(!stillActive() || IsDisposed || launching) return null;
            Process process=OwnedClient.Launch(DirectLaunch.BuildUri(ticket,placeId,follow));
            return process==null?null:new ClientLife(process);
        };
        recoveryTimer.Interval=5000; recoveryTimer.Tick+=async delegate { await recovery.Tick(); };
        recoverCrashes.CheckedChanged+=delegate {
            if(recoverCrashes.Checked) { recovery.Enabled=true; recoveryTimer.Start(); status.Text="Recovery enabled for your next launches. Normal exits are not restarted."; }
            else { recoveryTimer.Stop(); recovery.Stop(); status.Text="Recovery stopped. Your running games remain open."; }
        };
        FormClosing+=delegate { recoveryTimer.Stop(); recovery.Stop(); };
        FormClosed+=delegate { recoveryTimer.Dispose(); };
    }
}


