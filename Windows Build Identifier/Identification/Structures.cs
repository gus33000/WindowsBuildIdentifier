using System.Collections.Generic;

namespace WindowsBuildIdentifier.Identification
{
    public enum BuildType
    {
        fre,
        chk
    }

    public enum Type
    {
        Client,
        Server,
        ServerV
    }

    public enum Licensing
    {
        Retail,
        OEM,
        Volume
    }

    public class Report
    {
        public ulong MajorVersion;
        public ulong MinorVersion;
        public ulong BuildNumber;
        public ulong DeltaVersion;
        public string BranchName;
        public string CompileDate;
        public string Tag;
        public MachineType Architecture;
        public BuildType BuildType;
        public HashSet<Type> Type = new HashSet<Type>();
        public string Sku;
        public string[] Editions;
        public Licensing Licensing;
        public string[] LanguageCodes;
    }

    public enum MachineType : ushort
    {
        unknown = 0x0,
        am33 = 0x1d3,
        amd64 = 0x8664,
        arm = 0x1c0,
        arm64 = 0xaa64,
        woa = 0x1c4,
        ebc = 0xebc,
        x86 = 0x14c,
        ia64 = 0x200,
        m32r = 0x9041,
        mips16 = 0x266,
        mipsfpu = 0x366,
        mipsfpu16 = 0x466,
        powerpc = 0x1f0,
        powerpcfp = 0x1f1,
        r4000 = 0x166,
        sh3 = 0x1a2,
        sh3dsp = 0x1a3,
        sh4 = 0x1a6,
        sh5 = 0x1a8,
        thumb = 0x1c2,
        wcemipsv2 = 0x169,
    }

    public class WindowsVersion
    {
        public ulong MajorVersion;
        public ulong MinorVersion;
        public ulong BuildNumber;
        public ulong DeltaVersion;
        public string BranchName;
        public string CompileDate;
    }
}
