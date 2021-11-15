using DiscUtils;
using DiscUtils.Internal;
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
        public enum ProductMasks
        {
            VER_SUITE_SMALLBUSINESS = 0x00000001,
            VER_SUITE_SMALLBUSINESS_RESTRICTED = 0x00000020,

            VER_SUITE_EMBEDDEDNT = 0x00000040,
            VER_SUITE_EMBEDDED_RESTRICTED = 0x00000800,

            VER_SUITE_TERMINAL = 0x00000010,
            VER_SUITE_SINGLEUSERTS = 0x00000100,
            VER_SUITE_MULTIUSERTS = 0x00020000,

            VER_SUITE_ENTERPRISE = 0x00000002,
            VER_SUITE_BACKOFFICE = 0x00000004,
            VER_SUITE_COMMUNICATIONS = 0x00000008,
            VER_SUITE_DATACENTER = 0x00000080,
            VER_SUITE_PERSONAL = 0x00000200,
            VER_SUITE_BLADE = 0x00000400,
            VER_SUITE_SECURITY_APPLIANCE = 0x00001000,
            VER_SUITE_STORAGE_SERVER = 0x00002000,
            VER_SUITE_COMPUTE_SERVER = 0x00004000,
            VER_SUITE_WH_SERVER = 0x00008000
        }

        private readonly IFileSystem _originalFs;
        private readonly string _pathInFs;

        private TxtSetupFileEntry[] _entries;

        public TxtSetupBridge(IFileSystem originalFs, string pathInFs)
        {
            LoadTxtSetupData(originalFs, pathInFs);
            _originalFs = originalFs;
            _pathInFs = pathInFs;
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

        public bool CanWrite => false;

        public bool IsThreadSafe => true;

        public DiscDirectoryInfo Root => null;

        public long Size => 0;

        public long UsedSpace => 0;

        public long AvailableSpace => 0;

        public bool DirectoryExists(string path)
        {
            return _entries.Any(x => x.Path.StartsWith(@"\" + path + @"\", StringComparison.InvariantCultureIgnoreCase));
        }

        public bool Exists(string path)
        {
            return DirectoryExists(path) || FileExists(path);
        }

        public bool FileExists(string path)
        {
            return _entries.Any(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
        }

        public FileAttributes GetAttributes(string path)
        {
            TxtSetupFileEntry entry = _entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetAttributes(_pathInFs + @"\" + entry.DiscLocation);
        }

        public DateTime GetCreationTime(string path)
        {
            TxtSetupFileEntry entry = _entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetCreationTime(_pathInFs + @"\" + entry.DiscLocation);
        }

        public DateTime GetCreationTimeUtc(string path)
        {
            TxtSetupFileEntry entry = _entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetCreationTimeUtc(_pathInFs + @"\" + entry.DiscLocation);
        }

        public string[] GetDirectories(string path)
        {
            return _entries.Select(x => string.Join("\\", x.Path.Split("\\").Skip(1).Reverse().Skip(1))).Distinct()
                .ToArray();
        }

        public string[] GetDirectories(string path, string searchPattern)
        {
            return GetDirectories(path, searchPattern, SearchOption.TopDirectoryOnly);
        }

        public string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            IEnumerable<string> prereq = GetDirectories(path).Where(x => x.StartsWith(path) && re.Match(x).Success).Select(x => x);
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

        public long GetFileLength(string path)
        {
            TxtSetupFileEntry entry = _entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetFileLength(_pathInFs + @"\" + entry.DiscLocation);
        }

        public string[] GetFiles(string path)
        {
            return _entries.Select(x => string.Join("\\", x.Path.Split("\\").Skip(1))).Distinct().ToArray();
        }

        public string[] GetFiles(string path, string searchPattern)
        {
            return GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
        }

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            IEnumerable<string> prereq = GetFiles(path).Where(x => x.StartsWith(path) && re.Match(x).Success).Select(x => x);
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
            TxtSetupFileEntry entry = _entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetLastAccessTime(_pathInFs + @"\" + entry.DiscLocation);
        }

        public DateTime GetLastAccessTimeUtc(string path)
        {
            TxtSetupFileEntry entry = _entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetLastAccessTimeUtc(_pathInFs + @"\" + entry.DiscLocation);
        }

        public DateTime GetLastWriteTime(string path)
        {
            TxtSetupFileEntry entry = _entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetLastWriteTime(_pathInFs + @"\" + entry.DiscLocation);
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            TxtSetupFileEntry entry = _entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));
            return _originalFs.GetLastWriteTimeUtc(_pathInFs + @"\" + entry.DiscLocation);
        }

        public SparseStream OpenFile(string path, FileMode mode)
        {
            TxtSetupFileEntry entry = _entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));

            if (_originalFs.Exists(_pathInFs + @"\" + entry.DiscLocation))
            {
                return _originalFs.OpenFile(_pathInFs + @"\" + entry.DiscLocation, mode);
            }

            string tmpFile = Path.GetTempFileName();
            string exttmpFile = Path.GetTempFileName();
            File.Delete(exttmpFile);
            using (SparseStream strm = _originalFs.OpenFile(_pathInFs + @"\" + entry.DiscLocation[..^1] + "_", mode))
            using (FileStream strmo = File.Create(tmpFile))
            {
                strm.CopyTo(strmo);
            }

            Process proc = new();
            proc.StartInfo = new ProcessStartInfo("expand.exe", $"\"{tmpFile}\" \"{exttmpFile}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            proc.Start();
            proc.WaitForExit();

            MemoryStream memstrm = new();
            using (FileStream strm = File.OpenRead(exttmpFile))
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
            TxtSetupFileEntry entry = _entries.First(x => x.Path.Equals(@"\" + path, StringComparison.InvariantCultureIgnoreCase));

            if (_originalFs.Exists(_pathInFs + @"\" + entry.DiscLocation))
            {
                return _originalFs.OpenFile(_pathInFs + @"\" + entry.DiscLocation, mode, access);
            }

            string tmpFile = Path.GetTempFileName();
            string exttmpFile = Path.GetTempFileName();
            File.Delete(exttmpFile);
            using (SparseStream strm = _originalFs.OpenFile(_pathInFs + @"\" + entry.DiscLocation[..^1] + "_", mode, access))
            using (FileStream strmo = File.Create(tmpFile))
            {
                strm.CopyTo(strmo);
            }

            Process proc = new();
            proc.StartInfo = new ProcessStartInfo("expand.exe", $"\"{tmpFile}\" \"{exttmpFile}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            proc.Start();
            proc.WaitForExit();

            MemoryStream memstrm = new();
            using (FileStream strm = File.OpenRead(exttmpFile))
            {
                strm.CopyTo(memstrm);
            }

            memstrm.Seek(0, SeekOrigin.Begin);
            File.Delete(tmpFile);
            File.Delete(exttmpFile);

            return SparseStream.FromStream(memstrm, Ownership.Dispose);
        }

        public string GetSkuFromTxtSetupMedia(ulong build)
        {
            string sku = "";
            RegistryHive hive = new(_originalFs.OpenFile(Path.Combine(_pathInFs, "SETUPREG.HIV"), FileMode.Open));
            RegistryKey subkey = hive.Root.OpenSubKey(@"ControlSet001\Services\setupdd");
            byte[] buffer = (byte[])subkey.GetValue("");

            if (buffer != null && buffer.Length >= 16)
            {
                // value is storred in big endian...
                byte[] productsuiteData = buffer[12..16];

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(productsuiteData);
                }

                int parsed = BitConverter.ToInt32(productsuiteData);

                if ((parsed & (int)ProductMasks.VER_SUITE_SMALLBUSINESS_RESTRICTED) ==
                    (int)ProductMasks.VER_SUITE_SMALLBUSINESS_RESTRICTED)
                {
                    sku = "SmallBusinessServerRestricted";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_SMALLBUSINESS) ==
                         (int)ProductMasks.VER_SUITE_SMALLBUSINESS)
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
                else if ((parsed & (int)ProductMasks.VER_SUITE_EMBEDDED_RESTRICTED) ==
                         (int)ProductMasks.VER_SUITE_EMBEDDED_RESTRICTED)
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
                else if ((parsed & (int)ProductMasks.VER_SUITE_COMMUNICATIONS) ==
                         (int)ProductMasks.VER_SUITE_COMMUNICATIONS)
                {
                    sku = "CommunicationsServer";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_SINGLEUSERTS) ==
                         (int)ProductMasks.VER_SUITE_SINGLEUSERTS)
                {
                    sku = "TerminalServerSingleUser";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_MULTIUSERTS) ==
                         (int)ProductMasks.VER_SUITE_MULTIUSERTS)
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
                else if ((parsed & (int)ProductMasks.VER_SUITE_SECURITY_APPLIANCE) ==
                         (int)ProductMasks.VER_SUITE_SECURITY_APPLIANCE)
                {
                    sku = "SecurityApplianceServer";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_STORAGE_SERVER) ==
                         (int)ProductMasks.VER_SUITE_STORAGE_SERVER)
                {
                    sku = "StorageServer";
                }
                else if ((parsed & (int)ProductMasks.VER_SUITE_COMPUTE_SERVER) ==
                         (int)ProductMasks.VER_SUITE_COMPUTE_SERVER)
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
                FileIniDataParser parser = new();

                parser.Parser.Configuration.AllowDuplicateSections = true;
                parser.Parser.Configuration.AllowDuplicateKeys = true;
                parser.Parser.Configuration.SkipInvalidLines = true;

                SparseStream stream = _originalFs.OpenFile(_pathInFs + @"\txtsetup.sif", FileMode.Open);
                StreamReader txtSetupSif = new(stream);
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
                    if (cdtag.Contains('%'))
                    {
                        KeyDataCollection strings = data["Strings"];
                        string nonescapedpart = cdtag.Split("%")[1];
                        string replacement = strings[nonescapedpart];
                        cdtag = cdtag.Replace("%" + nonescapedpart + "%", replacement).Replace("\"", "");
                    }

                    string editionletterStr = cdtag.Replace("cdrom.", "").Split(".")[0];
                    char editionletter2 = '\0';
                    if (editionletterStr.Length >= 2)
                    {
                        editionletter2 = editionletterStr[^2];
                    }

                    char editionletter = editionletterStr[^1];

                    if (editionletter == 'p')
                    {
                        sku = "Professional";
                    }
                    else if (editionletter == 'c')
                    {
                        sku = "Personal";
                    }
                    else if (editionletter == 'w')
                    {
                        sku = "Workstation";
                    }
                    else if (editionletter == 'b')
                    {
                        sku = "WebServer";
                    }
                    else if (editionletter == 's')
                    {
                        sku = "StandardServer";
                        if (editionletter2 == 't')
                        {
                            sku = "TerminalServer";
                        }
                    }
                    else if (editionletter == 'a')
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
                    else if (editionletter == 'l')
                    {
                        sku = "SmallbusinessServer";
                    }
                    else if (editionletter == 'd')
                    {
                        sku = "DatacenterServer";
                    }
                }
            }

            return sku;
        }

        private void LoadTxtSetupData(IFileSystem originalFs, string pathInFs)
        {
            List<TxtSetupFileEntry> fileList = new();

            FileIniDataParser parser = new();

            parser.Parser.Configuration.AllowDuplicateSections = true;
            parser.Parser.Configuration.AllowDuplicateKeys = true;
            parser.Parser.Configuration.SkipInvalidLines = true;

            SparseStream stream = originalFs.OpenFile(pathInFs + @"\txtsetup.sif", FileMode.Open);
            StreamReader txtSetupSif = new(stream);
            IniData data = parser.ReadData(txtSetupSif);

            KeyDataCollection srcdisk = data["SourceDisksFiles"];
            KeyDataCollection dstdirs = data["WinntDirectories"];
            KeyDataCollection strings = data["Strings"];
            KeyDataCollection setupData = data["SetupData"];

            string arch = "x86";
            if (setupData["Architecture"] != null)
            {
                arch = setupData["Architecture"].Replace("i386", "x86");
            }

            KeyDataCollection srcdisk2 = data["SourceDisksFiles." + arch];
            KeyDataCollection dstdirs2 = data["WinntDirectories." + arch];

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

            string defdir = setupData["DefaultPath"];

            foreach (KeyData el in srcdisk)
            {
                string[] vdata = el.Value.Split(',');

                string filename = el.KeyName;
                try
                {
                    if (vdata.Length >= filenameid + 1 && !string.IsNullOrEmpty(vdata[filenameid]))
                    {
                        filename = vdata[filenameid];
                    }
                }
                catch
                {
                }

                filename = filename.Replace("\"", "");

                try
                {
                    if (filename.Contains('%'))
                    {
                        string nonescapedpart = filename.Split("%")[1];
                        string replacement = strings[nonescapedpart];
                        filename = filename.Replace("%" + nonescapedpart + "%", replacement).Replace("\"", "");
                    }
                }
                catch
                {
                }

                try
                {
                    string directoryId = vdata[dirid];
                    string directory;
                    if (directoryId == "" || !int.TryParse(directoryId, out int ret) ||
                        dstdirs.GetKeyData(directoryId) == null)
                    {
                        directory = "system32";
                    }
                    else
                    {
                        directory = dstdirs.GetKeyData(directoryId).Value.Replace("\"", "");
                    }

                    try
                    {
                        if (directory.Contains('%'))
                        {
                            string nonescapedpart = directory.Split("%")[1];
                            string replacement = strings[nonescapedpart];
                            directory = directory.Replace("%" + nonescapedpart + "%", replacement).Replace("\"", "");
                        }
                    }
                    catch
                    {
                    }

                    if (directory == @"\")
                    {
                        fileList.Add(new TxtSetupFileEntry
                        { Path = Path.Combine(defdir, filename), DiscLocation = el.KeyName });
                    }
                    else
                    {
                        fileList.Add(new TxtSetupFileEntry
                        { Path = Path.Combine(defdir, directory, filename), DiscLocation = el.KeyName });
                    }
                }
                catch
                {
                }
            }

            _entries = fileList.ToArray();
        }

        private class TxtSetupFileEntry
        {
            public string Path { get; set; }
            public string DiscLocation { get; set; }
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