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

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using WindowsBuildIdentifier.Identification;

namespace WindowsBuildIdentifier
{
    public class FileItem
    {
        public string Location;

        public string CreationTime;
        public string LastAccessTime;
        public string LastWriteTime;

        public Hash Hash;

        public string Size;

        public Version Version;

        public string[] Attributes;

        public MetaData Metadata;
    }

    public class Version
    {
        public string CompanyName;
        public string FileDescription;
        public string FileVersion;
        public string InternalName;
        public string LegalCopyright;
        public string OriginalFilename;
        public string ProductName;
        public string ProductVersion;
    }

    public class Hash
    {
        public string MD5;
        public string SHA1;
        public string CRC32;
    }

    public class MetaData
    {
        public WindowsImageIndex[] WindowsImageIndexes;
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("Release Identifier Tool");
            Console.WriteLine("BetaArchive Release Database Indexing Toolset");
            Console.WriteLine("BetaArchive (c) 2008-2020");
            Console.WriteLine("Gustave Monce (@gus33000) (c) 2009-2020");
            Console.WriteLine();

            var ogcolor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Pre-release version. For evaluation purposes only.");
            Console.ForegroundColor = ogcolor;
            Console.WriteLine();

            if (args.Length < 2)
            {
                Console.WriteLine("Usage: <Path to file to analyze> <Path to output xml file>");
                Console.WriteLine();
                return;
            }

            DiscUtils.Complete.SetupHelper.SetupComplete();

            var file = args[0];
            var extension = file.Split(".")[^1];

            switch (extension.ToLower())
            {
                case "vhd":
                    {
                        Identification.MediaHandler.IdentifyWindowsFromVHD(file);
                        break;
                    }
                case "vhdx":
                    {
                        Identification.MediaHandler.IdentifyWindowsFromVHDX(file);
                        break;
                    }
                case "iso":
                    {
                        FileItem[] result = Identification.MediaHandler.IdentifyWindowsFromISO(file);

                        XmlSerializer xsSubmit = new XmlSerializer(typeof(FileItem[]));
                        var xml = "";

                        using (var sww = new StringWriter())
                        {
                            XmlWriterSettings settings = new XmlWriterSettings();
                            settings.Indent = true;
                            settings.IndentChars = "     ";
                            settings.NewLineOnAttributes = false;
                            settings.OmitXmlDeclaration = true;

                            using (XmlWriter writer = XmlWriter.Create(sww, settings))
                            {
                                xsSubmit.Serialize(writer, result);
                                xml = sww.ToString();
                            }
                        }

                        File.WriteAllText(args[1] + @"\" + "meta_index.xml", xml);

                        if (result.Any(x => x.Location.ToLower() == @"\sources\install.wim"))
                        {
                            var wimtag = result.First(x => x.Location.ToLower() == @"\sources\install.wim");

                            xsSubmit = new XmlSerializer(typeof(WindowsImageIndex[]));
                            xml = "";

                            using (var sww = new StringWriter())
                            {
                                XmlWriterSettings settings = new XmlWriterSettings();
                                settings.Indent = true;
                                settings.IndentChars = "     ";
                                settings.NewLineOnAttributes = false;
                                settings.OmitXmlDeclaration = true;

                                using (XmlWriter writer = XmlWriter.Create(sww, settings))
                                {
                                    xsSubmit.Serialize(writer, wimtag.Metadata.WindowsImageIndexes);
                                    xml = sww.ToString();
                                }
                            }

                            File.WriteAllText(args[1] + @"\" + "meta_windows_image.xml", xml);
                        }

                        break;
                    }
                case "mdf":
                    {
                        FileItem[] result = Identification.MediaHandler.IdentifyWindowsFromMDF(file);

                        XmlSerializer xsSubmit = new XmlSerializer(typeof(FileItem[]));
                        var xml = "";

                        using (var sww = new StringWriter())
                        {
                            using (XmlWriter writer = XmlWriter.Create(sww))
                            {
                                writer.Settings.Indent = true;
                                writer.Settings.IndentChars = "     ";
                                writer.Settings.NewLineOnAttributes = false;
                                writer.Settings.OmitXmlDeclaration = true;

                                xsSubmit.Serialize(writer, result);
                                xml = sww.ToString();
                            }
                        }

                        File.WriteAllText(args[1], xml);

                        break;
                    }
                case "vmdk":
                    {
                        Identification.MediaHandler.IdentifyWindowsFromVMDK(file);
                        break;
                    }
                case "vdi":
                    {
                        Identification.MediaHandler.IdentifyWindowsFromVDI(file);
                        break;
                    }
                case "wim":
                case "esd":
                    {
                        Identification.MediaHandler.IdentifyWindowsFromWIM(new FileStream(file, FileMode.Open, FileAccess.Read));
                        break;
                    }
            }

            Console.WriteLine("Done.");

#if DEBUG
            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            }
#endif
        }
    }
}
