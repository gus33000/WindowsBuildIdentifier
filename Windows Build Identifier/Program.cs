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

using CommandLine;
using DiscUtils.Iso9660;
using DiscUtils.Udf;
using DiscUtils.Vfs;
using System;
using System.Collections.Generic;
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
        [Verb("diff", HelpText = "Diff two builds based on their meta_index.xml files. Note: index files must have been generated with the Deep option.")]
        public class DiffOptions
        {
            [Option('i', "index-1", Required = true, HelpText = "The media index file number 1.")]
            public string Index1 { get; set; }

            [Option('j', "index-2", Required = true, HelpText = "The media index file number 2.")]
            public string Index2 { get; set; }
        }

        [Verb("identify", HelpText = "Identify a build from a media file.")]
        public class IdentifyOptions
        {
            [Option('m', "media", Required = true, HelpText = "The media file to work on.")]
            public string Media { get; set; }

            [Option('o', "output", Required = true, HelpText = "The destination path for the windows index file.")]
            public string Output { get; set; }
        }

        [Verb("index", HelpText = "Index a build from a media file.")]
        public class IndexOptions
        {
            [Option('m', "media", Required = true, HelpText = "The media file to work on.")]
            public string Media { get; set; }

            [Option('o', "output", Required = true, HelpText = "The destination path for the index file.")]
            public string Output { get; set; }

            [Option('d', "deep", Required = false, Default = false, HelpText = "Perform a deep scan. A deep scan will recursively index files inside of various recognized container types such as wims, isos and etc...")]
            public bool Deep { get; set; }
        }

        [Verb("rename", HelpText = "Rename a build from a media file.")]
        public class RenameOptions
        {
            [Option('m', "media", Required = true, HelpText = "The media file to work on.")]
            public string Media { get; set; }

            [Option('w', "windows-index", Required = true, HelpText = "The path of the windows index file.")]
            public string WindowsIndex { get; set; }
        }

        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<DiffOptions, IdentifyOptions, IndexOptions, RenameOptions>(args)
            .MapResult(
              (DiffOptions opts) => RunDiffAndReturnExitCode(opts),
              (IdentifyOptions opts) => RunIdentifyAndReturnExitCode(opts),
              (IndexOptions opts) => RunIndexAndReturnExitCode(opts),
              (RenameOptions opts) => RunRenameAndReturnExitCode(opts),
              errs => 1);
        }

        private static int RunRenameAndReturnExitCode(RenameOptions opts)
        {
            PrintBanner();

            Console.WriteLine("Input xml file: " + opts.WindowsIndex);

            Console.WriteLine("Input media: " + opts.Media);

            XmlSerializer deserializer = new XmlSerializer(typeof(WindowsImageIndex[]));
            TextReader reader = new StreamReader(opts.WindowsIndex);
            object obj = deserializer.Deserialize(reader);
            WindowsImageIndex[] XmlData = (WindowsImageIndex[])obj;
            reader.Close();

            var f = XmlData[0].WindowsImage;

            Common.DisplayReport(f);

            var buildtag = $"{f.MajorVersion}.{f.MinorVersion}.{f.BuildNumber}.{f.MinorVersion}";

            if (!string.IsNullOrEmpty(f.BranchName))
            {
                buildtag += $".{f.BranchName}.{f.CompileDate}";
            }

            var types = f.Types;
            var licensings = new HashSet<Licensing> { f.Licensing };
            var languages = f.LanguageCodes.ToHashSet();
            var skus = new HashSet<string> { f.Sku };

            for (int i = 1; i < XmlData.Length; i++)
            {
                var d = XmlData[i].WindowsImage;
                Common.DisplayReport(d);

                types = types.Union(d.Types).ToHashSet();
                if (!licensings.Contains(d.Licensing))
                {
                    licensings.Add(d.Licensing);
                }
                languages = languages.Union(d.LanguageCodes).ToHashSet();
                if (!skus.Contains(d.Sku))
                {
                    skus.Add(d.Sku);
                }
            }

            Console.WriteLine($"Build tag: {buildtag}");
            Console.WriteLine();

            var filename = $"{buildtag}_{f.Architecture}{f.BuildType}_{string.Join("-", types)}-{string.Join("-", skus)}_{string.Join("-", licensings)}_{string.Join("-", languages)}";
            filename = filename.ToLower();

            var fileextension = opts.Media.Split(@".")[^1];
            string label = "";

            if (fileextension == "iso" || fileextension == "mdf")
            {
                DiscUtils.Complete.SetupHelper.SetupComplete();

                try
                {
                    using FileStream isoStream = File.Open(opts.Media, FileMode.Open, FileAccess.Read);

                    VfsFileSystemFacade cd = new CDReader(isoStream, true);
                    if (cd.FileExists(@"README.TXT"))
                    {
                        cd = new UdfReader(isoStream);
                    }

                    label = cd.VolumeLabel;
                }
                catch { };
            }

            var dst = filename + "." + fileextension;
            if (!string.IsNullOrEmpty(label))
                dst = filename + "-" + label + "." + fileextension;

            dst = string.Join(@"\", opts.Media.Split(@"\")[0..^1]) + @"\" + dst;

            Console.WriteLine($"Target filename: {dst}");
            Console.WriteLine();

            if (opts.Media == dst)
            {
                Console.WriteLine("Nothing to do, file name is already good");
            }
            else
            {
                Console.WriteLine("Renaming");
                File.Move(opts.Media, dst);
            }

            Console.WriteLine("Done.");

#if DEBUG
            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            }
#endif
            return 0;
        }

        private static int RunIndexAndReturnExitCode(IndexOptions opts)
        {
            PrintBanner();

            DiscUtils.Complete.SetupHelper.SetupComplete();

            var file = opts.Media;
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
                        FileItem[] result = Identification.MediaHandler.IdentifyWindowsFromISO(file, opts.Deep, true);

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

                        File.WriteAllText(opts.Output, xml);

                        break;
                    }
                case "mdf":
                    {
                        FileItem[] result = Identification.MediaHandler.IdentifyWindowsFromMDF(file, opts.Deep, true);

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

                        File.WriteAllText(opts.Output, xml);

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
                        Identification.MediaHandler.IdentifyWindowsFromWIM(new FileStream(file, FileMode.Open, FileAccess.Read), true);
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
            return 0;
        }

        private static int RunIdentifyAndReturnExitCode(IdentifyOptions opts)
        {
            PrintBanner();

            DiscUtils.Complete.SetupHelper.SetupComplete();

            var file = opts.Media;
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
                        FileItem[] result = Identification.MediaHandler.IdentifyWindowsFromISO(file, false, false);

                        if (result.Any(x => x.Location.ToLower() == @"\sources\install.wim"))
                        {
                            var wimtag = result.First(x => x.Location.ToLower() == @"\sources\install.wim");

                            XmlSerializer xsSubmit = new XmlSerializer(typeof(WindowsImageIndex[]));
                            string xml = "";

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

                            File.WriteAllText(opts.Output, xml);
                        }

                        if (result.Any(x => x.Location.ToLower() == @"\sources\install.esd"))
                        {
                            var wimtag = result.First(x => x.Location.ToLower() == @"\sources\install.esd");

                            XmlSerializer xsSubmit = new XmlSerializer(typeof(WindowsImageIndex[]));
                            string xml = "";

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

                            File.WriteAllText(opts.Output, xml);
                        }

                        if (result.Any(x => x.Location.ToLower().EndsWith(@"\txtsetup.sif")))
                        {
                            var txtsetups = result.Where(x => x.Location.ToLower().EndsWith(@"\txtsetup.sif")).Select(x => x.Metadata.WindowsImageIndexes);

                            WindowsImageIndex[] indexes = null;
                            foreach (var arr in txtsetups)
                            {
                                if (indexes == null)
                                {
                                    indexes = arr;
                                }
                                else
                                {
                                    var tmplist = indexes.ToList();
                                    tmplist.AddRange(arr);
                                    indexes = tmplist.ToArray();
                                }
                            }

                            XmlSerializer xsSubmit = new XmlSerializer(typeof(WindowsImageIndex[]));
                            string xml = "";

                            using (var sww = new StringWriter())
                            {
                                XmlWriterSettings settings = new XmlWriterSettings();
                                settings.Indent = true;
                                settings.IndentChars = "     ";
                                settings.NewLineOnAttributes = false;
                                settings.OmitXmlDeclaration = true;

                                using (XmlWriter writer = XmlWriter.Create(sww, settings))
                                {
                                    xsSubmit.Serialize(writer, indexes);
                                    xml = sww.ToString();
                                }
                            }

                            File.WriteAllText(opts.Output, xml);
                        }

                        break;
                    }
                case "mdf":
                    {
                        FileItem[] result = Identification.MediaHandler.IdentifyWindowsFromMDF(file, false, false);

                        if (result.Any(x => x.Location.ToLower() == @"\sources\install.wim"))
                        {
                            var wimtag = result.First(x => x.Location.ToLower() == @"\sources\install.wim");

                            XmlSerializer xsSubmit = new XmlSerializer(typeof(WindowsImageIndex[]));
                            string xml = "";

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

                            File.WriteAllText(opts.Output, xml);
                        }

                        if (result.Any(x => x.Location.ToLower() == @"\sources\install.esd"))
                        {
                            var wimtag = result.First(x => x.Location.ToLower() == @"\sources\install.esd");

                            XmlSerializer xsSubmit = new XmlSerializer(typeof(WindowsImageIndex[]));
                            string xml = "";

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

                            File.WriteAllText(opts.Output, xml);
                        }

                        if (result.Any(x => x.Location.ToLower().EndsWith(@"\txtsetup.sif")))
                        {
                            var txtsetups = result.Where(x => x.Location.ToLower().EndsWith(@"\txtsetup.sif")).Select(x => x.Metadata.WindowsImageIndexes);

                            WindowsImageIndex[] indexes = null;
                            foreach (var arr in txtsetups)
                            {
                                if (indexes == null)
                                {
                                    indexes = arr;
                                }
                                else
                                {
                                    var tmplist = indexes.ToList();
                                    tmplist.AddRange(arr);
                                    indexes = tmplist.ToArray();
                                }
                            }

                            XmlSerializer xsSubmit = new XmlSerializer(typeof(WindowsImageIndex[]));
                            string xml = "";

                            using (var sww = new StringWriter())
                            {
                                XmlWriterSettings settings = new XmlWriterSettings();
                                settings.Indent = true;
                                settings.IndentChars = "     ";
                                settings.NewLineOnAttributes = false;
                                settings.OmitXmlDeclaration = true;

                                using (XmlWriter writer = XmlWriter.Create(sww, settings))
                                {
                                    xsSubmit.Serialize(writer, indexes);
                                    xml = sww.ToString();
                                }
                            }

                            File.WriteAllText(opts.Output, xml);
                        }

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
                        var wimindexes = Identification.MediaHandler.IdentifyWindowsFromWIM(new FileStream(file, FileMode.Open, FileAccess.Read), false);
                        
                        XmlSerializer xsSubmit = new XmlSerializer(typeof(WindowsImageIndex[]));
                        string xml = "";

                        using (var sww = new StringWriter())
                        {
                            XmlWriterSettings settings = new XmlWriterSettings();
                            settings.Indent = true;
                            settings.IndentChars = "     ";
                            settings.NewLineOnAttributes = false;
                            settings.OmitXmlDeclaration = true;

                            using (XmlWriter writer = XmlWriter.Create(sww, settings))
                            {
                                xsSubmit.Serialize(writer, wimindexes);
                                xml = sww.ToString();
                            }
                        }

                        File.WriteAllText(opts.Output, xml);
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
            return 0;
        }

        private static void PrintBanner()
        {
            Console.WriteLine();
            Console.WriteLine("Release Identifier Tool");
            Console.WriteLine("Release Database Indexing Toolset");
            Console.WriteLine("Gustave Monce (@gus33000) (c) 2009-2020");
            Console.WriteLine();
        }

        private static int RunDiffAndReturnExitCode(DiffOptions opts)
        {
            PrintBanner();
            Comparer.CompareBuilds(opts.Index1, opts.Index2);

            Console.WriteLine("Done.");

#if DEBUG
            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            }
#endif
            return 0;
        }
    }
}
