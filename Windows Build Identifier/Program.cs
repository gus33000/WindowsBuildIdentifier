/*
 * Copyright (c) 2020, Gustave Monce - gus33000.me - @gus33000
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using DiscUtils.Iso9660;
using DiscUtils.Udf;
using DiscUtils.Vfs;
using SevenZipExtractor;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace WindowsBuildIdentifier
{
    class Program
    {
        static void IdentifyWIMSetup(Stream wimstream)
        {
            Console.WriteLine();

            Console.WriteLine("Gathering WIM information XML file");
            string xml = ExtractWIMXml(wimstream);

            Console.WriteLine("Parsing WIM information XML file");
            XmlFormats.WIMXml.WIM wim = GetWIMClassFromXml(xml);

            Console.WriteLine("Found " + wim.IMAGE.Count() + " images in the wim according to the XML");

            Console.WriteLine("Evaluating relevant images in the WIM according to the XML");
            int irelevantcount2 = (wim.IMAGE.Any(x => x.DESCRIPTION.ToLower().Contains("winpe")) ? 1 : 0) +
                (wim.IMAGE.Any(x => x.DESCRIPTION.ToLower().Contains("setup")) ? 1 : 0) +
                (wim.IMAGE.Any(x => x.DESCRIPTION.ToLower().Contains("preinstallation")) ? 1 : 0) +
                (wim.IMAGE.Any(x => x.DESCRIPTION.ToLower().Contains("winre")) ? 1 : 0) +
                (wim.IMAGE.Any(x => x.DESCRIPTION.ToLower().Contains("recovery")) ? 1 : 0);

            Console.WriteLine("Found " + irelevantcount2 + " irrelevant images in the wim according to the XML");

            foreach (var image in wim.IMAGE)
            {
                Console.WriteLine();
                Console.WriteLine("Processing index " + image.INDEX);

                //
                // If what we're trying to identify isn't just a winpe, and we are accessing a winpe image
                // skip the image
                //
                int irelevantcount = (image.DESCRIPTION.ToLower().Contains("winpe") ? 1 : 0) +
                    (image.DESCRIPTION.ToLower().Contains("setup") ? 1 : 0) +
                    (image.DESCRIPTION.ToLower().Contains("preinstallation") ? 1 : 0) +
                    (image.DESCRIPTION.ToLower().Contains("winre") ? 1 : 0) +
                    (image.DESCRIPTION.ToLower().Contains("recovery") ? 1 : 0);

                Console.WriteLine("Index contains " + irelevantcount + " flags indicating this is a preinstallation environment");

                if (irelevantcount != 0 && irelevantcount2 < wim.IMAGE.Count())
                {
                    Console.WriteLine("Skipping this image");
                    continue;
                }

                string index = wim.IMAGE.Count() == 1 ? null : image.INDEX;

                bool WorkaroundForWIMFormatBug = false;

                if (index != null && wim.IMAGE[0].INDEX == "0")
                {
                    using (ArchiveFile archiveFile = new ArchiveFile(wimstream, SevenZipFormat.Wim))
                    {
                        if (!archiveFile.Entries.Any(x => x.FileName.StartsWith("0\\")))
                        {
                            WorkaroundForWIMFormatBug = true;
                        }
                    }
                }

                if (WorkaroundForWIMFormatBug)
                {
                    int t = int.Parse(index);
                    index = (++t).ToString();
                }

                Console.WriteLine("Index value: " + index);

                var provider = new WIMInstallProviderInterface(wimstream, index);

                var report = IdentifyWindowsInstall.IdentifyWindows(provider);

                provider.Close();

                if (string.IsNullOrEmpty(report.Sku) || report.Sku == "TerminalServer")
                {
                    report.Sku = image.FLAGS;

                    report.Type = null;

                    if ((report.Sku.ToLower().Contains("server") && report.Sku.ToLower().EndsWith("hyperv")) ||
                    (report.Sku.ToLower().Contains("server") && report.Sku.ToLower().EndsWith("v")))
                    {
                        if (report.Type == null)
                        {
                            report.Type = new IdentifyWindowsInstall.Type[] { IdentifyWindowsInstall.Type.ServerV };
                        }
                        else if (!report.Type.Any(x => x == IdentifyWindowsInstall.Type.ServerV))
                        {
                            report.Type = report.Type.Append(IdentifyWindowsInstall.Type.ServerV).ToArray();
                        }
                    }
                    else if (report.Sku.ToLower().Contains("server"))
                    {
                        if (report.Type == null)
                        {
                            report.Type = new IdentifyWindowsInstall.Type[] { IdentifyWindowsInstall.Type.Server };
                        }
                        else if (!report.Type.Any(x => x == IdentifyWindowsInstall.Type.Server))
                        {
                            report.Type = report.Type.Append(IdentifyWindowsInstall.Type.Server).ToArray();
                        }
                    }
                    else
                    {
                        if (report.Type == null)
                        {
                            report.Type = new IdentifyWindowsInstall.Type[] { IdentifyWindowsInstall.Type.Client };
                        }
                        else if (!report.Type.Any(x => x == IdentifyWindowsInstall.Type.Client))
                        {
                            report.Type = report.Type.Append(IdentifyWindowsInstall.Type.Client).ToArray();
                        }
                    }
                }

                IdentifyWindowsInstall.DisplayReport(report);
            }

            wimstream.Dispose();
        }

        static void IdentifyVHD(string vhdpath)
        {
            using (FileStream vhdStream = File.Open(vhdpath, FileMode.Open, FileAccess.Read))
            {
                VHDInstallProviderInterface provider = new VHDInstallProviderInterface(vhdStream);

                var report = IdentifyWindowsInstall.IdentifyWindows(provider);
                IdentifyWindowsInstall.DisplayReport(report);
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("Release Identifier Tool");
            Console.WriteLine("TBD Release Database Indexing Toolset");
            Console.WriteLine("TBD (c) 2008-2020");
            Console.WriteLine("Gustave Monce (@gus33000) (c) 2009-2020");
            Console.WriteLine();

            var ogcolor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Pre-release version. For evaluation purposes only.");
            Console.ForegroundColor = ogcolor;
            Console.WriteLine();

            if (args.Length < 1)
            {
                Console.WriteLine("Usage: <Path to directory containing ISO files>");
                Console.WriteLine();
                return;
            }

            DiscUtils.Complete.SetupHelper.SetupComplete();

            foreach (var isopath in Directory.GetFiles(args[0], "*.iso", SearchOption.AllDirectories))
            {
                /*Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Opening VHD File");
                Console.WriteLine(isopath);
                IdentifyVHD(isopath);*/

                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Opening ISO File");
                Console.WriteLine(isopath);
                try
                {
                    using (FileStream isoStream = File.Open(isopath, FileMode.Open, FileAccess.Read))
                    {
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
                                ExtractWIMXml(cd.OpenFile(@"sources\install.wim", FileMode.Open));
                                //
                                // If this succeeds we are processing a properly supported final (or near final)
                                // WIM file format, so we use the adequate function to handle it.
                                //
                                IdentifyWIMSetup(cd.OpenFile(@"sources\install.wim", FileMode.Open, FileAccess.Read));
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
                                ExtractWIMXml(cd.OpenFile(@"sources\install.esd", FileMode.Open));

                                //
                                // If this succeeds we are processing a properly supported final (or near final)
                                // WIM file format, so we use the adequate function to handle it.
                                //
                                IdentifyWIMSetup(cd.OpenFile(@"sources\install.esd", FileMode.Open, FileAccess.Read));
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
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Fail");
                    Console.WriteLine(ex.ToString());
                }
            }

            Console.WriteLine("Done.");
            Console.ReadLine();
        }

        class UnsupportedWIMException : Exception { }

        class UnsupportedWIMXmlException : Exception { }

        static string ExtractWIMXml(Stream wimstream)
        {
            try
            {
                using (ArchiveFile archiveFile = new ArchiveFile(wimstream, SevenZipFormat.Wim))
                {
                    if (archiveFile.Entries.Any(x => x.FileName == "[1].xml"))
                    {
                        Entry wimXmlEntry = archiveFile.Entries.First(x => x.FileName == "[1].xml");

                        MemoryStream memoryStream = new MemoryStream();
                        wimXmlEntry.Extract(memoryStream);

                        string xml = Encoding.Unicode.GetString(memoryStream.ToArray(), 2, (int)memoryStream.Length - 2);

                        memoryStream.Dispose();

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

        static XmlFormats.WIMXml.WIM GetWIMClassFromXml(string xml)
        {
            try
            {
                XmlSerializer ser = new XmlSerializer(typeof(XmlFormats.WIMXml.WIM));
                XmlFormats.WIMXml.WIM wim;
                using (StringReader stream = new StringReader(xml))
                {
                    using (XmlReader reader = XmlReader.Create(stream))
                    {
                        wim = (XmlFormats.WIMXml.WIM)ser.Deserialize(reader);
                    }
                }
                return wim;
            }
            catch (InvalidOperationException)
            {
                throw new UnsupportedWIMXmlException();
            }
        }
    }
}