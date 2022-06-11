using DiscUtils;
using DiscUtils.Iso9660;
using DiscUtils.Streams;
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
using WindowsBuildIdentifier.Identification.InstalledImage;
using WindowsBuildIdentifier.Interfaces;
using WindowsBuildIdentifier.XmlFormats;

namespace WindowsBuildIdentifier.Identification
{
    public class MediaHandler
    {
        private static string ExtractWimXml(ArchiveFile archiveFile)
        {
            try
            {
                if (archiveFile.Entries.Any(x => x.FileName == "[1].xml"))
                {
                    Entry wimXmlEntry = archiveFile.Entries.First(x => x.FileName == "[1].xml");

                    string xml;
                    using MemoryStream memoryStream = new();
                    wimXmlEntry.Extract(memoryStream);

                    xml = Encoding.Unicode.GetString(memoryStream.ToArray(), 2, (int)memoryStream.Length - 2);

                    return xml;
                }

                if (archiveFile.Entries.Any(x => x.FileName == "Windows"))
                {
                    return archiveFile.GetArchiveComment();
                }

                throw new UnsupportedWimException();
            }
            catch (SevenZipException)
            {
                throw new UnsupportedWimException();
            }
        }

        private static WIMXml.WIM GetWimClassFromXml(string xml)
        {
            try
            {
                XmlSerializer ser = new(typeof(WIMXml.WIM));
                WIMXml.WIM wim;
                using StringReader stream = new(xml);
                using XmlReader reader = XmlReader.Create(stream);
                wim = (WIMXml.WIM)ser.Deserialize(reader);

                return wim;
            }
            catch (InvalidOperationException)
            {
                throw new UnsupportedWimXmlException();
            }
        }

        private static WindowsImageIndex[] IdentifyWindowsNTFromWim(Stream wimstream, bool includePe)
        {
            HashSet<WindowsImageIndex> results = new();

            Console.WriteLine("Gathering WIM information XML file");


            using ArchiveFile archiveFile = new(wimstream, SevenZipFormat.Wim);
            string xml = ExtractWimXml(archiveFile);

            Console.WriteLine("Parsing WIM information XML file");
            WIMXml.WIM wim = GetWimClassFromXml(xml);

            Console.WriteLine($"Found {wim.IMAGE.Length} images in the wim according to the XML");

            Console.WriteLine("Evaluating relevant images in the WIM according to the XML");
            int irelevantcount2 = (wim.IMAGE.Any(x => x.DESCRIPTION != null && x.DESCRIPTION.Contains("winpe", StringComparison.OrdinalIgnoreCase)) ? 1 : 0) +
                                  (wim.IMAGE.Any(x => x.DESCRIPTION != null && x.DESCRIPTION.Contains("setup", StringComparison.OrdinalIgnoreCase)) ? 1 : 0) +
                                  (wim.IMAGE.Any(x => x.DESCRIPTION != null && x.DESCRIPTION.Contains("preinstallation", StringComparison.OrdinalIgnoreCase)) ? 1 : 0) +
                                  (wim.IMAGE.Any(x => x.DESCRIPTION != null && x.DESCRIPTION.Contains("winre", StringComparison.OrdinalIgnoreCase)) ? 1 : 0) +
                                  (wim.IMAGE.Any(x => x.DESCRIPTION != null && x.DESCRIPTION.Contains("recovery", StringComparison.OrdinalIgnoreCase)) ? 1 : 0);

            Console.WriteLine($"Found {irelevantcount2} irrelevant images in the wim according to the XML");

            WimInstallProviderInterface provider = new(archiveFile);

            foreach (WIMXml.IMAGE image in wim.IMAGE)
            {
                Console.WriteLine();
                Console.WriteLine($"Processing index {image.INDEX}");

                //
                // If what we're trying to identify isn't just a winpe, and we are accessing a winpe image
                // skip the image
                //
                int irelevantcount = (image.DESCRIPTION != null && image.DESCRIPTION.Contains("winpe", StringComparison.OrdinalIgnoreCase) ? 1 : 0) +
                                     (image.DESCRIPTION != null && image.DESCRIPTION.Contains("setup", StringComparison.OrdinalIgnoreCase) ? 1 : 0) +
                                     (image.DESCRIPTION != null && image.DESCRIPTION.Contains("preinstallation", StringComparison.OrdinalIgnoreCase) ? 1 : 0) +
                                     (image.DESCRIPTION != null && image.DESCRIPTION.Contains("winre", StringComparison.OrdinalIgnoreCase) ? 1 : 0) +
                                     (image.DESCRIPTION != null && image.DESCRIPTION.Contains("recovery", StringComparison.OrdinalIgnoreCase) ? 1 : 0);

                Console.WriteLine(
                    $"Index contains {irelevantcount} flags indicating this is a preinstallation environment");

                if (!includePe && irelevantcount != 0 && irelevantcount2 < wim.IMAGE.Length)
                {
                    Console.WriteLine("Skipping this image");
                    continue;
                }

                string index = wim.IMAGE.Length == 1 ? null : image.INDEX;

                bool workaroundForWimFormatBug = false;

                if (index != null && wim.IMAGE[0].INDEX == "0")
                {
                    if (!archiveFile.Entries.Any(x => x.FileName.StartsWith("0\\")))
                    {
                        workaroundForWimFormatBug = true;
                    }
                }

                if (workaroundForWimFormatBug)
                {
                    int t = int.Parse(index);
                    index = (++t).ToString();
                }

                Console.WriteLine($"Index value: {index}");

                provider.SetIndex(index);

                WindowsImage report = DetectionHandler.IdentifyWindowsNT(provider);
                if (report == null)
                {
                    continue;
                }

                // fallback
                if ((string.IsNullOrEmpty(report.Sku) || report.Sku == "TerminalServer") &&
                    !string.IsNullOrEmpty(image.FLAGS))
                {
                    Console.WriteLine("WARNING: Falling back to WIM XML for edition gathering");

                    report.Sku = image.FLAGS;

                    report.Types = new HashSet<Type>();

                    if (report.Sku.Contains("server", StringComparison.OrdinalIgnoreCase) &&
                        report.Sku.EndsWith("hyperv", StringComparison.OrdinalIgnoreCase) ||
                        report.Sku.Contains("server", StringComparison.OrdinalIgnoreCase) &&
                        report.Sku.EndsWith("v", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!report.Types.Contains(Type.ServerV))
                        {
                            report.Types.Add(Type.ServerV);
                        }
                    }
                    else if (report.Sku.Contains("server", StringComparison.OrdinalIgnoreCase))
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

                WindowsImageIndex imageIndex = new()
                {
                    Name = image.NAME,
                    Description = image.DESCRIPTION
                };

                if (image.CREATIONTIME != null)
                {
                    long creationtime = Convert.ToInt32(image.CREATIONTIME.HIGHPART, 16) * 4294967296 +
                                        Convert.ToInt32(image.CREATIONTIME.LOWPART, 16);
                    DateTime cTime = DateTime.FromFileTimeUtc(creationtime);
                    imageIndex.CreationTime = cTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                }

                if (image.LASTMODIFICATIONTIME != null)
                {
                    long creationtime = Convert.ToInt32(image.LASTMODIFICATIONTIME.HIGHPART, 16) * 4294967296 +
                                        Convert.ToInt32(image.LASTMODIFICATIONTIME.LOWPART, 16);
                    DateTime cTime = DateTime.FromFileTimeUtc(creationtime);
                    imageIndex.LastModifiedTime = cTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                }

                imageIndex.WindowsImage = report;

                results.Add(imageIndex);
            }

            provider.Close();

            return results.ToArray();
        }

        private static void IdentifyWindowsNTFromVhd(Stream vhdstream)
        {
            VhdInstallProviderInterface provider = new(vhdstream);

            WindowsImage report = DetectionHandler.IdentifyWindowsNT(provider);
            Common.DisplayReport(report);
        }

        private static WindowsImageIndex[] IdentifyWindowsNTFromRootFs(TxtSetupBridge fileSystem)
        {
            RootFsInstallProviderInterface provider = new(fileSystem);

            WindowsImage report = DetectionHandler.IdentifyWindowsNT(provider);
            report.Sku = fileSystem.GetSkuFromTxtSetupMedia(report.BuildNumber);
            report.Licensing = fileSystem.GetLicensingFromTxtSetupMedia();
            report = DetectionHandler.FixSkuNames(report, false);

            Common.DisplayReport(report);

            WindowsImageIndex index = new() { WindowsImage = report };

            return new[] { index };
        }

        private static void IdentifyWindowsNTFromVdi(Stream vhdstream)
        {
            VdiInstallProviderInterface provider = new(vhdstream);

            WindowsImage report = DetectionHandler.IdentifyWindowsNT(provider);
            Common.DisplayReport(report);
        }

        private static void IdentifyWindowsNTFromVmdk(string vhdpath)
        {
            VmdkInstallProviderInterface provider = new(vhdpath);

            WindowsImage report = DetectionHandler.IdentifyWindowsNT(provider);
            Common.DisplayReport(report);
        }

        private static void IdentifyWindowsNTFromVhdx(Stream vhdstream)
        {
            VhdxInstallProviderInterface provider = new(vhdstream);

            WindowsImage report = DetectionHandler.IdentifyWindowsNT(provider);
            Common.DisplayReport(report);
        }

        private static FileItem[] HandleFacade(IFileSystem facade, bool recursivity = false, bool index = false)
        {
            HashSet<FileItem> result = new();

            try
            {
                if (index)
                {
                    // add the root to the array
                    DiscDirectoryInfo root = facade.Root;

                    if (root != null)
                    {
                        FileItem fileItem3 = new()
                        {
                            Location = @"\"
                        };

                        Console.WriteLine($"Folder: {fileItem3.Location}");

                        string tmpattribs3 = facade.GetAttributes(fileItem3.Location).ToString();
                        fileItem3.Attributes = tmpattribs3.Split(", ");

                        fileItem3.LastAccessTime =
                            root.LastAccessTimeUtc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        fileItem3.LastWriteTime =
                            root.LastWriteTimeUtc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        fileItem3.CreationTime =
                            root.CreationTimeUtc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                        result.Add(fileItem3);
                    }
                    // end of adding root

                    foreach (string item in facade.GetDirectories("", null, SearchOption.AllDirectories))
                    {
                        FileItem fileItem = new()
                        {
                            Location = item
                        };

                        Console.WriteLine($"Folder: {fileItem.Location}");

                        string tmpattribs = facade.GetAttributes(fileItem.Location).ToString();
                        fileItem.Attributes = tmpattribs.Split(", ");

                        fileItem.LastAccessTime = facade.GetLastAccessTimeUtc(fileItem.Location)
                            .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        fileItem.LastWriteTime = facade.GetLastWriteTimeUtc(fileItem.Location)
                            .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        fileItem.CreationTime = facade.GetCreationTimeUtc(fileItem.Location)
                            .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                        result.Add(fileItem);
                    }
                }

                foreach (string item in facade.GetFiles("", null, SearchOption.AllDirectories))
                {
                    FileItem fileItem = new()
                    {
                        Location = item
                    };

                    if (index)
                    {
                        Console.WriteLine($"File: {fileItem.Location}");

                        string tmpattribs = facade.GetAttributes(fileItem.Location).ToString();
                        fileItem.Attributes = tmpattribs.Split(", ");

                        fileItem.LastAccessTime = facade.GetLastAccessTimeUtc(fileItem.Location)
                            .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        fileItem.LastWriteTime = facade.GetLastWriteTimeUtc(fileItem.Location)
                            .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                        fileItem.CreationTime = facade.GetCreationTimeUtc(fileItem.Location)
                            .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

                        fileItem.Size = facade.GetFileLength(fileItem.Location).ToString();

                        fileItem.Hash = new Hash();

                        using SparseStream file = facade.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read);
                        using MD5 md5Prov = MD5.Create();
                        using SHA1 sha1Prov = SHA1.Create();
                        using Crc32 crcProv = new();

                        Console.WriteLine("Computing hashes");

                        byte[] buffer = new byte[16384];
                        int bytesRead = 0;

                        while ((bytesRead = file.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            md5Prov.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                            sha1Prov.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                            crcProv.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                        }

                        md5Prov.TransformFinalBlock(buffer, 0, 0);
                        sha1Prov.TransformFinalBlock(buffer, 0, 0);
                        crcProv.TransformFinalBlock(buffer, 0, 0);

                        fileItem.Hash.Md5 = BitConverter.ToString(md5Prov.Hash).Replace("-", "");
                        fileItem.Hash.Sha1 = BitConverter.ToString(sha1Prov.Hash).Replace("-", "");
                        fileItem.Hash.Crc32 = BitConverter.ToString(crcProv.Hash).Replace("-", "");
                    }

                    string extension = fileItem.Location.Split(".")[^1];

                    switch (extension.ToLower())
                    {
                        case "wim":
                        case "esd":
                            {
                                try
                                {
                                    WindowsImageIndex[] tempIndexes = IdentifyWindowsFromWim(
                                        facade.OpenFile(fileItem.Location, FileMode.Open, FileAccess.Read), index);
                                    fileItem.Metadata = new MetaData
                                    {
                                        WindowsImageIndexes = tempIndexes
                                    };
                                }
                                catch
                                {
                                }
                                break;
                            }
                        case "sif":
                            {
                                try
                                {
                                    if (fileItem.Location.Contains("txtsetup.sif",
                                            StringComparison.OrdinalIgnoreCase))
                                    {
                                        TxtSetupBridge bridge = new(facade,
                                            string.Join("\\", fileItem.Location.Split('\\')[..^1]));
                                        WindowsImageIndex[] tempIndexes = IdentifyWindowsNTFromRootFs(bridge);

                                        fileItem.Metadata = new MetaData
                                        {
                                            WindowsImageIndexes = tempIndexes
                                        };
                                    }
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
                                if (index)
                                {
                                    try
                                    {
                                        using SparseStream itemstream = facade.OpenFile(fileItem.Location, FileMode.Open,
                                            FileAccess.Read);
                                        using ArchiveFile arch = new(itemstream, SevenZipFormat.PE);

                                        if (arch.Entries.Any(x => x.FileName.EndsWith("version.txt")))
                                        {
                                            Entry ver = arch.Entries.First(x => x.FileName.EndsWith("version.txt"));

                                            using MemoryStream memread = new();
                                            ver.Extract(memread);

                                            memread.Seek(0, SeekOrigin.Begin);

                                            using TextReader tr = new StreamReader(memread, Encoding.Unicode);

                                            fileItem.Version = new Version();

                                            string ln = tr.ReadLine();
                                            while (ln != null)
                                            {
                                                string line = ln.Replace("\0", "");
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
                                    catch
                                    {
                                    }
                                }

                                break;
                            }
                    }

                    if (index)
                    {
                        result.Add(fileItem);
                    }
                    else if (fileItem.Metadata != null && fileItem.Metadata.WindowsImageIndexes != null)
                    {
                        result.Add(fileItem);
                    }

                    if (index && recursivity)
                    {
                        switch (extension.ToLower())
                        {
                            case "wim":
                            case "esd":
                                {
                                    try
                                    {
                                        using SparseStream wimstrm = facade.OpenFile(fileItem.Location, FileMode.Open,
                                            FileAccess.Read);
                                        using ArchiveFile arch = new(wimstrm, SevenZipFormat.Wim);
                                        ArchiveBridge wimparsed = new(arch);

                                        FileItem[] res = HandleFacade(wimparsed);

                                        IEnumerable<FileItem> res2 = res.Select(x =>
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
                                        using SparseStream pestrm = facade.OpenFile(fileItem.Location, FileMode.Open,
                                            FileAccess.Read);
                                        using ArchiveFile arch = new(pestrm, SevenZipFormat.PE);
                                        ArchiveBridge peparsed = new(arch);

                                        FileItem[] res = HandleFacade(peparsed);

                                        IEnumerable<FileItem> res2 = res.Select(x =>
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
                                        using SparseStream isoStream = facade.OpenFile(fileItem.Location, FileMode.Open,
                                            FileAccess.Read);

                                        VfsFileSystemFacade cd = new CDReader(isoStream, true);
                                        if (cd.FileExists(@"README.TXT"))
                                        {
                                            cd = new UdfReader(isoStream);
                                        }

                                        FileItem[] res = HandleFacade(cd);

                                        IEnumerable<FileItem> res2 = res.Select(x =>
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

        public static FileItem[] IdentifyWindowsFromIso(string isopath, bool deep, bool index)
        {
            FileItem[] result = Array.Empty<FileItem>();

            Console.WriteLine();
            Console.WriteLine("Opening ISO File");
            Console.WriteLine(isopath);

            try
            {
                using FileStream isoStream = File.Open(isopath, FileMode.Open, FileAccess.Read);

                VfsFileSystemFacade cd = new CDReader(isoStream, true);
                if (cd.FileExists(@"README.TXT") || cd.Root.GetDirectories().Length == 0)
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

        public static FileItem[] IdentifyWindowsFromMdf(string isopath, bool deep, bool index)
        {
            FileItem[] result = Array.Empty<FileItem>();

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

        public static WindowsImageIndex[] IdentifyWindowsFromWim(Stream wim, bool includePe)
        {
            WindowsImageIndex[] result = Array.Empty<WindowsImageIndex>();

            try
            {
                //
                // If this succeeds we are processing a properly supported final (or near final)
                // WIM file format, so we use the adequate function to handle it.
                //
                result = IdentifyWindowsNTFromWim(wim, includePe);
            }
            catch (UnsupportedWimException)
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

            wim.Dispose();
            return result;
        }

        public static void IdentifyWindowsFromVhd(string vhdpath)
        {
            Console.WriteLine();
            Console.WriteLine("Opening VHD File");
            Console.WriteLine(vhdpath);
            try
            {
                using FileStream vhdStream = File.Open(vhdpath, FileMode.Open, FileAccess.Read);
                IdentifyWindowsNTFromVhd(vhdStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }
        }

        public static void IdentifyWindowsFromVmdk(string vhdpath)
        {
            Console.WriteLine();
            Console.WriteLine("Opening VMDK File");
            Console.WriteLine(vhdpath);
            try
            {
                IdentifyWindowsNTFromVmdk(vhdpath);
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

        public static void IdentifyWindowsFromVhdx(string vhdpath)
        {
            Console.WriteLine();
            Console.WriteLine("Opening VHDX File");
            Console.WriteLine(vhdpath);
            try
            {
                using FileStream vhdStream = File.Open(vhdpath, FileMode.Open, FileAccess.Read);
                IdentifyWindowsNTFromVhdx(vhdStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }
        }

        public static void IdentifyWindowsFromVdi(string vhdpath)
        {
            Console.WriteLine();
            Console.WriteLine("Opening VDI File");
            Console.WriteLine(vhdpath);
            try
            {
                using FileStream vhdStream = File.Open(vhdpath, FileMode.Open, FileAccess.Read);
                IdentifyWindowsNTFromVdi(vhdStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }
        }

        public class UnsupportedWimException : Exception
        {
        }

        public class UnsupportedWimXmlException : Exception
        {
        }
    }
}