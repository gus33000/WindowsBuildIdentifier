using DiscUtils;
using DiscUtils.Streams;
using IniParser;
using IniParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WindowsBuildIdentifier.Interfaces
{
    public class TxtSetupBridge : IFileSystem
    {

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

        private IFileSystem _originalFs;
        private string _pathInFs;

        private TxtSetupFileEntry[] entries;

        private class TxtSetupFileEntry
        {
            public string Path { get; set; }
            public string DiscLocation { get; set; }
        }

        private void LoadTxtSetupData(IFileSystem originalFs, string pathInFs)
        {
            List<TxtSetupFileEntry> fileList = new List<TxtSetupFileEntry>();

            var parser = new FileIniDataParser();

            parser.Parser.Configuration.AllowDuplicateSections = true;
            parser.Parser.Configuration.AllowDuplicateKeys = true;
            parser.Parser.Configuration.SkipInvalidLines = true;

            var stream = originalFs.OpenFile(pathInFs + @"\txtsetup.sif", FileMode.Open);
            var txtSetupSif = new StreamReader(stream);
            IniData data = parser.ReadData(txtSetupSif);

            var srcdisk = data["SourceDisksFiles"];
            var dstdirs = data["WinntDirectories"];
            var strings = data["Strings"];
            var setupData = data["SetupData"];

            string arch = "x86";
            if (setupData["Architecture"] != null)
            {
                arch = setupData["Architecture"].Replace("i386", "x86");
            }

            var srcdisk2 = data["SourceDisksFiles." + arch];
            var dstdirs2 = data["WinntDirectories." + arch];

            srcdisk.Merge(srcdisk2);
            dstdirs.Merge(dstdirs2);

            int dirid = 7;
            int filenameid = 10;

            if (srcdisk.Count == 0)
            {
                dirid = 3;
                filenameid = 6;
                srcdisk = data["Files"];

                srcdisk2 = data["Files.SetupMedia"];
                srcdisk.Merge(srcdisk2);
            }

            var defdir = setupData["DefaultPath"];

            foreach (var el in srcdisk)
            {
                var vdata = el.Value.Split(',');

                var filename = el.KeyName;
                if (vdata.Length >= filenameid + 1 && !string.IsNullOrEmpty(vdata[filenameid]))
                {
                    filename = vdata[filenameid];
                }

                filename = filename.Replace("\"", "");

                if (filename.Contains("%"))
                {
                    var nonescapedpart = filename.Split("%")[1];
                    var replacement = strings[nonescapedpart];
                    filename = filename.Replace("%" + nonescapedpart + "%", replacement).Replace("\"", "");
                }

                var directoryId = vdata[dirid];
                string directory;
                if (directoryId == "")
                {
                    directory = "system32";
                }
                else
                {
                    directory = dstdirs.GetKeyData(directoryId).Value.Replace("\"", "");
                }

                if (directory.Contains("%"))
                {
                    var nonescapedpart = directory.Split("%")[1];
                    var replacement = strings[nonescapedpart];
                    directory = directory.Replace("%" + nonescapedpart + "%", replacement).Replace("\"", "");
                }

                if (directory == @"\")
                {
                    fileList.Add(new TxtSetupFileEntry() { Path = Path.Combine(defdir, filename), DiscLocation = el.KeyName });
                }
                else
                {
                    fileList.Add(new TxtSetupFileEntry() { Path = Path.Combine(defdir, directory, filename), DiscLocation = el.KeyName });
                }
            }

            entries = fileList.ToArray();
        }

        public TxtSetupBridge(IFileSystem originalFs, string pathInFs)
        {
            LoadTxtSetupData(originalFs, pathInFs);
            _originalFs = originalFs;
            _pathInFs = pathInFs;
        }

        public bool CanWrite => false;

        public bool IsThreadSafe => true;

        public DiscDirectoryInfo Root => null;

        public long Size => 0;

        public long UsedSpace => 0;

        public long AvailableSpace => 0;

        public bool DirectoryExists(string path)
        {
            return entries.Any(x => x.Path.StartsWith(@"\" + path + @"\", StringComparison.InvariantCultureIgnoreCase));
        }

        public bool Exists(string path)
        {
            return DirectoryExists(path) || FileExists(path);
        }

        public bool FileExists(string path)
        {
            return entries.Any(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
        }

        public FileAttributes GetAttributes(string path)
        {
            var entry = entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetAttributes(_pathInFs + @"\" + entry.DiscLocation);
        }

        public DateTime GetCreationTime(string path)
        {
            var entry = entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetCreationTime(_pathInFs + @"\" + entry.DiscLocation);
        }

        public DateTime GetCreationTimeUtc(string path)
        {
            var entry = entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetCreationTimeUtc(_pathInFs + @"\" + entry.DiscLocation);
        }

        public string[] GetDirectories(string path)
        {
            return entries.Select(x => string.Join("\\", x.Path.Split("\\").Skip(1).Reverse().Skip(1))).Distinct().ToArray();
        }

        public string[] GetDirectories(string path, string searchPattern)
        {
            return GetDirectories(path, searchPattern, SearchOption.TopDirectoryOnly);
        }

        public string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            Regex re = DiscUtils.Internal.Utilities.ConvertWildcardsToRegEx(searchPattern);

            var prereq = GetDirectories(path).Where(x => x.StartsWith(path) && re.Match(x).Success).Select(x => x);
            switch (searchOption)
            {
                case SearchOption.AllDirectories:
                    {
                        return prereq.ToArray();
                    }
                case SearchOption.TopDirectoryOnly:
                    {
                        var expectedCount = path.Count(x => x == '\\');
                        if (!path.EndsWith("\\"))
                            expectedCount++;

                        return prereq.Where(x => x.Count(x => x == '\\') == expectedCount).ToArray();
                    }
            }

            return new string[0];
        }

        public long GetFileLength(string path)
        {
            var entry = entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetFileLength(_pathInFs + @"\" + entry.DiscLocation);
        }

        public string[] GetFiles(string path)
        {
            return entries.Select(x => string.Join("\\", x.Path.Split("\\").Skip(1))).Distinct().ToArray();
        }

        public string[] GetFiles(string path, string searchPattern)
        {
            return GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
        }

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            Regex re = DiscUtils.Internal.Utilities.ConvertWildcardsToRegEx(searchPattern);

            var prereq = GetFiles(path).Where(x => x.StartsWith(path) && re.Match(x).Success).Select(x => x);
            switch (searchOption)
            {
                case SearchOption.AllDirectories:
                    {
                        return prereq.ToArray();
                    }
                case SearchOption.TopDirectoryOnly:
                    {
                        var expectedCount = path.Count(x => x == '\\');
                        if (!path.EndsWith("\\"))
                            expectedCount++;

                        return prereq.Where(x => x.Count(x => x == '\\') == expectedCount).ToArray();
                    }
            }

            return new string[0];
        }

        public DateTime GetLastAccessTime(string path)
        {
            var entry = entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetLastAccessTime(_pathInFs + @"\" + entry.DiscLocation);
        }

        public DateTime GetLastAccessTimeUtc(string path)
        {
            var entry = entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetLastAccessTimeUtc(_pathInFs + @"\" + entry.DiscLocation);
        }

        public DateTime GetLastWriteTime(string path)
        {
            var entry = entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetLastWriteTime(_pathInFs + @"\" + entry.DiscLocation);
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            var entry = entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetLastWriteTimeUtc(_pathInFs + @"\" + entry.DiscLocation);
        }

        public SparseStream OpenFile(string path, FileMode mode)
        {
            var entry = entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.OpenFile(_pathInFs + @"\" + entry.DiscLocation, mode);
        }

        public SparseStream OpenFile(string path, FileMode mode, FileAccess access)
        {
            var entry = entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.OpenFile(_pathInFs + @"\" + entry.DiscLocation, mode, access);
        }
    }
}
