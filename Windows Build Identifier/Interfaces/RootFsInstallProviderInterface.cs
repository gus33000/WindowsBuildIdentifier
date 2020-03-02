using DiscUtils;
using System.IO;

namespace WindowsBuildIdentifier.Interfaces
{
    public class RootFsInstallProviderInterface : WindowsInstallProviderInterface
    {
        private IFileSystem _fileSystem;
        public RootFsInstallProviderInterface(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public void Close()
        {
            
        }

        public string ExpandFile(string Entry)
        {
            var tmp = Path.GetTempFileName();
            using (var srcstrm = _fileSystem.OpenFile(Entry, FileMode.Open))
            {
                using var dststrm = new FileStream(tmp, FileMode.Append);
                srcstrm.CopyTo(dststrm);
            }

            return tmp;
        }

        public string[] GetFileSystemEntries()
        {
            return _fileSystem.GetFiles("", "*.*", SearchOption.AllDirectories);
        }
    }
}
