using DiscUtils;
using DiscUtils.Iso9660;
using DiscUtils.Udf;
using DiscUtils.Vfs;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using WindowsBuildIdentifier.Interfaces;

namespace WindowsBuildIdentifier.Identification
{
    public class MediaHandler
    {
        public class UnsupportedWIMException : Exception { }

        public class UnsupportedWIMXmlException : Exception { }

        private static string ExtractWIMXml(Stream wimstream)
        {
            try
            {
                using (ArchiveFile archiveFile = new ArchiveFile(wimstream, SevenZipFormat.Wim))
                {
                    if (archiveFile.Entries.Any(x => x.FileName == "[1].xml"))
                    {
                        Entry wimXmlEntry = archiveFile.Entries.First(x => x.FileName == "[1].xml");

                        string xml;
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            wimXmlEntry.Extract(memoryStream);

                            xml = Encoding.Unicode.GetString(memoryStream.ToArray(), 2, (int)memoryStream.Length - 2);
                        }

                        return xml;
                    }
                    else if (archiveFile.Entries.Any(x => x.FileName == "Windows"))
                    {
                        return archiveFile.GetArchiveComment();
                    }
                }

                throw new UnsupportedWIMException();
            }
            catch (SevenZipException)
            {
                throw new UnsupportedWIMException();
            }
        }

        private static XmlFormats.WIMXml.WIM GetWIMClassFromXml(string xml)
        {
            try
            {
                XmlSerializer ser = new XmlSerializer(typeof(XmlFormats.WIMXml.WIM));
                XmlFormats.WIMXml.WIM wim;
                using (StringReader stream = new StringReader(xml))
                {
                    using XmlReader reader = XmlReader.Create(stream);
                    wim = (XmlFormats.WIMXml.WIM)ser.Deserialize(reader);
                }
                return wim;
            }
            catch (InvalidOperationException)
            {
                throw new UnsupportedWIMXmlException();
            }
        }

        private static WindowsImageIndex[] IdentifyWindowsNTFromWIM(Stream wimstream, bool include_pe)
        {
            HashSet<WindowsImageIndex> results = new HashSet<WindowsImageIndex>();

            Console.WriteLine("Gathering WIM information XML file");

            string xml = ExtractWIMXml(wimstream);

            Console.WriteLine("Parsing WIM information XML file");
            XmlFormats.WIMXml.WIM wim = GetWIMClassFromXml(xml);

            Console.WriteLine($"Found {wim.IMAGE.Length} images in the wim according to the XML");

            Console.WriteLine("Evaluating relevant images in the WIM according to the XML");
            int irelevantcount2 = (wim.IMAGE.Any(x => x.DESCRIPTION.Contains("winpe", StringComparison.InvariantCultureIgnoreCase)) ? 1 : 0) +
                (wim.IMAGE.Any(x => x.DESCRIPTION.Contains("setup", StringComparison.InvariantCultureIgnoreCase)) ? 1 : 0) +
                (wim.IMAGE.Any(x => x.DESCRIPTION.Contains("preinstallation", StringComparison.InvariantCultureIgnoreCase)) ? 1 : 0) +
                (wim.IMAGE.Any(x => x.DESCRIPTION.Contains("winre", StringComparison.InvariantCultureIgnoreCase)) ? 1 : 0) +
                (wim.IMAGE.Any(x => x.DESCRIPTION.Contains("recovery", StringComparison.InvariantCultureIgnoreCase)) ? 1 : 0);

            Console.WriteLine($"Found {irelevantcount2} irrelevant images in the wim according to the XML");

            foreach (var image in wim.IMAGE)
            {
                Console.WriteLine();
                Console.WriteLine($"Processing index {image.INDEX}");

                //
                // If what we're trying to identify isn't just a winpe, and we are accessing a winpe image
                // skip the image
                //
                int irelevantcount = (image.DESCRIPTION.Contains("winpe", StringComparison.InvariantCultureIgnoreCase) ? 1 : 0) +
                    (image.DESCRIPTION.Contains("setup", StringComparison.InvariantCultureIgnoreCase) ? 1 : 0) +
                    (image.DESCRIPTION.Contains("preinstallation", StringComparison.InvariantCultureIgnoreCase) ? 1 : 0) +
                    (image.DESCRIPTION.Contains("winre", StringComparison.InvariantCultureIgnoreCase) ? 1 : 0) +
                    (image.DESCRIPTION.Contains("recovery", StringComparison.InvariantCultureIgnoreCase) ? 1 : 0);

                Console.WriteLine($"Index contains {irelevantcount} flags indicating this is a preinstallation environment");

                if (!include_pe && irelevantcount != 0 && irelevantcount2 < wim.IMAGE.Length)
                {
                    Console.WriteLine("Skipping this image");
                    continue;
                }

                string index = wim.IMAGE.Count() == 1 ? null : image.INDEX;

                bool WorkaroundForWIMFormatBug = false;

                if (index != null && wim.IMAGE[0].INDEX == "0")
                {
                    using ArchiveFile archiveFile = new ArchiveFile(wimstream, SevenZipFormat.Wim);
                    if (!archiveFile.Entries.Any(x => x.FileName.StartsWith("0\\")))
                    {
                        WorkaroundForWIMFormatBug = true;
                    }
                }

                if (WorkaroundForWIMFormatBug)
                {
                    int t = int.Parse(index);
                    index = (++t).ToString();
                }

                Console.WriteLine($"Index value: {index}");

                var provider = new WIMInstallProviderInterface(wimstream, index);

                var report = InstalledImage.DetectionHandler.IdentifyWindowsNT(provider);

                provider.Close();

                // fallback
                if ((string.IsNullOrEmpty(report.Sku) || report.Sku == "TerminalServer") && !string.IsNullOrEmpty(image.FLAGS))
                {
                    Console.WriteLine("WARNING: Falling back to WIM XML for edition gathering");

                    report.Sku = image.FLAGS;

                    report.Types = new HashSet<Type>();

                    if ((report.Sku.Contains("server", StringComparison.InvariantCultureIgnoreCase) && report.Sku.EndsWith("hyperv", StringComparison.InvariantCultureIgnoreCase)) ||
                    (report.Sku.Contains("server", StringComparison.InvariantCultureIgnoreCase) && report.Sku.EndsWith("v", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        if (!report.Types.Contains(Type.ServerV))
                        {
                            report.Types.Add(Type.ServerV);
                        }
                    }
                    else if (report.Sku.Contains("server", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!report.Types.Contains(Type.Server))
                        {
                            report.Types.Add(Type.Server);
                        }
                    }
                    else
                    {
                        if (!report.Types.Contains(Type.Client))
                        {
                            report.Types.Add(Type.Client);
                        }
                    }
                }

                Common.DisplayReport(report);

                WindowsImageIndex imageIndex = new WindowsImageIndex();

                imageIndex.Name = image.NAME;
                imageIndex.Description = image.DESCRIPTION;

                if (image.CREATIONTIME != null)
                {
                    var creationtime = Convert.ToInt32(image.CREATIONTIME.HIGHPART, 16) * 4294967296 + Convert.ToInt32(image.CREATIONTIME.LOWPART, 16);
                    var cTime = DateTime.FromFileTimeUtc(creationtime);
                    imageIndex.CreationTime = cTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                }

                if (image.LASTMODIFICATIONTIME != null)
                {
                    var creationtime = Convert.ToInt32(image.LASTMODIFICATIONTIME.HIGHPART, 16) * 4294967296 + Convert.ToInt32(image.LASTMODIFICATIONTIME.LOWPART, 16);
                    var cTime = DateTime.FromFileTimeUtc(creationtime);
                    imageIndex.LastModifiedTime = cTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                }

                imageIndex.WindowsImage = report;

                results.Add(imageIndex);
            }

            wimstream.Dispose();

            return results.ToArray();
        }

        private static void IdentifyWindowsNTFromVHD(Stream vhdstream)
        {
            VHDInstallProviderInterface provider = new VHDInstallProviderInterface(vhdstream);

            var report = InstalledImage.DetectionHandler.IdentifyWindowsNT(provider);
            Common.DisplayReport(report);
        }

        private static void IdentifyWindowsNTFromVDI(Stream vhdstream)
        {
            VDIInstallProviderInterface provider = new VDIInstallProviderInterface(vhdstream);

            var report = InstalledImage.DetectionHandler.IdentifyWindowsNT(provider);
            Common.DisplayReport(report);
        }

        private static void IdentifyWindowsNTFromVMDK(string vhdpath)
        {
            VMDKInstallProviderInterface provider = new VMDKInstallProviderInterface(vhdpath);

            var report = InstalledImage.DetectionHandler.IdentifyWindowsNT(provider);
            Common.DisplayReport(report);
        }

        private static void IdentifyWindowsNTFromVHDX(Stream vhdstream)
        {
            VHDXInstallProviderInterface provider = new VHDXInstallProviderInterface(vhdstream);

            var report = InstalledImage.DetectionHandler.IdentifyWindowsNT(provider);
            Common.DisplayReport(report);
        }

        private static FileItem[] HandleFacade(IFileSystem facade, bool Recursivity = false, bool index = false)
        {
            HashSet<FileItem> result = new HashSet<FileItem>();

            try
            {
                if (index)
                {
                    // add the root to the array
                    var root = facade.Root;

                    if (root != null)
                    {
                        FileItem fileItem3 = new FileItem();
                        fileItem3.Location = @"\";

                        Console.WriteLine($"Folder: {fileItem3.Location}");

                        var tmpattribs3 = facade.GetAttributes(fileItem3.Location).ToString();
                        fileItem3.Attributes = tmpattribs3.Split(", ");

                        fileItem3.LastAccessTime = root.LastAccessTimeUtc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        fileItem3.LastWriteTime = root.LastWriteTimeUtc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        fileItem3.CreationTime = root.CreationTimeUtc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                        result.Add(fileItem3);
                    }
                    // end of adding root

                    foreach (var item in facade.GetDirectories("", null, SearchOption.AllDirectories))
                    {
                        FileItem fileItem = new FileItem();
                        fileItem.Location = item;

                        Console.WriteLine($"Folder: {fileItem.Location}");

                        var tmpattribs = facade.GetAttributes(fileItem.Location).ToString();
                        fileItem.Attributes = tmpattribs.Split(", ");

                        fileItem.LastAccessTime = facade.GetLastAccessTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        fileItem.LastWriteTime = facade.GetLastWriteTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        fileItem.CreationTime = facade.GetCreationTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                        result.Add(fileItem);
                    }
                }

                foreach (var item in facade.GetFiles("", null, SearchOption.AllDirectories))
                {
                    FileItem fileItem = new FileItem();
                    fileItem.Location = item;

                    if (index)
                    {
                        Console.WriteLine($"File: {fileItem.Location}");

                        var tmpattribs = facade.GetAttributes(fileItem.Location).ToString();
                        fileItem.Attributes = tmpattribs.Split(", ");

                        fileItem.LastAccessTime = facade.GetLastAccessTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        fileItem.LastWriteTime = facade.GetLastWriteTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        fileItem.CreationTime = facade.GetCreationTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                        fileItem.Size = facade.GetFileLength(fileItem.Location).ToString();

                        fileItem.Hash = new Hash();

                        Console.WriteLine("Computing MD5");
                        var md5hash = new MD5CryptoServiceProvider().ComputeHash(facade.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read));
                        fileItem.Hash.MD5 = BitConverter.ToString(md5hash).Replace("-", "");

                        Console.WriteLine("Computing SHA1");
                        var sha1hash = new SHA1CryptoServiceProvider().ComputeHash(facade.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read));
                        fileItem.Hash.SHA1 = BitConverter.ToString(sha1hash).Replace("-", "");

                        Console.WriteLine("Computing CRC32");
                        var crc32hash = new Crc32().ComputeHash(facade.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read));
                        fileItem.Hash.CRC32 = BitConverter.ToString(crc32hash).Replace("-", "");
                    }

                    var extension = fileItem.Location.Split(".")[^1];

                    switch (extension.ToLower())
                    {
                        case "wim":
                        case "esd":
                            {
                                try
                                {
                                    var tempIndexes = IdentifyWindowsFromWIM(facade.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read), index);
                                    fileItem.Metadata = new MetaData();
                                    fileItem.Metadata.WindowsImageIndexes = tempIndexes;
                                }
                                catch { };
                                break;
                            }
                        case "mui":
                        case "dll":
                        case "sys":
                        case "exe":
                        case "efi":
                            {
                                if (index)
                                {
                                    try
                                    {
                                        using var itemstream = facade.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read);
                                        using var arch = new ArchiveFile(itemstream, SevenZipFormat.PE);

                                        if (arch.Entries.Any(x => x.FileName.EndsWith("version.txt")))
                                        {
                                            var ver = arch.Entries.First(x => x.FileName.EndsWith("version.txt"));

                                            using var memread = new MemoryStream();
                                            ver.Extract(memread);

                                            memread.Seek(0, SeekOrigin.Begin);

                                            using TextReader tr = new StreamReader(memread, Encoding.Unicode);

                                            fileItem.Version = new Version();

                                            var ln = tr.ReadLine();
                                            while (ln != null)
                                            {
                                                var line = ln.Replace("\0", "");
                                                if (line.Contains("VALUE \"CompanyName\","))
                                                {
                                                    fileItem.Version.CompanyName = line.Split("\"")[^2];
                                                }
                                                else if (line.Contains("VALUE \"FileDescription\","))
                                                {
                                                    fileItem.Version.FileDescription = line.Split("\"")[^2];
                                                }
                                                else if (line.Contains("VALUE \"FileVersion\","))
                                                {
                                                    fileItem.Version.FileVersion = line.Split("\"")[^2];
                                                }
                                                else if (line.Contains("VALUE \"InternalName\","))
                                                {
                                                    fileItem.Version.InternalName = line.Split("\"")[^2];
                                                }
                                                else if (line.Contains("VALUE \"LegalCopyright\","))
                                                {
                                                    fileItem.Version.LegalCopyright = line.Split("\"")[^2];
                                                }
                                                else if (line.Contains("VALUE \"OriginalFilename\","))
                                                {
                                                    fileItem.Version.OriginalFilename = line.Split("\"")[^2];
                                                }
                                                else if (line.Contains("VALUE \"ProductName\","))
                                                {
                                                    fileItem.Version.ProductName = line.Split("\"")[^2];
                                                }
                                                else if (line.Contains("VALUE \"ProductVersion\","))
                                                {
                                                    fileItem.Version.ProductVersion = line.Split("\"")[^2];
                                                }

                                                ln = tr.ReadLine();
                                            }
                                        }
                                    }
                                    catch { };
                                }
                                break;
                            }
                    }

                    if (index)
                        result.Add(fileItem);
                    else if (fileItem.Metadata != null && fileItem.Metadata.WindowsImageIndexes != null)
                        result.Add(fileItem);

                    if (index && Recursivity)
                    {
                        switch (extension.ToLower())
                        {
                            case "wim":
                            case "esd":
                                {
                                    try
                                    {
                                        using var wimstrm = facade.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read);
                                        using var arch = new ArchiveFile(wimstrm, SevenZipFormat.Wim);
                                        var wimparsed = new ArchiveBridge(arch);

                                        var res = HandleFacade(wimparsed);

                                        var res2 = res.Select(x =>
                                        {
                                            x.Location = fileItem.Location + @"\" + x.Location;
                                            return x;
                                        });

                                        result = result.Concat(res2).ToHashSet();
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex.ToString());
                                    }
                                    break;
                                }

                            case "mui":
                            case "dll":
                            case "sys":
                            case "exe":
                            case "efi":
                                {
                                    try
                                    {
                                        using var pestrm = facade.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read);
                                        using var arch = new ArchiveFile(pestrm, SevenZipFormat.PE);
                                        var peparsed = new ArchiveBridge(arch);

                                        var res = HandleFacade(peparsed);

                                        var res2 = res.Select(x =>
                                        {
                                            x.Location = fileItem.Location + @"\" + x.Location;
                                            return x;
                                        });

                                        result = result.Concat(res2).ToHashSet();
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex.ToString());
                                    }
                                    break;
                                }
                            case "iso":
                            case "mdf":
                                {
                                    try
                                    {
                                        using var isoStream = facade.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read);

                                        VfsFileSystemFacade cd = new CDReader(isoStream, true);
                                        if (cd.FileExists(@"README.TXT"))
                                        {
                                            cd = new UdfReader(isoStream);
                                        }

                                        var res = HandleFacade(cd);

                                        var res2 = res.Select(x =>
                                        {
                                            x.Location = fileItem.Location + @"\" + x.Location;
                                            return x;
                                        });

                                        result = result.Concat(res2).ToHashSet();
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("Fail");
                                        Console.WriteLine(ex.ToString());
                                    }

                                    break;
                                }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }

            return result.OrderBy(x => x.Location).ToArray();
        }

        public static FileItem[] IdentifyWindowsFromISO(string isopath, bool deep, bool index)
        {
            FileItem[] result = new FileItem[0];

            Console.WriteLine();
            Console.WriteLine("Opening ISO File");
            Console.WriteLine(isopath);

            try
            {
                using FileStream isoStream = File.Open(isopath, FileMode.Open, FileAccess.Read);

                VfsFileSystemFacade cd = new CDReader(isoStream, true);
                if (cd.FileExists(@"README.TXT"))
                {
                    cd = new UdfReader(isoStream);
                }

                result = HandleFacade(cd, deep, index);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }

            return result;
        }

        public static FileItem[] IdentifyWindowsFromMDF(string isopath, bool deep, bool index)
        {
            FileItem[] result = new FileItem[0];

            Console.WriteLine();
            Console.WriteLine("Opening MDF File");
            Console.WriteLine(isopath);

            try
            {
                using FileStream isoStream = File.Open(isopath, FileMode.Open, FileAccess.Read);

                VfsFileSystemFacade cd = new CDReader(isoStream, true);
                if (cd.FileExists(@"README.TXT"))
                {
                    cd = new UdfReader(isoStream);
                }

                result = HandleFacade(cd, deep, index);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }

            return result;
        }

        public static WindowsImageIndex[] IdentifyWindowsFromWIM(Stream wim, bool include_pe)
        {
            WindowsImageIndex[] result = new WindowsImageIndex[0];

            try
            {
                //
                // If this succeeds we are processing a properly supported final (or near final)
                // WIM file format, so we use the adequate function to handle it.
                //
                result = IdentifyWindowsNTFromWIM(wim, include_pe);
            }
            catch (UnsupportedWIMException)
            {
                //
                // If this fails we are processing an early
                // WIM file format, so we use the adequate function to handle it.
                //
                Console.WriteLine("Early WIM Format TODO");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }

            return result;
        }

        public static void IdentifyWindowsFromVHD(string vhdpath)
        {
            Console.WriteLine();
            Console.WriteLine("Opening VHD File");
            Console.WriteLine(vhdpath);
            try
            {
                using FileStream vhdStream = File.Open(vhdpath, FileMode.Open, FileAccess.Read);
                IdentifyWindowsNTFromVHD(vhdStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }
        }

        public static void IdentifyWindowsFromVMDK(string vhdpath)
        {
            Console.WriteLine();
            Console.WriteLine("Opening VMDK File");
            Console.WriteLine(vhdpath);
            try
            {
                IdentifyWindowsNTFromVMDK(vhdpath);
            }
            catch (NullReferenceException)
            {
                Console.WriteLine("Fail, this image is most likely a differential image. Skipping.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }
        }

        public static void IdentifyWindowsFromVHDX(string vhdpath)
        {
            Console.WriteLine();
            Console.WriteLine("Opening VHDX File");
            Console.WriteLine(vhdpath);
            try
            {
                using FileStream vhdStream = File.Open(vhdpath, FileMode.Open, FileAccess.Read);
                IdentifyWindowsNTFromVHDX(vhdStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }
        }

        public static void IdentifyWindowsFromVDI(string vhdpath)
        {
            Console.WriteLine();
            Console.WriteLine("Opening VDI File");
            Console.WriteLine(vhdpath);
            try
            {
                using FileStream vhdStream = File.Open(vhdpath, FileMode.Open, FileAccess.Read);
                IdentifyWindowsNTFromVDI(vhdStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
