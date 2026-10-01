using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Xml;
using System.Globalization;

public class PresetBackup {
    public int Version { get; set; }
    public string SettingsPath { get; set; }
    public Dictionary<string,string> Original { get; set; }
}
public class ResourceMode {
    readonly string settings,journal,gate;
    readonly Func<bool> running;
    readonly string[] keys;
    readonly bool audioOnly;
    static readonly Dictionary<string,string> Targets=new Dictionary<string,string> {
        {"FramerateCap","60"}, {"SavedQualityLevel","1"}, {"GraphicsQualityLevel","1"},
        {"GraphicsOptimizationMode","0"}, {"MaxQualityEnabled","false"}, {"MasterVolume","0"}
    };
    static readonly Dictionary<string,string> Types=new Dictionary<string,string> {
        {"FramerateCap","int"}, {"SavedQualityLevel","token"}, {"GraphicsQualityLevel","int"},
        {"GraphicsOptimizationMode","token"}, {"MaxQualityEnabled","bool"}, {"MasterVolume","float"}
    };
    static string Target(string key,PresetBackup backup) {
        if(key=="FramerateCap") { int original=Int32.Parse(backup.Original[key]); return original>0 && original<60 ? original.ToString() : "60"; }
        return Targets[key];
    }
    public ResourceMode(string path,string dataDirectory,Func<bool> isRunning,bool muteOnly=false) {
        audioOnly=muteOnly;
        keys=muteOnly ? new[] { "MasterVolume" } : Targets.Keys.Where(key=>key!="MasterVolume").ToArray();
        settings=Path.GetFullPath(path); Directory.CreateDirectory(dataDirectory);
        journal=Path.Combine(dataDirectory,muteOnly?"audio-mode-backup.json":"resource-mode-backup.json"); gate=Path.Combine(dataDirectory,"resource-mode.lock"); running=isRunning;
    }
    public bool Active { get { return File.Exists(journal); } }
    public static bool RobloxRunning() {
        foreach(string name in new[] {"RobloxPlayerBeta","RobloxPlayer","RobloxPlayerInstaller"}) {
            Process[] processes=Process.GetProcessesByName(name); bool found=processes.Length>0;
            foreach(var process in processes) process.Dispose(); if(found) return true;
        }
        return false;
    }
    void RequireClosed() { if(running()) throw new InvalidOperationException("Close all Roblox clients before switching low resource mode. Running clients keep settings in memory."); }
    static XmlDocument Parse(byte[] bytes) {
        XmlDocument document=new XmlDocument { PreserveWhitespace=true,XmlResolver=null };
        using(var input=new MemoryStream(bytes)) using(var reader=XmlReader.Create(input,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null })) document.Load(reader);
        if(document.SelectNodes("//Item[@class='UserGameSettings']/Properties").Count!=1) throw new InvalidOperationException("Roblox settings have an unfamiliar format. No settings were changed.");
        return document;
    }
    static XmlElement Property(XmlDocument document,string name) {
        var matches=document.SelectNodes("//Item[@class='UserGameSettings']/Properties/*[@name='"+name+"']");
        if(matches.Count!=1) throw new InvalidOperationException("Roblox setting "+name+" is missing or duplicated. Update Roblox settings in-game first.");
        var element=(XmlElement)matches[0];
        if(element.LocalName!=Types[name]) throw new InvalidOperationException("Roblox setting "+name+" has an unfamiliar type. No settings were changed.");
        return element;
    }
    PresetBackup ReadBackup() {
        var backup=new JavaScriptSerializer().Deserialize<PresetBackup>(File.ReadAllText(journal));
        if(backup==null || backup.Version!=1 || !String.Equals(backup.SettingsPath,settings,StringComparison.OrdinalIgnoreCase) || backup.Original==null || (audioOnly ? !backup.Original.ContainsKey("MasterVolume") : (!backup.Original.ContainsKey("FramerateCap") || !backup.Original.ContainsKey("SavedQualityLevel"))) || backup.Original.Keys.Any(key=>!keys.Contains(key)))
            throw new InvalidOperationException("The settings backup is invalid. It has been kept; no settings were changed.");
        foreach(var pair in backup.Original) if(!ValidValue(pair.Key,pair.Value)) throw new InvalidOperationException("Invalid settings backup.");
        return backup;
    }
    static bool ValidValue(string key,string value) {
        if(Types[key]=="bool") { bool flag; return Boolean.TryParse(value,out flag); }
        if(Types[key]=="float") { double number; return Double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number) && number>=0 && number<=1; }
        int integer; return Int32.TryParse(value,out integer);
    }
    static void WriteAtomic(string path,byte[] bytes) {
        string temporary=path+".rdl-"+Guid.NewGuid().ToString("N")+".tmp";
        try {
            using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { file.Write(bytes,0,bytes.Length); file.Flush(true); }
            if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
        } finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
    void Commit(XmlDocument document,byte[] original) {
        RequireClosed();
        if(!File.ReadAllBytes(settings).SequenceEqual(original)) throw new InvalidOperationException("Roblox settings changed during the operation. Try again after closing Roblox.");
        using(var stream=new MemoryStream()) { document.Save(stream); WriteAtomic(settings,stream.ToArray()); }
    }
    public void Apply() {
        using(var exclusive=new FileStream(gate,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
            RequireClosed();
            if(!File.Exists(settings)) throw new InvalidOperationException("Open Roblox once and change its graphics settings, then close it before enabling this mode.");
            byte[] original=File.ReadAllBytes(settings); XmlDocument document=Parse(original);
            PresetBackup backup;
            if(Active) backup=ReadBackup();
            else {
                if(audioOnly) Property(document,"MasterVolume"); else { Property(document,"FramerateCap"); Property(document,"SavedQualityLevel"); }
                backup=new PresetBackup { Version=1,SettingsPath=settings,Original=new Dictionary<string,string>() };
                foreach(string key in keys) {
                    if(document.SelectSingleNode("//Item[@class='UserGameSettings']/Properties/*[@name='"+key+"']")!=null) backup.Original[key]=Property(document,key).InnerText;
                }
                // Validate every original value before recording a recoverable change.
                foreach(var pair in backup.Original) if(!ValidValue(pair.Key,pair.Value)) throw new InvalidOperationException("A Roblox setting has an unfamiliar value.");
                WriteAtomic(journal,System.Text.Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(backup)));
            }
            foreach(string key in backup.Original.Keys) Property(document,key).InnerText=Target(key,backup);
            Commit(document,original);
            XmlDocument verified=Parse(File.ReadAllBytes(settings));
            foreach(string key in backup.Original.Keys) if(Property(verified,key).InnerText!=Target(key,backup)) throw new IOException("The preset could not be verified. Your backup has been retained.");
        }
    }
    public void Restore() {
        using(var exclusive=new FileStream(gate,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
            RequireClosed(); if(!Active) return;
            PresetBackup backup=ReadBackup();
            if(!File.Exists(settings)) throw new InvalidOperationException("Roblox settings are missing. The backup has been kept for recovery.");
            byte[] original=File.ReadAllBytes(settings); XmlDocument document=Parse(original);
            foreach(var pair in backup.Original) Property(document,pair.Key).InnerText=pair.Value;
            Commit(document,original);
            XmlDocument verified=Parse(File.ReadAllBytes(settings));
            foreach(var pair in backup.Original) if(Property(verified,pair.Key).InnerText!=pair.Value) throw new IOException("Restoration could not be verified. The backup has been kept.");
            // Delete only after persisted settings match every saved original value.
            File.Delete(journal);
        }
    }
    public bool MatchesPreset() {
        if(!Active) return false;
        var backup=ReadBackup(); var document=Parse(File.ReadAllBytes(settings));
        return backup.Original.Keys.All(key=>Property(document,key).InnerText==Target(key,backup));
    }
    public string Describe() {
        var document=Parse(File.ReadAllBytes(settings));
        return "Saved FPS "+Property(document,"FramerateCap").InnerText+" / graphics "+Property(document,"SavedQualityLevel").InnerText;
    }
}
public class CpuSample {
    ulong idle,kernel,user; bool ready;
    public double Percent { get; private set; }
    public bool Ready { get { return ready; } }
    public void Update(ulong newIdle,ulong newKernel,ulong newUser) {
        if(ready && newIdle>=idle && newKernel>=kernel && newUser>=user) {
            double total=(double)(newKernel-kernel)+(newUser-user);
            if(total>0) Percent=Math.Max(0,Math.Min(100,(total-(newIdle-idle))/total*100));
        }
        idle=newIdle; kernel=newKernel; user=newUser; ready=true;
    }
}
public class ResourceUsage {
    [StructLayout(LayoutKind.Sequential)] struct FileTime { public uint Low,High; public ulong Value {get {return ((ulong)High<<32)|Low;}} }
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FileTime idle,out FileTime kernel,out FileTime user);
    CpuSample system=new CpuSample();
    public double SystemCpu { get {return system.Percent;} }
    public bool SystemReady { get {return system.Ready;} }
    [StructLayout(LayoutKind.Sequential)] class MemoryStatus {
        public uint Length=(uint)Marshal.SizeOf(typeof(MemoryStatus)); public uint Load;
        public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,AvailableExtended;
    }
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GlobalMemoryStatusEx([In,Out] MemoryStatus value);
    Dictionary<int,double> previous=new Dictionary<int,double>(); DateTime last=DateTime.UtcNow;
    public string Sample() {
        FileTime idle,kernel,user; if(GetSystemTimes(out idle,out kernel,out user)) system.Update(idle.Value,kernel.Value,user.Value);
        long ram=0; double cpu=0; DateTime now=DateTime.UtcNow; double elapsed=(now-last).TotalSeconds;
        var current=new Dictionary<int,double>(); int count=0;
        foreach(var process in Process.GetProcessesByName("RobloxPlayerBeta")) using(process) {
            try { double time=process.TotalProcessorTime.TotalSeconds; current[process.Id]=time; ram+=process.WorkingSet64; count++; if(previous.ContainsKey(process.Id)&&elapsed>0) cpu+=Math.Max(0,time-previous[process.Id])/elapsed/Environment.ProcessorCount*100; } catch { }
        }
        previous=current; last=now;
        var memory=new MemoryStatus(); bool available=GlobalMemoryStatusEx(memory);
        return count+" clients • Roblox CPU "+Math.Min(100,cpu).ToString("0.0")+"% • System "+SystemCpu.ToString("0")+"% • RAM "+(ram/1073741824d).ToString("0.0")+" GB"+(available?" • Available "+(memory.AvailablePhysical/1073741824d).ToString("0.0")+" GB":"");
    }
    public static bool MemoryLow() {
        var memory=new MemoryStatus(); if(!GlobalMemoryStatusEx(memory)) return false;
        return memory.AvailablePhysical<Math.Max(1073741824UL,memory.TotalPhysical/10);
    }
}
