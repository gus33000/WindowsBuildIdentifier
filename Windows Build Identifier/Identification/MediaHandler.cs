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

        private static WindowsImageIndex[] IdentifyWindowsNTFromWIM(Stream wimstream)
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

                /*if (irelevantcount != 0 && irelevantcount2 < wim.IMAGE.Length)
                {
                    Console.WriteLine("Skipping this image");
                    continue;
                }*/

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
                if (string.IsNullOrEmpty(report.Sku) || report.Sku == "TerminalServer")
                {
                    Console.WriteLine("WARNING: Falling back to WIM XML for edition gathering");

                    report.Sku = image.FLAGS;

                    report.Type = new HashSet<Type>();

                    if ((report.Sku.Contains("server", StringComparison.InvariantCultureIgnoreCase) && report.Sku.EndsWith("hyperv", StringComparison.InvariantCultureIgnoreCase)) ||
                    (report.Sku.Contains("server", StringComparison.InvariantCultureIgnoreCase) && report.Sku.EndsWith("v", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        if (!report.Type.Contains(Type.ServerV))
                        {
                            report.Type.Add(Type.ServerV);
                        }
                    }
                    else if (report.Sku.Contains("server", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!report.Type.Contains(Type.Server))
                        {
                            report.Type.Add(Type.Server);
                        }
                    }
                    else
                    {
                        if (!report.Type.Contains(Type.Client))
                        {
                            report.Type.Add(Type.Client);
                        }
                    }
                }

                Common.DisplayReport(report);

                WindowsImageIndex imageIndex = new WindowsImageIndex();

                imageIndex.Name = image.NAME;
                imageIndex.Description = image.DESCRIPTION;

                var creationtime = Convert.ToInt32(image.CREATIONTIME.HIGHPART, 16) * 4294967296 + Convert.ToInt32(image.CREATIONTIME.LOWPART, 16);
                var lastmodifiedtime = Convert.ToInt32(image.LASTMODIFICATIONTIME.HIGHPART, 16) * 4294967296 + Convert.ToInt32(image.LASTMODIFICATIONTIME.LOWPART, 16);

                var cTime = DateTime.FromFileTimeUtc(creationtime);
                var lTime = DateTime.FromFileTimeUtc(lastmodifiedtime);

                imageIndex.CreationTime = cTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                imageIndex.LastModifiedTime = lTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

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

        public static FileItem[] IdentifyWindowsFromISO(string isopath)
        {
            HashSet<FileItem> result = new HashSet<FileItem>();

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

                foreach (var item in cd.GetDirectories("", null, SearchOption.AllDirectories))
                {
                    FileItem fileItem = new FileItem();
                    fileItem.Location = item;

                    Console.WriteLine($"Folder: {fileItem.Location}");

                    fileItem.Attributes = cd.GetAttributes(fileItem.Location).ToString();

                    fileItem.LastAccessTime = cd.GetLastAccessTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                    fileItem.LastWriteTime = cd.GetLastWriteTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                    fileItem.CreationTime = cd.GetCreationTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                    result.Add(fileItem);
                }
                foreach (var item in cd.GetFiles("", null, SearchOption.AllDirectories))
                {
                    FileItem fileItem = new FileItem();
                    fileItem.Location = item;

                    Console.WriteLine($"File: {fileItem.Location}");

                    fileItem.Attributes = cd.GetAttributes(fileItem.Location).ToString();

                    fileItem.LastAccessTime = cd.GetLastAccessTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                    fileItem.LastWriteTime = cd.GetLastWriteTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                    fileItem.CreationTime = cd.GetCreationTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                    Console.WriteLine("Computing MD5");
                    var md5hash = new MD5CryptoServiceProvider().ComputeHash(cd.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read));
                    fileItem.MD5 = BitConverter.ToString(md5hash).Replace("-", "");

                    var extension = fileItem.Location.Split(".")[^1];

                    switch (extension.ToLower())
                    {
                        case "wim":
                        case "esd":
                            {
                                try
                                {
                                    var tempIndexes = IdentifyWindowsFromWIM(cd.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read));
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
                                try
                                {
                                    using var itemstream = cd.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read);
                                    using var arch = new ArchiveFile(itemstream, SevenZipFormat.PE);

                                    if (arch.Entries.Any(x => x.FileName.EndsWith("version.txt")))
                                    {
                                        var ver = arch.Entries.First(x => x.FileName.EndsWith("version.txt"));
                                        var tmpf = Path.GetTempFileName();
                                        ver.Extract(tmpf);

                                        var vers = File.ReadAllText(tmpf);

                                        fileItem.Metadata = new MetaData();
                                        fileItem.Metadata.VersionInfo = vers;

                                        File.Delete(tmpf);
                                    }
                                }
                                catch { };
                                break;
                            }
                    }

                    result.Add(fileItem);

                    switch (extension.ToLower())
                    {
                        case "wim":
                        case "esd":
                            {
                                try
                                {
                                    using var wimstrm = cd.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read);
                                    var wimparsed = new DiscUtils.Wim.WimFile(wimstrm);

                                    for (int i = 0; i < wimparsed.ImageCount; i++)
                                    {
                                        try
                                        {
                                            var image = wimparsed.GetImage(i);

                                            foreach (var itm in image.GetDirectories("", null, SearchOption.AllDirectories))
                                            {
                                                try
                                                {
                                                    FileItem fileItem2 = new FileItem();
                                                    fileItem2.Location = itm;

                                                    Console.WriteLine(@$"Folder: {fileItem.Location}\{i}\{fileItem2.Location}");

                                                    fileItem2.Attributes = image.GetAttributes(fileItem2.Location).ToString();

                                                    fileItem2.LastAccessTime = image.GetLastAccessTimeUtc(fileItem2.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                                                    fileItem2.LastWriteTime = image.GetLastWriteTimeUtc(fileItem2.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                                                    fileItem2.CreationTime = image.GetCreationTimeUtc(fileItem2.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                                                    fileItem2.Location = @$"{fileItem.Location}\{i}\{fileItem2.Location}";

                                                    result.Add(fileItem2);
                                                }
                                                catch { };
                                            }

                                            foreach (var itm in image.GetFiles("", null, SearchOption.AllDirectories))
                                            {
                                                try
                                                {
                                                    FileItem fileItem2 = new FileItem();
                                                    fileItem2.Location = itm;

                                                    Console.WriteLine(@$"File: {fileItem.Location}\{i}\{fileItem2.Location}");

                                                    fileItem2.Attributes = image.GetAttributes(fileItem2.Location).ToString();

                                                    fileItem2.LastAccessTime = image.GetLastAccessTimeUtc(fileItem2.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                                                    fileItem2.LastWriteTime = image.GetLastWriteTimeUtc(fileItem2.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                                                    fileItem2.CreationTime = image.GetCreationTimeUtc(fileItem2.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                                                    Console.WriteLine("Computing MD5");
                                                    var md5hash2 = new MD5CryptoServiceProvider().ComputeHash(image.OpenFile(fileItem2.Location, FileMode.Open, FileAccess.Read));
                                                    fileItem2.MD5 = BitConverter.ToString(md5hash2).Replace("-", "");

                                                    var extension2 = fileItem2.Location.Split(".")[^1];

                                                    switch (extension2.ToLower())
                                                    {
                                                        case "mui":
                                                        case "dll":
                                                        case "sys":
                                                        case "exe":
                                                        case "efi":
                                                            {
                                                                try
                                                                {
                                                                    using var itemstream = image.OpenFile(fileItem2.Location, FileMode.Open, FileAccess.Read);
                                                                    using var arch = new ArchiveFile(itemstream, SevenZipFormat.PE);

                                                                    if (arch.Entries.Any(x => x.FileName.EndsWith("version.txt")))
                                                                    {
                                                                        var ver = arch.Entries.First(x => x.FileName.EndsWith("version.txt"));
                                                                        var tmpf = Path.GetTempFileName();
                                                                        ver.Extract(tmpf, false);

                                                                        var vers = File.ReadAllText(tmpf);

                                                                        fileItem2.Metadata = new MetaData();
                                                                        fileItem2.Metadata.VersionInfo = vers;

                                                                        File.Delete(tmpf);
                                                                    }
                                                                }
                                                                catch { };
                                                                break;
                                                            }
                                                    }

                                                    fileItem2.Location = @$"{fileItem.Location}\{i}\{fileItem2.Location}";

                                                    result.Add(fileItem2);
                                                }
                                                catch (Exception ex)
                                                {
                                                    Console.WriteLine(@$"File: {fileItem.Location}\{i}\{itm}");
                                                    Console.WriteLine(ex.ToString());
                                                };
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine(ex.ToString());
                                        };
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.ToString());
                                };
                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }

            return result.ToArray();
        }

        public static FileItem[] IdentifyWindowsFromMDF(string isopath)
        {
            HashSet<FileItem> result = new HashSet<FileItem>();

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

                foreach (var item in cd.GetDirectories("", "*.*", SearchOption.AllDirectories))
                {
                    FileItem fileItem = new FileItem();
                    fileItem.Location = item;

                    Console.WriteLine($"Folder: {fileItem.Location}");

                    fileItem.Attributes = cd.GetAttributes(fileItem.Location).ToString();

                    fileItem.LastAccessTime = cd.GetLastAccessTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                    fileItem.LastWriteTime = cd.GetLastWriteTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                    fileItem.CreationTime = cd.GetCreationTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                    result.Add(fileItem);
                }
                foreach (var item in cd.GetFiles("", "*.*", SearchOption.AllDirectories))
                {
                    FileItem fileItem = new FileItem();
                    fileItem.Location = item;

                    Console.WriteLine($"File: {fileItem.Location}");

                    fileItem.Attributes = cd.GetAttributes(fileItem.Location).ToString();

                    fileItem.LastAccessTime = cd.GetLastAccessTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                    fileItem.LastWriteTime = cd.GetLastWriteTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                    fileItem.CreationTime = cd.GetCreationTimeUtc(fileItem.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                    Console.WriteLine("Computing MD5");
                    var md5hash = new MD5CryptoServiceProvider().ComputeHash(cd.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read));
                    fileItem.MD5 = BitConverter.ToString(md5hash).Replace("-", "");

                    var extension = fileItem.Location.Split(".")[^1];

                    switch (extension.ToLower())
                    {
                        case "wim":
                        case "esd":
                            {
                                try
                                {
                                    var tempIndexes = IdentifyWindowsFromWIM(cd.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read));
                                    fileItem.Metadata = new MetaData();
                                    fileItem.Metadata.WindowsImageIndexes = tempIndexes;
                                }
                                catch { };
                                break;
                            }
                    }

                    result.Add(fileItem);

                    switch (extension.ToLower())
                    {
                        case "wim":
                        case "esd":
                            {
                                try
                                {
                                    using var wimstrm = cd.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read);
                                    var wimparsed = new DiscUtils.Wim.WimFile(wimstrm);

                                    for (int i = 0; i < wimparsed.ImageCount; i++)
                                    {
                                        var image = wimparsed.GetImage(i);

                                        foreach (var itm in image.GetDirectories("", "*.*", SearchOption.AllDirectories))
                                        {
                                            FileItem fileItem2 = new FileItem();
                                            fileItem2.Location = itm;

                                            Console.WriteLine($"File: {fileItem2.Location}");

                                            fileItem2.Attributes = image.GetAttributes(fileItem2.Location).ToString();

                                            fileItem2.LastAccessTime = image.GetLastAccessTimeUtc(fileItem2.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                                            fileItem2.LastWriteTime = image.GetLastWriteTimeUtc(fileItem2.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                                            fileItem2.CreationTime = image.GetCreationTimeUtc(fileItem2.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                                            fileItem2.Location = @$"{fileItem.Location}\{i}\{itm}";

                                            result.Add(fileItem2);
                                        }

                                        foreach (var itm in image.GetFiles("", "*", SearchOption.AllDirectories))
                                        {
                                            FileItem fileItem2 = new FileItem();
                                            fileItem2.Location = itm;

                                            Console.WriteLine($"File: {fileItem2.Location}");

                                            fileItem2.Attributes = image.GetAttributes(fileItem2.Location).ToString();

                                            fileItem2.LastAccessTime = image.GetLastAccessTimeUtc(fileItem2.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                                            fileItem2.LastWriteTime = image.GetLastWriteTimeUtc(fileItem2.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                                            fileItem2.CreationTime = image.GetCreationTimeUtc(fileItem2.Location).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                                            Console.WriteLine("Computing MD5");
                                            var md5hash2 = new MD5CryptoServiceProvider().ComputeHash(image.OpenFile(fileItem2.Location, FileMode.Open, FileAccess.Read));
                                            fileItem2.MD5 = BitConverter.ToString(md5hash2).Replace("-", "");

                                            fileItem2.Location = @$"{fileItem.Location}\{i}\{itm}";

                                            result.Add(fileItem2);
                                        }
                                    }
                                }
                                catch { };
                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }

            return result.ToArray();
        }

        public static WindowsImageIndex[] IdentifyWindowsFromWIM(Stream wim)
        {
            WindowsImageIndex[] result = new WindowsImageIndex[0];

            try
            {
                //
                // If this succeeds we are processing a properly supported final (or near final)
                // WIM file format, so we use the adequate function to handle it.
                //
                result = IdentifyWindowsNTFromWIM(wim);
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
