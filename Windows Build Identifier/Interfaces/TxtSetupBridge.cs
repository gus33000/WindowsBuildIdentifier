using DiscUtils;
using DiscUtils.Registry;
using DiscUtils.Streams;
using IniParser;
using IniParser.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public enum ProductMasks
        {
            VER_SUITE_SMALLBUSINESS             = 0x00000001,
            VER_SUITE_SMALLBUSINESS_RESTRICTED  = 0x00000020,

            VER_SUITE_EMBEDDEDNT                = 0x00000040,
            VER_SUITE_EMBEDDED_RESTRICTED       = 0x00000800,

            VER_SUITE_TERMINAL                  = 0x00000010,
            VER_SUITE_SINGLEUSERTS              = 0x00000100,
            VER_SUITE_MULTIUSERTS               = 0x00020000,

            VER_SUITE_ENTERPRISE                = 0x00000002,
            VER_SUITE_BACKOFFICE                = 0x00000004,
            VER_SUITE_COMMUNICATIONS            = 0x00000008,
            VER_SUITE_DATACENTER                = 0x00000080,
            VER_SUITE_PERSONAL                  = 0x00000200,
            VER_SUITE_BLADE                     = 0x00000400,
            VER_SUITE_SECURITY_APPLIANCE        = 0x00001000,
            VER_SUITE_STORAGE_SERVER            = 0x00002000,
            VER_SUITE_COMPUTE_SERVER            = 0x00004000,
            VER_SUITE_WH_SERVER                 = 0x00008000
        }

        public string GetSkuFromTxtSetupMedia(ulong build)
        {
            string sku = "";
            RegistryHive hive = new RegistryHive(_originalFs.OpenFile(Path.Combine(_pathInFs, "SETUPREG.HIV"), FileMode.Open));
            var subkey = hive.Root.OpenSubKey(@"ControlSet001\Services\setupdd");
            byte[] buffer = (byte[])subkey.GetValue("");

            if (buffer != null && buffer.Length >= 16)
            {
                // value is storred in big endian...
                byte[] productsuiteData = buffer[12..16];

                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(productsuiteData);

                int parsed = BitConverter.ToInt32(productsuiteData);

                if ((parsed & (int)ProductMasks.VER_SUITE_SMALLBUSINESS_RESTRICTED) == (int)ProductMasks.VER_SUITE_SMALLBUSINESS_RESTRICTED)
                {
                    sku = "SmallBusinessServerRestricted";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_SMALLBUSINESS) == (int)ProductMasks.VER_SUITE_SMALLBUSINESS)
                {
                    sku = "SmallBusinessServer";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_DATACENTER) == (int)ProductMasks.VER_SUITE_DATACENTER)
                {
                    sku = "DatacenterServer";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_ENTERPRISE) == (int)ProductMasks.VER_SUITE_ENTERPRISE)
                {
                    sku = "EnterpriseServer";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_EMBEDDED_RESTRICTED) == (int)ProductMasks.VER_SUITE_EMBEDDED_RESTRICTED)
                {
                    sku = "EmbeddedRestricted";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_EMBEDDEDNT) == (int)ProductMasks.VER_SUITE_EMBEDDEDNT)
                {
                    sku = "Embedded";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_BACKOFFICE) == (int)ProductMasks.VER_SUITE_BACKOFFICE)
                {
                    sku = "BackOfficeServer";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_COMMUNICATIONS) == (int)ProductMasks.VER_SUITE_COMMUNICATIONS)
                {
                    sku = "CommunicationsServer";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_SINGLEUSERTS) == (int)ProductMasks.VER_SUITE_SINGLEUSERTS)
                {
                    sku = "TerminalServerSingleUser";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_MULTIUSERTS) == (int)ProductMasks.VER_SUITE_MULTIUSERTS)
                {
                    sku = "TerminalServerMultiUser";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_TERMINAL) == (int)ProductMasks.VER_SUITE_TERMINAL)
                {
                    sku = "TerminalServer";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_PERSONAL) == (int)ProductMasks.VER_SUITE_PERSONAL)
                {
                    sku = "Personal";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_BLADE) == (int)ProductMasks.VER_SUITE_BLADE)
                {
                    sku = "WebServer";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_SECURITY_APPLIANCE) == (int)ProductMasks.VER_SUITE_SECURITY_APPLIANCE)
                {
                    sku = "SecurityApplianceServer";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_STORAGE_SERVER) == (int)ProductMasks.VER_SUITE_STORAGE_SERVER)
                {
                    sku = "StorageServer";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_COMPUTE_SERVER) == (int)ProductMasks.VER_SUITE_COMPUTE_SERVER)
                {
                    sku = "ComputeServer";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_WH_SERVER) == (int)ProductMasks.VER_SUITE_WH_SERVER)
                {
                    sku = "HomeServer";
                }
            }

            if (sku == "")
            {
                var parser = new FileIniDataParser();

                parser.Parser.Configuration.AllowDuplicateSections = true;
                parser.Parser.Configuration.AllowDuplicateKeys = true;
                parser.Parser.Configuration.SkipInvalidLines = true;

                var stream = _originalFs.OpenFile(_pathInFs + @"\txtsetup.sif", FileMode.Open);
                var txtSetupSif = new StreamReader(stream);
                IniData data = parser.ReadData(txtSetupSif);


                if (data["SetupData"]["ProductSuite"] != null)
                {
                    sku = data["SetupData"]["ProductSuite"].Replace(" ", "").Replace("\"", "");
                }

                string cdtag = null;
                if (data.Sections.Any(x => x.SectionName == "SourceDisksNames"))
                {
                    cdtag = data["SourceDisksNames"]["_x"].Split(",")[1].Replace("\"", "");
                }
                else if (data.Sections.Any(x => x.SectionName == "Media"))
                {
                    cdtag = data["Media"]["dx"].Split(",")[1].Replace("\"", "");
                }

                if (cdtag != null)
                {
                    if (cdtag.Contains("%"))
                    {
                        var strings = data["Strings"];
                        var nonescapedpart = cdtag.Split("%")[1];
                        var replacement = strings[nonescapedpart];
                        cdtag = cdtag.Replace("%" + nonescapedpart + "%", replacement).Replace("\"", "");
                    }

                    string editionletter = cdtag.Replace("cdrom.", "").Split(".")[0];
                    char _editionletter2 = '\0';
                    if (editionletter.Length >= 2)
                        _editionletter2 = editionletter[^2];
                    char _editionletter = editionletter[^1];

                    if (_editionletter == 'p')
                    {
                        sku = "Professional";
                    }
                    else if (_editionletter == 'c')
                    {
                        sku = "Personal";
                    }
                    else if (_editionletter == 'w')
                    {
                        sku = "Workstation";
                    }
                    else if (_editionletter == 'b')
                    {
                        sku = "WebServer";
                    }
                    else if (_editionletter == 's')
                    {
                        sku = "StandardServer";
                        if (_editionletter2 == 't')
                        {
                            sku = "TerminalServer";
                        }
                    }
                    else if (_editionletter == 'a')
                    {
                        if (build < 2202)
                        {
                            sku = "AdvancedServer";
                        }
                        else
                        {
                            sku = "EnterpriseServer";
                        }
                    }
                    else if (_editionletter == 'l')
                    {
                        sku = "SmallbusinessServer";
                    }
                    else if (_editionletter == 'd')
                    {
                        sku = "DatacenterServer";
                    }
                }
            }

            return sku;
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
                int ret;
                if (directoryId == "" || !int.TryParse(directoryId, out ret) || dstdirs.GetKeyData(directoryId) == null)
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

            if (_originalFs.Exists(_pathInFs + @"\" + entry.DiscLocation))
            {
                return _originalFs.OpenFile(_pathInFs + @"\" + entry.DiscLocation, mode);
            }

            var tmpFile = Path.GetTempFileName();
            var exttmpFile = Path.GetTempFileName();
            File.Delete(exttmpFile);
            using (var strm = _originalFs.OpenFile(_pathInFs + @"\" + entry.DiscLocation[0..^1] + "_", mode))
            using (var strmo = File.Create(tmpFile))
            {
                strm.CopyTo(strmo);
            }

            var proc = new Process();
            proc.StartInfo = new ProcessStartInfo("expand.exe", $"\"{tmpFile}\" \"{exttmpFile}\"");
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;

            proc.Start();
            proc.WaitForExit();

            var memstrm = new MemoryStream();
            using (var strm = File.OpenRead(exttmpFile))
            {
                strm.CopyTo(memstrm);
            }
            memstrm.Seek(0, SeekOrigin.Begin);
            File.Delete(tmpFile);
            File.Delete(exttmpFile);

            return SparseStream.FromStream(memstrm, Ownership.Dispose);
        }

        public SparseStream OpenFile(string path, FileMode mode, FileAccess access)
        {
            var entry = entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));

            if (_originalFs.Exists(_pathInFs + @"\" + entry.DiscLocation))
            {
                return _originalFs.OpenFile(_pathInFs + @"\" + entry.DiscLocation, mode, access);
            }

            var tmpFile = Path.GetTempFileName();
            var exttmpFile = Path.GetTempFileName();
            File.Delete(exttmpFile);
            using (var strm = _originalFs.OpenFile(_pathInFs + @"\" + entry.DiscLocation[0..^1] + "_", mode, access))
            using (var strmo = File.Create(tmpFile))
            {
                strm.CopyTo(strmo);
            }

            var proc = new Process();
            proc.StartInfo = new ProcessStartInfo("expand.exe", $"\"{tmpFile}\" \"{exttmpFile}\"");
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;

            proc.Start();
            proc.WaitForExit();

            var memstrm = new MemoryStream();
            using (var strm = File.OpenRead(exttmpFile))
            {
                strm.CopyTo(memstrm);
            }
            memstrm.Seek(0, SeekOrigin.Begin);
            File.Delete(tmpFile);
            File.Delete(exttmpFile);

            return SparseStream.FromStream(memstrm, Ownership.Dispose);
        }
    }
}
