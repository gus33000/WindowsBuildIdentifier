using DiscUtils.Iso9660;
using DiscUtils.Udf;
using DiscUtils.Vfs;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            //try
            //{
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
            //}
            /*catch (SevenZipException)
            {
                throw new UnsupportedWIMException();
            }*/
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

        private static void IdentifyWindowsNTFromWIM(Stream wimstream)
        {
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

                if (irelevantcount != 0 && irelevantcount2 < wim.IMAGE.Length)
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

                if (string.IsNullOrEmpty(report.Sku) || report.Sku == "TerminalServer")
                {
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
            }

            wimstream.Dispose();
        }

        private static void IdentifyWindowsNTFromVHD(Stream vhdstream)
        {
            VHDInstallProviderInterface provider = new VHDInstallProviderInterface(vhdstream);

            var report = InstalledImage.DetectionHandler.IdentifyWindowsNT(provider);
            Common.DisplayReport(report);
        }

        public static void IdentifyWindowsFromISO(string isopath)
        {
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

                //
                // WIM Setup
                //
                if (cd.FileExists(@"sources\install.wim"))
                {
                    try
                    {
                        //
                        // If this succeeds we are processing a properly supported final (or near final)
                        // WIM file format, so we use the adequate function to handle it.
                        //
                        IdentifyWindowsNTFromWIM(cd.OpenFile(@"sources\install.wim", FileMode.Open, FileAccess.Read));
                    }
                    catch (UnsupportedWIMException)
                    {
                        //
                        // If this fails we are processing an early
                        // WIM file format, so we use the adequate function to handle it.
                        //
                        Console.WriteLine("Early WIM Format TODO");
                    }
                }
                else if (cd.FileExists(@"sources\install.esd"))
                {
                    try
                    {
                        //
                        // If this succeeds we are processing a properly supported final (or near final)
                        // WIM file format, so we use the adequate function to handle it.
                        //
                        IdentifyWindowsNTFromWIM(cd.OpenFile(@"sources\install.esd", FileMode.Open, FileAccess.Read));
                    }
                    catch (UnsupportedWIMException)
                    {
                        //
                        // If this fails we are processing an early
                        // WIM file format, so we use the adequate function to handle it.
                        //
                        Console.WriteLine("Early WIM Format TODO");
                    }
                }
                else
                {
                    Console.WriteLine("No idea");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fail");
                Console.WriteLine(ex.ToString());
            }
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
    }
}
