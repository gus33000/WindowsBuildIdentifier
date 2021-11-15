using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using DiscUtils.Vdi;
using System.IO;

namespace WindowsBuildIdentifier.Interfaces
{
    public class VdiInstallProviderInterface : IWindowsInstallProviderInterface
    {
        private readonly NtfsFileSystem _ntfs;
        private readonly Disk _vhd;
        private readonly Stream _vhdstream;

        public VdiInstallProviderInterface(Stream vhdstream)
        {
            _vhdstream = vhdstream;
            _vhd = new Disk(_vhdstream, Ownership.Dispose);

            PartitionInfo part = null;
            foreach (PartitionInfo partition in _vhd.Partitions.Partitions)
            {
                if (part == null)
                {
                    part = partition;
                }
                else
                {
                    if (partition.SectorCount > part.SectorCount)
                    {
                        part = partition;
                    }
                }
            }

            _ntfs = new NtfsFileSystem(part.Open());
        }

        public void Close()
        {
            _vhd.Dispose();
        }

        public string ExpandFile(string entry)
        {
            string tmp = Path.GetTempFileName();
            using SparseStream srcstrm = _ntfs.OpenFile(entry, FileMode.Open);
            using FileStream dststrm = new(tmp, FileMode.Append);
            srcstrm.CopyTo(dststrm);

            return tmp;
        }

        public string[] GetFileSystemEntries()
        {
            return _ntfs.GetFiles("", "*.*", SearchOption.AllDirectories);
        }
    }
}