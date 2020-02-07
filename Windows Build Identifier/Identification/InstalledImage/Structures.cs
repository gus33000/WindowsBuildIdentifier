namespace WindowsBuildIdentifier.Identification.InstalledImage
{
    internal class VersionInfo1
    {
        public ulong MajorVersion;
        public ulong MinorVersion;
        public ulong BuildNumber;
        public ulong DeltaVersion;
        public string BranchName;
        public string CompileDate;
        public MachineType Architecture;
        public BuildType BuildType;
    }

    internal class VersionInfo2
    {
        public ulong MajorVersion;
        public ulong MinorVersion;
        public ulong BuildNumber;
        public ulong DeltaVersion;
        public string BranchName;
        public string CompileDate;
        public string Tag;
        public Licensing Licensing;
        public string[] LanguageCodes;
    }
}
