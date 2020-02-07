namespace WindowsBuildIdentifier.Identification.InstalledImage
{
    internal class VersionInfo1
    {
        public WindowsVersion version;
        public MachineType Architecture;
        public BuildType BuildType;
    }

    internal class VersionInfo2
    {
        public WindowsVersion version;
        public string Tag;
        public Licensing Licensing;
        public string[] LanguageCodes;
    }
}
