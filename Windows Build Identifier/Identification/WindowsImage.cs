using System.Collections.Generic;

namespace WindowsBuildIdentifier.Identification;

public class WindowsImage
{
    public MachineType Architecture;
    public string BaseSku;
    public string BranchName;
    public ulong BuildNumber;
    public BuildType BuildType;
    public string CompileDate;
    public ulong DeltaVersion;
    public string[] Editions;
    public string[] LanguageCodes;
    public Licensing Licensing;
    public ulong MajorVersion;
    public ulong MinorVersion;
    public string Sku;
    public string Tag;
    public HashSet<Type> Types = new();
}