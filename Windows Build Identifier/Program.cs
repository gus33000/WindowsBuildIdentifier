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

            [Option('o', "output", HelpText = "The destination path for the windows index file.")]
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

            [Option('w', "windows-index", HelpText = "The path of the windows index file.")]
            public string WindowsIndex { get; set; }
        }

        [Verb("bulkrename", HelpText = "Rename builds in a folder.")]
        public class BulkRenameOptions
        {
            [Option('i', "input", Required = true, HelpText = "The input folder to work on.")]
            public string Input { get; set; }
        }

        [Verb("bulksort", HelpText = "Sorts builds in a folder.")]
        public class BulkSortOptions
        {
            [Option('i', "input", Required = true, HelpText = "The input folder to work on.")]
            public string Input { get; set; }
            [Option('o', "output", Required = true, HelpText = "The output folder to work on.")]
            public string Output { get; set; }
        }

        static int Main(string[] args)
        {
            return CommandLine.Parser.Default.ParseArguments<DiffOptions, IdentifyOptions, IndexOptions, RenameOptions, BulkSortOptions, BulkRenameOptions>(args)
            .MapResult(
              (DiffOptions opts) => RunDiffAndReturnExitCode(opts),
              (IdentifyOptions opts) => RunIdentifyAndReturnExitCode(opts),
              (IndexOptions opts) => RunIndexAndReturnExitCode(opts),
              (RenameOptions opts) => RunRenameAndReturnExitCode(opts),
              (BulkSortOptions opts) => RunBulkSortAndReturnExitCode(opts),
              (BulkRenameOptions opts) => RunBulkRenameAndReturnExitCode(opts),
              errs => 1);
        }

        public static string ReplaceInvalidChars(string filename)
        {
            return Path.Join(filename.Replace(Path.GetFileName(filename), ""),  string.Join("_", Path.GetFileName(filename).Split(Path.GetInvalidFileNameChars())));
        }

        private static (string, string) GetAdequateNameFromImageIndexes(WindowsImageIndex[] imageIndexes)
        {
            var f = imageIndexes[0].WindowsImage;

            Common.DisplayReport(f);

            var buildtag = $"{f.MajorVersion}.{f.MinorVersion}.{f.BuildNumber}.{f.DeltaVersion}";

            if (!string.IsNullOrEmpty(f.BranchName))
            {
                buildtag += $".{f.BranchName}.{f.CompileDate}";
            }

            var types = f.Types;
            var licensings = new HashSet<Licensing> { f.Licensing };
            var languages = f.LanguageCodes != null ? f.LanguageCodes.ToHashSet() : new HashSet<string>() { "lang-unknown" };
            var skus = new HashSet<string> { f.Sku.Replace("Server", "") };
            var baseSkus = new HashSet<string> { f.Sku.Replace("Server", "") };
            var archs = new HashSet<string> { $"{f.Architecture}{f.BuildType}" };

            for (int i = 1; i < imageIndexes.Length; i++)
            {
                var d = imageIndexes[i].WindowsImage;
                Common.DisplayReport(d);

                types = types.Union(d.Types).ToHashSet();
                if (!licensings.Contains(d.Licensing))
                {
                    licensings.Add(d.Licensing);
                }
                languages = languages.Union(d.LanguageCodes).ToHashSet();

                if (!skus.Contains(d.Sku.Replace("Server", "")))
                {
                    skus.Add(d.Sku.Replace("Server", ""));
                }

                if (!baseSkus.Contains(d.BaseSku.Replace("Server", "")))
                {
                    baseSkus.Add(d.BaseSku.Replace("Server", ""));
                }

                if (!archs.Contains($"{f.Architecture}{f.BuildType}"))
                {
                    archs.Add($"{f.Architecture}{f.BuildType}");
                }
            }

            Console.WriteLine($"Build tag: {buildtag}");
            Console.WriteLine();

            string skustr = skus.Count > 5 ? string.Join("-", baseSkus) + "-multi" : string.Join("-", skus);
            string licensingstr = licensings.Count == 0 ? "" : "_" + string.Join("-", licensings);

            var filename = $"{string.Join("-", archs)}_{string.Join("-", types)}-{skustr}{licensingstr}_{string.Join("-", languages)}";

            return (buildtag, filename);
        }

        private static int RunBulkRenameAndReturnExitCode(BulkRenameOptions opts)
        {
            string path = opts.Input;

            var ifiles = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories);

            foreach (var file in ifiles)
            {
                FileItem[] result;

                switch (file.Split(".")[^1].ToLower())
                {
                    case "iso":
                        {
                            result = Identification.MediaHandler.IdentifyWindowsFromISO(file, false, false);
                            break;
                        }
                    case "mdf":
                        {
                            result = Identification.MediaHandler.IdentifyWindowsFromMDF(file, false, false);
                            break;
                        }
                    case "wim":
                    case "esd":
                        {
                            var images = Identification.MediaHandler.IdentifyWindowsFromWIM(File.OpenRead(file), true);
                            if (images.Length > 0)
                            {
                                result = new FileItem[] { new FileItem() { Metadata = new MetaData() { WindowsImageIndexes = images } } };
                            }
                            else
                            {
                                continue;
                            }
                            break;
                        }
                    default:
                        {
                            continue;
                        }
                }

                if (!result.Any(x => x.Metadata != null && x.Metadata.WindowsImageIndexes != null && x.Metadata.WindowsImageIndexes.Length != 0))
                    continue;

                var files = result.Where(x => x.Metadata != null && x.Metadata.WindowsImageIndexes != null && x.Metadata.WindowsImageIndexes.Length != 0).ToArray();

                WindowsImageIndex[] WindowsImageIndexes = files[0].Metadata.WindowsImageIndexes;

                if (files.Any(x => x.Location.EndsWith("install.wim", StringComparison.InvariantCultureIgnoreCase)))
                {
                    WindowsImageIndexes = files.First(x => x.Location.EndsWith("install.wim")).Metadata.WindowsImageIndexes;
                }
                else if (files.Any(x => x.Location.EndsWith("txtsetup.sif", StringComparison.InvariantCultureIgnoreCase)))
                {
                    foreach (var vfile in files.Where(x => x.Location.EndsWith("txtsetup.sif", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        WindowsImageIndexes = WindowsImageIndexes.Union(vfile.Metadata.WindowsImageIndexes).ToArray();
                    }
                }

                (string buildtag, string filename) = GetAdequateNameFromImageIndexes(WindowsImageIndexes);

                filename = buildtag + "_" + filename;
                filename = filename.ToLower();

                var fileextension = file.Split(@".")[^1];
                string label = "";

                if (fileextension == "iso" || fileextension == "mdf")
                {
                    DiscUtils.Complete.SetupHelper.SetupComplete();

                    try
                    {
                        using FileStream isoStream = File.Open(file, FileMode.Open, FileAccess.Read);

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

                dst = string.Join(@"\", file.Split(@"\")[0..^1]) + @"\" + dst;

                Console.WriteLine($"Target filename: {dst}");
                Console.WriteLine();

                if (file == ReplaceInvalidChars(dst))
                {
                    Console.WriteLine("Nothing to do, file name is already good");
                }
                else
                {
                    Console.WriteLine("Renaming");
                    File.Move(file, ReplaceInvalidChars(dst));
                }

                Console.WriteLine("Done.");
            }

            return 0;
        }

        private static int RunBulkSortAndReturnExitCode(BulkSortOptions opts)
        {
            string path = opts.Input;
            string output = opts.Output;

            if (!Directory.Exists(output))
            {
                Directory.CreateDirectory(output);
            }

            var ifiles = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories);

            foreach (var file in ifiles)
            {
                FileItem[] result;

                switch (file.Split(".")[^1].ToLower())
                {
                    case "iso":
                        {
                            result = Identification.MediaHandler.IdentifyWindowsFromISO(file, false, false);
                            break;
                        }
                    case "mdf":
                        {
                            result = Identification.MediaHandler.IdentifyWindowsFromMDF(file, false, false);
                            break;
                        }
                    case "wim":
                    case "esd":
                        {
                            var images = Identification.MediaHandler.IdentifyWindowsFromWIM(File.OpenRead(file), true);
                            if (images.Length > 0)
                            {
                                result = new FileItem[] { new FileItem() { Metadata = new MetaData() { WindowsImageIndexes = images } } };
                            }
                            else
                            {
                                continue;
                            }
                            break;
                        }
                    default:
                        {
                            continue;
                        }
                }

                if (!result.Any(x => x.Metadata != null && x.Metadata.WindowsImageIndexes != null && x.Metadata.WindowsImageIndexes.Length != 0))
                    continue;

                var files = result.Where(x => x.Metadata != null && x.Metadata.WindowsImageIndexes != null && x.Metadata.WindowsImageIndexes.Length != 0).ToArray();

                WindowsImageIndex[] WindowsImageIndexes = files[0].Metadata.WindowsImageIndexes;

                if (files.Any(x => x.Location.EndsWith("install.wim", StringComparison.InvariantCultureIgnoreCase)))
                {
                    WindowsImageIndexes = files.First(x => x.Location.EndsWith("install.wim")).Metadata.WindowsImageIndexes;
                }
                else if (files.Any(x => x.Location.EndsWith("txtsetup.sif", StringComparison.InvariantCultureIgnoreCase)))
                {
                    foreach (var vfile in files.Where(x => x.Location.EndsWith("txtsetup.sif", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        WindowsImageIndexes = WindowsImageIndexes.Union(vfile.Metadata.WindowsImageIndexes).ToArray();
                    }
                }

                (string buildtag, string filename) = GetAdequateNameFromImageIndexes(WindowsImageIndexes);

                string label = "";

                DiscUtils.Complete.SetupHelper.SetupComplete();

                try
                {
                    using FileStream isoStream = File.Open(file, FileMode.Open, FileAccess.Read);

                    VfsFileSystemFacade cd = new CDReader(isoStream, true);
                    if (cd.FileExists(@"README.TXT"))
                    {
                        cd = new UdfReader(isoStream);
                    }

                    label = cd.VolumeLabel;
                }
                catch { };

                label = label.Trim('\0');

                var dst = filename;
                if (!string.IsNullOrEmpty(label))
                    dst = filename + "-" + label;

                var middlepath = Path.Join(output, buildtag);
                if (!Directory.Exists(middlepath))
                {
                    Directory.CreateDirectory(middlepath);
                }

                var finalpath = Path.Join(middlepath, dst);
                if (!Directory.Exists(finalpath))
                {
                    Directory.CreateDirectory(finalpath);
                }

                File.Move(file, ReplaceInvalidChars(Path.Combine(finalpath, file.Split(@"\")[^1])));
            }

            return 0;
        }

        private static int RunRenameAndReturnExitCode(RenameOptions opts)
        {
            PrintBanner();

            if (string.IsNullOrEmpty(opts.WindowsIndex))
            {
                opts.WindowsIndex = $"{opts.Media}.meta_id.xml";
            }

            Console.WriteLine("Input xml file: " + opts.WindowsIndex);

            Console.WriteLine("Input media: " + opts.Media);

            XmlSerializer deserializer = new XmlSerializer(typeof(WindowsImageIndex[]));
            TextReader reader = new StreamReader(opts.WindowsIndex);
            object obj = deserializer.Deserialize(reader);
            WindowsImageIndex[] XmlData = (WindowsImageIndex[])obj;
            reader.Close();

            (string buildtag, string filename) = GetAdequateNameFromImageIndexes(XmlData);

            filename = buildtag + "_" + filename;
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

            if (opts.Media == ReplaceInvalidChars(dst))
            {
                Console.WriteLine("Nothing to do, file name is already good");
            }
            else
            {
                Console.WriteLine("Renaming");
                File.Move(opts.Media, ReplaceInvalidChars(dst));

                if (opts.WindowsIndex.Contains(opts.Media))
                {
                    File.Move(opts.WindowsIndex, opts.WindowsIndex.Replace(opts.Media, ReplaceInvalidChars(dst)));
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
            if (string.IsNullOrEmpty(opts.Output))
            {
                opts.Output = $"{file}.meta_id.xml";
            }

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
