using DiscUtils;
using DiscUtils.Internal;
using DiscUtils.Streams;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WindowsBuildIdentifier.Interfaces
{
    public class ArchiveBridge : IFileSystem
    {
        private readonly ArchiveFile _archiveFile;

        public ArchiveBridge(ArchiveFile archiveFile)
        {
            _archiveFile = archiveFile;
        }

        public bool CanWrite => false;

        public bool IsThreadSafe => true;

        // todo
        public DiscDirectoryInfo Root => null;

        public long Size => _archiveFile.GetArchiveSize();

        public long UsedSpace => _archiveFile.GetArchiveSize();

        public long AvailableSpace => 0;

        public bool DirectoryExists(string path)
        {
            return _archiveFile.Entries.Any(x => x.IsFolder && x.FileName == path);
        }

        public bool Exists(string path)
        {
            return _archiveFile.Entries.Any(x => x.FileName == path);
        }

        public bool FileExists(string path)
        {
            return _archiveFile.Entries.Any(x => !x.IsFolder && x.FileName == path);
        }

        public FileAttributes GetAttributes(string path)
        {
            Entry entry = _archiveFile.Entries.First(x => x.FileName == path);
            return (FileAttributes)entry.Attributes;
        }

        public string[] GetDirectories(string path)
        {
            return GetDirectories(path, null, SearchOption.TopDirectoryOnly);
        }

        public string[] GetDirectories(string path, string searchPattern)
        {
            return GetDirectories(path, searchPattern, SearchOption.TopDirectoryOnly);
        }

        public string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            IEnumerable<string> prereq = _archiveFile.Entries
                .Where(x => x.IsFolder && x.FileName.StartsWith(path) && re.Match(x.FileName).Success)
                .Select(x => x.FileName);
            switch (searchOption)
            {
                case SearchOption.AllDirectories:
                    {
                        return prereq.ToArray();
                    }
                case SearchOption.TopDirectoryOnly:
                    {
                        int expectedCount = path.Count(x => x == '\\');
                        if (!path.EndsWith("\\"))
                        {
                            expectedCount++;
                        }

                        return prereq.Where(x => x.Count(x => x == '\\') == expectedCount).ToArray();
                    }
            }

            return Array.Empty<string>();
        }

        public DiscDirectoryInfo GetDirectoryInfo(string path)
        {
            throw new NotImplementedException();
        }

        public DiscFileInfo GetFileInfo(string path)
        {
            throw new NotImplementedException();
        }

        public string[] GetFileSystemEntries(string path)
        {
            throw new NotImplementedException();
        }

        public string[] GetFileSystemEntries(string path, string searchPattern)
        {
            throw new NotImplementedException();
        }

        public DiscFileSystemInfo GetFileSystemInfo(string path)
        {
            throw new NotImplementedException();
        }

        public byte[] ReadBootCode()
        {
            throw new NotImplementedException();
        }

        public long GetFileLength(string path)
        {
            Entry entry = _archiveFile.Entries.First(x => x.FileName == path);
            return (long)entry.Size;
        }

        public string[] GetFiles(string path)
        {
            return GetFiles(path, null, SearchOption.TopDirectoryOnly);
        }

        public string[] GetFiles(string path, string searchPattern)
        {
            return GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
        }

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            IEnumerable<string> prereq = _archiveFile.Entries
                .Where(x => !x.IsFolder && x.FileName.StartsWith(path) && re.Match(x.FileName).Success)
                .Select(x => x.FileName);
            switch (searchOption)
            {
                case SearchOption.AllDirectories:
                    {
                        return prereq.ToArray();
                    }
                case SearchOption.TopDirectoryOnly:
                    {
                        int expectedCount = path.Count(x => x == '\\');
                        if (!path.EndsWith("\\"))
                        {
                            expectedCount++;
                        }

                        return prereq.Where(x => x.Count(x => x == '\\') == expectedCount).ToArray();
                    }
            }

            return Array.Empty<string>();
        }

        public DateTime GetLastAccessTime(string path)
        {
            Entry entry = _archiveFile.Entries.First(x => x.FileName == path);
            return entry.LastAccessTime;
        }

        public DateTime GetLastAccessTimeUtc(string path)
        {
            Entry entry = _archiveFile.Entries.First(x => x.FileName == path);
            return entry.LastAccessTime.ToUniversalTime();
        }

        public DateTime GetLastWriteTime(string path)
        {
            Entry entry = _archiveFile.Entries.First(x => x.FileName == path);
            return entry.LastWriteTime;
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            Entry entry = _archiveFile.Entries.First(x => x.FileName == path);
            return entry.LastWriteTime.ToUniversalTime();
        }

        public DateTime GetCreationTime(string path)
        {
            Entry entry = _archiveFile.Entries.First(x => x.FileName == path);
            return entry.CreationTime;
        }

        public DateTime GetCreationTimeUtc(string path)
        {
            Entry entry = _archiveFile.Entries.First(x => x.FileName == path);
            return entry.CreationTime.ToUniversalTime();
        }

        public SparseStream OpenFile(string path, FileMode mode)
        {
            Entry entry = _archiveFile.Entries.First(x => x.FileName == path);
            MemoryStream memstrm = new();
            entry.Extract(memstrm);
            return SparseStream.FromStream(memstrm, Ownership.Dispose);
        }

        public SparseStream OpenFile(string path, FileMode mode, FileAccess access)
        {
            Entry entry = _archiveFile.Entries.First(x => x.FileName == path);
            MemoryStream memstrm = new();
            entry.Extract(memstrm);
            memstrm.Seek(0, SeekOrigin.Begin);
            return SparseStream.FromStream(memstrm, Ownership.Dispose);
        }

        #region writing routines

        public void CopyFile(string sourceFile, string destinationFile)
        {
            throw new NotImplementedException();
        }

        public void CreateDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public void DeleteDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            throw new NotImplementedException();
        }

        public void DeleteFile(string path)
        {
            throw new NotImplementedException();
        }

        public void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName)
        {
            throw new NotImplementedException();
        }

        public void MoveFile(string sourceName, string destinationName)
        {
            throw new NotImplementedException();
        }

        public void MoveFile(string sourceName, string destinationName, bool overwrite)
        {
            throw new NotImplementedException();
        }

        public void SetAttributes(string path, FileAttributes newValue)
        {
            throw new NotImplementedException();
        }

        public void SetCreationTime(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        public void SetCreationTimeUtc(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        public void SetLastAccessTime(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        public void SetLastAccessTimeUtc(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        public void SetLastWriteTime(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        public void SetLastWriteTimeUtc(string path, DateTime newTime)
        {
            throw new NotImplementedException();
        }

        public void CopyFile(string sourceFile, string destinationFile, bool overwrite)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}