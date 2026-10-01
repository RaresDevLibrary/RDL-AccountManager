using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

public static class AffinityPlanner {
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetLogicalProcessorInformationEx(int relationship,IntPtr buffer,ref uint length);
    [DllImport("kernel32.dll")] static extern ushort GetActiveProcessorGroupCount();
    public static List<ulong> PhysicalCores() {
        if(GetActiveProcessorGroupCount()>1) throw new InvalidOperationException("Multiple processor groups are not supported by this affinity preset.");
        uint length=0; GetLogicalProcessorInformationEx(0,IntPtr.Zero,ref length);
        if(length==0 || length>1048576) throw new InvalidOperationException("Windows did not supply usable CPU topology.");
        IntPtr buffer=Marshal.AllocHGlobal((int)length);
        try {
            if(!GetLogicalProcessorInformationEx(0,buffer,ref length)) throw new InvalidOperationException("CPU topology query failed.");
            var cores=new List<ulong>();
            for(int offset=0;offset<(int)length;) {
                int size=Marshal.ReadInt32(buffer,offset+4);
                if(size<48 || offset+size>(int)length) throw new InvalidOperationException("Unknown CPU topology record.");
                int groups=(ushort)Marshal.ReadInt16(buffer,offset+30);
                for(int group=0;group<groups;group++) {
                    int record=offset+32+group*16;
                    if(record+16>offset+size) throw new InvalidOperationException("Invalid processor group record.");
                    if((ushort)Marshal.ReadInt16(buffer,record+8)==0) {
                        ulong mask=unchecked((ulong)Marshal.ReadInt64(buffer,record)); if(mask!=0) cores.Add(mask);
                    }
                }
                offset+=size;
            }
            if(cores.Count==0) throw new InvalidOperationException("No supported CPU cores found.");
            return cores;
        } finally {Marshal.FreeHGlobal(buffer);}
    }
    public static int Bits(ulong value) {int count=0;while(value!=0) {value&=value-1;count++;}return count;}
    public static ulong Pair(ulong available,IEnumerable<ulong> topology,IEnumerable<ulong> assignments) {
        if(available==0) throw new InvalidOperationException("No allowed processors available.");
        var cores=topology.Select(mask=>mask&available).Where(mask=>mask!=0).Distinct().ToList();
        ulong covered=cores.Aggregate(0UL,(all,mask)=>all|mask);
        // Unknown logical processors are treated as separate cores, never guessed as siblings.
        for(int bit=0;bit<64;bit++) if((available&(1UL<<bit))!=0 && (covered&(1UL<<bit))==0) cores.Add(1UL<<bit);
        var loads=assignments.ToArray(); ulong chosen=0,firstCore=0;
        for(int slot=0;slot<2;slot++) {
            var candidates=cores.Where(core=>(core&~chosen)!=0 && (slot==0 || core!=firstCore)).ToArray();
            if(candidates.Length==0) candidates=cores.Where(core=>(core&~chosen)!=0).ToArray();
            if(candidates.Length==0) break;
            ulong selectedCore=candidates.OrderBy(core=>loads.Sum(mask=>Bits(mask&core))).ThenBy(core=>core).First();
            ulong logical=Enumerable.Range(0,64).Select(bit=>1UL<<bit).Where(mask=>(selectedCore&mask)!=0 && (chosen&mask)==0)
                .OrderBy(mask=>loads.Count(assigned=>(assigned&mask)!=0)).ThenBy(mask=>mask).First();
            chosen|=logical; if(slot==0) firstCore=selectedCore;
        }
        return chosen;
    }
}
