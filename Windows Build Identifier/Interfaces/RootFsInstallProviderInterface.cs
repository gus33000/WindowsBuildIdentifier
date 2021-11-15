using DiscUtils;
using DiscUtils.Streams;
using System.IO;

namespace WindowsBuildIdentifier.Interfaces
{
    public class RootFsInstallProviderInterface : IWindowsInstallProviderInterface
    {
        private readonly IFileSystem _fileSystem;

        public RootFsInstallProviderInterface(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public void Close()
        {
        }

        public string ExpandFile(string entry)
        {
            string tmp = Path.GetTempFileName();
            using SparseStream srcstrm = _fileSystem.OpenFile(entry, FileMode.Open);
            using FileStream dststrm = new(tmp, FileMode.Append);
            srcstrm.CopyTo(dststrm);

            return tmp;
        }

        public string[] GetFileSystemEntries()
        {
            return _fileSystem.GetFiles("", "*.*", SearchOption.AllDirectories);
        }
    }
}