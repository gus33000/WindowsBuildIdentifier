using DiscUtils.Vhd;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using System.IO;
using System;

namespace WindowsBuildIdentifier
{
    public class VHDInstallProviderInterface : WindowsInstallProviderInterface
    {
        private readonly Stream _vhdstream;
        private readonly Disk _vhd;
        private readonly NtfsFileSystem _ntfs;

        public VHDInstallProviderInterface(Stream vhdstream)
        {
            _vhdstream = vhdstream;
            _vhd = new Disk(_vhdstream, DiscUtils.Streams.Ownership.Dispose);

            PartitionInfo part = null;
            foreach (var partition in _vhd.Partitions.Partitions)
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

        public string ExpandFile(string Entry)
        {
            var tmp = Path.GetTempFileName();
            using (var srcstrm = _ntfs.OpenFile(Entry, FileMode.Open))
            {
                using var dststrm = new FileStream(tmp, FileMode.Append);
                srcstrm.CopyTo(dststrm);
            }

            return tmp;
        }

        public string[] GetFileSystemEntries()
        {
            return _ntfs.GetFiles("", "*.*", SearchOption.AllDirectories);
        }
    }
}
