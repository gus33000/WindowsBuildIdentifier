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
using DiscUtils.Complete;
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
using Type = WindowsBuildIdentifier.Identification.Type;

namespace WindowsBuildIdentifier
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            return Parser.Default
                .ParseArguments<DiffOptions, IdentifyOptions, IndexOptions, RenameOptions, BulkSortOptions,
                    BulkRenameOptions>(args)
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
            string dir = Path.GetDirectoryName(filename);
            char[] file = Path.GetFileName(filename).ToCharArray();

            char[] remove = Path.GetInvalidFileNameChars();
            for (int i = 0; i < file.Length; i++)
            {
                if (remove.Contains(file[i]))
                {
                    file[i] = '_';
                }
            }

            return Path.Join(dir, file);
        }

        private static (string, string) GetAdequateNameFromImageIndexes(WindowsImageIndex[] imageIndexes)
        {
            WindowsImage f = imageIndexes[0].WindowsImage;

            Common.DisplayReport(f);

            string buildtag = $"{f.MajorVersion}.{f.MinorVersion}.{f.BuildNumber}.{f.DeltaVersion}";

            if (!string.IsNullOrEmpty(f.BranchName))
            {
                buildtag += $".{f.BranchName}.{f.CompileDate}";
            }

            HashSet<Type> types = f.Types;
            SortedSet<Licensing> licensings = new() { f.Licensing };
            SortedSet<string> languages = new(f.LanguageCodes ?? new[] { "lang-unknown" });
            SortedSet<string> skus = new() { f.Sku.Replace("Server", "") };
            SortedSet<string> baseSkus = string.IsNullOrEmpty(f.BaseSku)
                ? new()
                : new() { f.BaseSku.Replace("Server", "") };
            SortedSet<string> archs = new() { $"{f.Architecture}{f.BuildType}" };

            for (int i = 1; i < imageIndexes.Length; i++)
            {
                WindowsImage d = imageIndexes[i].WindowsImage;
                Common.DisplayReport(d);

                types = types.Union(d.Types).ToHashSet();
                if (!licensings.Contains(d.Licensing))
                {
                    licensings.Add(d.Licensing);
                }

                languages = new SortedSet<string>(languages.Union(d.LanguageCodes));

                if (!skus.Contains(d.Sku.Replace("Server", "")))
                {
                    skus.Add(d.Sku.Replace("Server", ""));
                }

                if (d.BaseSku != null && !baseSkus.Contains(d.BaseSku.Replace("Server", "")))
                {
                    baseSkus.Add(d.BaseSku.Replace("Server", ""));
                }

                if (!archs.Contains($"{f.Architecture}{f.BuildType}"))
                {
                    archs.Add($"{f.Architecture}{f.BuildType}");
                }
            }

            // Don't include core SKUs in filename list if full is also included
            foreach (string sku in skus.ToArray())
            {
                if (sku.Length >= 5 && sku.EndsWith("Core") && skus.Contains(sku[..^4]))
                {
                    skus.Remove(sku);
                }
            }

            foreach (string baseSku in baseSkus.ToArray())
            {
                if (baseSku.Length >= 5 && baseSku.EndsWith("Core") && baseSkus.Contains(baseSku[..^4]))
                {
                    baseSkus.Remove(baseSku);
                }
            }

            Console.WriteLine($"Build tag: {buildtag}");
            Console.WriteLine();

            string skustr = skus.Count > 5 && baseSkus.Count < skus.Count
                ? string.Join("-", baseSkus) + "-multi"
                : string.Join("-", skus);
            string licensingstr = licensings.Count == 0 ? "" : "_" + string.Join("-", licensings);

            string filename =
                $"{string.Join("-", archs)}_{string.Join("-", types)}-{skustr}{licensingstr}_{string.Join("-", languages)}";

            return (buildtag, filename);
        }

        private static int RunBulkRenameAndReturnExitCode(BulkRenameOptions opts)
        {
            string path = opts.Input;

            IEnumerable<string> ifiles = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories);

            foreach (string file in ifiles)
            {
                FileItem[] result;

                switch (file.Split(".")[^1].ToLower())
                {
                    case "iso":
                        {
                            result = MediaHandler.IdentifyWindowsFromIso(file, false, false);
                            break;
                        }
                    case "mdf":
                        {
                            result = MediaHandler.IdentifyWindowsFromMdf(file, false, false);
                            break;
                        }
                    case "wim":
                    case "esd":
                        {
                            WindowsImageIndex[] images = MediaHandler.IdentifyWindowsFromWim(File.OpenRead(file), true);
                            if (images.Length > 0)
                            {
                                result = new FileItem[] { new() { Metadata = new MetaData { WindowsImageIndexes = images } } };
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

                if (!result.Any(x =>
                        x.Metadata != null && x.Metadata.WindowsImageIndexes != null &&
                        x.Metadata.WindowsImageIndexes.Length != 0))
                {
                    continue;
                }

                FileItem[] files = result.Where(x =>
                    x.Metadata != null && x.Metadata.WindowsImageIndexes != null &&
                    x.Metadata.WindowsImageIndexes.Length != 0).ToArray();

                WindowsImageIndex[] windowsImageIndexes = files[0].Metadata.WindowsImageIndexes;

                if (files.Any(x => x.Location.EndsWith("install.wim", StringComparison.OrdinalIgnoreCase)))
                {
                    windowsImageIndexes = files.First(x => x.Location.EndsWith("install.wim", StringComparison.OrdinalIgnoreCase)).Metadata
                        .WindowsImageIndexes;
                }
                else if (files.Any(
                             x => x.Location.EndsWith("txtsetup.sif", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (FileItem vfile in files.Where(x =>
                                 x.Location.EndsWith("txtsetup.sif", StringComparison.OrdinalIgnoreCase)))
                    {
                        windowsImageIndexes = windowsImageIndexes.Union(vfile.Metadata.WindowsImageIndexes).ToArray();
                    }
                }

                (string buildtag, string filename) = GetAdequateNameFromImageIndexes(windowsImageIndexes);

                filename = buildtag + "_" + filename;
                filename = filename.ToLower();

                string fileextension = file.Split(@".")[^1];
                string label = "";

                if (fileextension == "iso" || fileextension == "mdf")
                {
                    SetupHelper.SetupComplete();

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
                    catch
                    {
                    }
                }

                string dst = filename + "." + fileextension;
                if (!string.IsNullOrEmpty(label))
                {
                    dst = filename + "-" + label + "." + fileextension;
                }

                dst = string.Join(@"\", file.Split(@"\")[..^1]) + @"\" + dst;

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

            IEnumerable<string> ifiles = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories);

            foreach (string file in ifiles)
            {
                FileItem[] result;

                switch (file.Split(".")[^1].ToLower())
                {
                    case "iso":
                        {
                            result = MediaHandler.IdentifyWindowsFromIso(file, false, false);
                            break;
                        }
                    case "mdf":
                        {
                            result = MediaHandler.IdentifyWindowsFromMdf(file, false, false);
                            break;
                        }
                    case "wim":
                    case "esd":
                        {
                            WindowsImageIndex[] images = MediaHandler.IdentifyWindowsFromWim(File.OpenRead(file), true);
                            if (images.Length > 0)
                            {
                                result = new FileItem[] { new() { Metadata = new MetaData { WindowsImageIndexes = images } } };
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

                if (!result.Any(x =>
                        x.Metadata != null && x.Metadata.WindowsImageIndexes != null &&
                        x.Metadata.WindowsImageIndexes.Length != 0))
                {
                    continue;
                }

                FileItem[] files = result.Where(x =>
                    x.Metadata != null && x.Metadata.WindowsImageIndexes != null &&
                    x.Metadata.WindowsImageIndexes.Length != 0).ToArray();

                WindowsImageIndex[] windowsImageIndexes = files[0].Metadata.WindowsImageIndexes;

                if (files.Any(x => x.Location.EndsWith("install.wim", StringComparison.OrdinalIgnoreCase)))
                {
                    windowsImageIndexes = files.First(x => x.Location.EndsWith("install.wim", StringComparison.OrdinalIgnoreCase)).Metadata
                        .WindowsImageIndexes;
                }
                else if (files.Any(
                             x => x.Location.EndsWith("txtsetup.sif", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (FileItem vfile in files.Where(x =>
                                 x.Location.EndsWith("txtsetup.sif", StringComparison.OrdinalIgnoreCase)))
                    {
                        windowsImageIndexes = windowsImageIndexes.Union(vfile.Metadata.WindowsImageIndexes).ToArray();
                    }
                }

                (string buildtag, string filename) = GetAdequateNameFromImageIndexes(windowsImageIndexes);

                string label = "";

                SetupHelper.SetupComplete();

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
                catch
                {
                }

                ;

                label = label.Trim('\0');

                string dst = filename;
                if (!string.IsNullOrEmpty(label))
                {
                    dst = filename + "-" + label;
                }

                string middlepath = Path.Join(output, buildtag);
                if (!Directory.Exists(middlepath))
                {
                    Directory.CreateDirectory(middlepath);
                }

                string finalpath = Path.Join(middlepath, dst);
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

            XmlSerializer deserializer = new(typeof(WindowsImageIndex[]));
            TextReader reader = new StreamReader(opts.WindowsIndex);
            object obj = deserializer.Deserialize(reader);
            WindowsImageIndex[] xmlData = (WindowsImageIndex[])obj;
            reader.Close();

            (string buildtag, string filename) = GetAdequateNameFromImageIndexes(xmlData);

            filename = buildtag + "_" + filename;
            filename = filename.ToLower();

            string fileextension = opts.Media.Split(@".")[^1];
            string label = "";

            if (fileextension == "iso" || fileextension == "mdf")
            {
                SetupHelper.SetupComplete();

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
                catch
                {
                }

                ;
            }

            string dst = filename + "." + fileextension;
            if (!string.IsNullOrEmpty(label))
            {
                dst = filename + "-" + label + "." + fileextension;
            }

            dst = Path.Combine(Path.GetDirectoryName(opts.WindowsIndex), ReplaceInvalidChars(dst));

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

                if (opts.WindowsIndex.Contains(opts.Media))
                {
                    File.Move(opts.WindowsIndex, opts.WindowsIndex.Replace(opts.Media, dst));
                }

                string sha1File = $"{opts.Media}.sha1";
                if (File.Exists(sha1File))
                {
                    Console.WriteLine("Update SHA-1 Checksum File");
                    string sha1Content = File.ReadAllText(sha1File);
                    sha1Content = sha1Content.Replace(opts.Media, Path.GetFileName(dst));
                    sha1Content = sha1Content.Replace(Path.GetFileName(opts.Media), Path.GetFileName(dst));
                    File.WriteAllText($"{dst}.sha1", sha1Content);
                    File.Delete(sha1File);
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

            SetupHelper.SetupComplete();

            string file = opts.Media;
            string extension = file.Split(".")[^1];

            switch (extension.ToLower())
            {
                case "vhd":
                    {
                        MediaHandler.IdentifyWindowsFromVhd(file);
                        break;
                    }
                case "vhdx":
                    {
                        MediaHandler.IdentifyWindowsFromVhdx(file);
                        break;
                    }
                case "iso":
                    {
                        FileItem[] result = MediaHandler.IdentifyWindowsFromIso(file, opts.Deep, true);

                        XmlSerializer xsSubmit = new(typeof(FileItem[]));
                        string xml;

                        using (StringWriter sww = new())
                        {
                            XmlWriterSettings settings = new()
                            {
                                Indent = true,
                                IndentChars = "     ",
                                NewLineOnAttributes = false,
                                OmitXmlDeclaration = true
                            };

                            using XmlWriter writer = XmlWriter.Create(sww, settings);
                            xsSubmit.Serialize(writer, result);
                            xml = sww.ToString();
                        }

                        File.WriteAllText(opts.Output, xml);

                        break;
                    }
                case "mdf":
                    {
                        FileItem[] result = MediaHandler.IdentifyWindowsFromMdf(file, opts.Deep, true);

                        XmlSerializer xsSubmit = new(typeof(FileItem[]));
                        string xml;

                        using (StringWriter sww = new())
                        {
                            using XmlWriter writer = XmlWriter.Create(sww);
                            writer.Settings.Indent = true;
                            writer.Settings.IndentChars = "     ";
                            writer.Settings.NewLineOnAttributes = false;
                            writer.Settings.OmitXmlDeclaration = true;

                            xsSubmit.Serialize(writer, result);
                            xml = sww.ToString();
                        }

                        File.WriteAllText(opts.Output, xml);

                        break;
                    }
                case "vmdk":
                    {
                        MediaHandler.IdentifyWindowsFromVmdk(file);
                        break;
                    }
                case "vdi":
                    {
                        MediaHandler.IdentifyWindowsFromVdi(file);
                        break;
                    }
                case "wim":
                case "esd":
                    {
                        MediaHandler.IdentifyWindowsFromWim(new FileStream(file, FileMode.Open, FileAccess.Read), true);
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

            SetupHelper.SetupComplete();

            string file = opts.Media;
            if (string.IsNullOrEmpty(opts.Output))
            {
                opts.Output = $"{file}.meta_id.xml";
            }

            string extension = file.Split(".")[^1];

            switch (extension.ToLower())
            {
                case "vhd":
                    {
                        MediaHandler.IdentifyWindowsFromVhd(file);
                        break;
                    }
                case "vhdx":
                    {
                        MediaHandler.IdentifyWindowsFromVhdx(file);
                        break;
                    }
                case "iso":
                    {
                        FileItem[] result = MediaHandler.IdentifyWindowsFromIso(file, false, false);

                        if (result.Any(x => x.Location.ToLower() == @"\sources\install.wim"))
                        {
                            FileItem wimtag = result.First(x => x.Location.ToLower() == @"\sources\install.wim");

                            XmlSerializer xsSubmit = new(typeof(WindowsImageIndex[]));
                            string xml;

                            using (StringWriter sww = new())
                            {
                                XmlWriterSettings settings = new()
                                {
                                    Indent = true,
                                    IndentChars = "     ",
                                    NewLineOnAttributes = false,
                                    OmitXmlDeclaration = true
                                };

                                using XmlWriter writer = XmlWriter.Create(sww, settings);
                                xsSubmit.Serialize(writer, wimtag.Metadata.WindowsImageIndexes);
                                xml = sww.ToString();
                            }

                            File.WriteAllText(opts.Output, xml);
                        }
                        else if (result.Any(x => x.Location.ToLower() == @"\sources\install.esd"))
                        {
                            FileItem wimtag = result.First(x => x.Location.ToLower() == @"\sources\install.esd");

                            XmlSerializer xsSubmit = new(typeof(WindowsImageIndex[]));
                            string xml;

                            using (StringWriter sww = new())
                            {
                                XmlWriterSettings settings = new()
                                {
                                    Indent = true,
                                    IndentChars = "     ",
                                    NewLineOnAttributes = false,
                                    OmitXmlDeclaration = true
                                };

                                using XmlWriter writer = XmlWriter.Create(sww, settings);
                                xsSubmit.Serialize(writer, wimtag.Metadata.WindowsImageIndexes);
                                xml = sww.ToString();
                            }

                            File.WriteAllText(opts.Output, xml);
                        }
                        else if (result.Any(x => x.Location.ToLower() == @"\sources\boot.wim"))
                        {
                            FileItem wimtag = result.First(x => x.Location.ToLower() == @"\sources\boot.wim");

                            XmlSerializer xsSubmit = new(typeof(WindowsImageIndex[]));
                            string xml;

                            using (StringWriter sww = new())
                            {
                                XmlWriterSettings settings = new()
                                {
                                    Indent = true,
                                    IndentChars = "     ",
                                    NewLineOnAttributes = false,
                                    OmitXmlDeclaration = true
                                };

                                using XmlWriter writer = XmlWriter.Create(sww, settings);
                                xsSubmit.Serialize(writer, wimtag.Metadata.WindowsImageIndexes);
                                xml = sww.ToString();
                            }

                            File.WriteAllText(opts.Output, xml);
                        }
                        else if (result.Any(x => x.Location.ToLower().EndsWith(@"\txtsetup.sif")))
                        {
                            IEnumerable<WindowsImageIndex[]> txtsetups = result.Where(x => x.Location.ToLower().EndsWith(@"\txtsetup.sif"))
                                .Select(x => x.Metadata.WindowsImageIndexes);

                            WindowsImageIndex[] indexes = null;
                            foreach (WindowsImageIndex[] arr in txtsetups)
                            {
                                if (indexes == null)
                                {
                                    indexes = arr;
                                }
                                else
                                {
                                    List<WindowsImageIndex> tmplist = indexes.ToList();
                                    tmplist.AddRange(arr);
                                    indexes = tmplist.ToArray();
                                }
                            }

                            XmlSerializer xsSubmit = new(typeof(WindowsImageIndex[]));
                            string xml;

                            using (StringWriter sww = new())
                            {
                                XmlWriterSettings settings = new()
                                {
                                    Indent = true,
                                    IndentChars = "     ",
                                    NewLineOnAttributes = false,
                                    OmitXmlDeclaration = true
                                };

                                using XmlWriter writer = XmlWriter.Create(sww, settings);
                                xsSubmit.Serialize(writer, indexes);
                                xml = sww.ToString();
                            }

                            File.WriteAllText(opts.Output, xml);
                        }

                        break;
                    }
                case "mdf":
                    {
                        FileItem[] result = MediaHandler.IdentifyWindowsFromMdf(file, false, false);

                        if (result.Any(x => x.Location.ToLower() == @"\sources\install.wim"))
                        {
                            FileItem wimtag = result.First(x => x.Location.ToLower() == @"\sources\install.wim");

                            XmlSerializer xsSubmit = new(typeof(WindowsImageIndex[]));
                            string xml;

                            using (StringWriter sww = new())
                            {
                                XmlWriterSettings settings = new()
                                {
                                    Indent = true,
                                    IndentChars = "     ",
                                    NewLineOnAttributes = false,
                                    OmitXmlDeclaration = true
                                };

                                using XmlWriter writer = XmlWriter.Create(sww, settings);
                                xsSubmit.Serialize(writer, wimtag.Metadata.WindowsImageIndexes);
                                xml = sww.ToString();
                            }

                            File.WriteAllText(opts.Output, xml);
                        }

                        if (result.Any(x => x.Location.ToLower() == @"\sources\install.esd"))
                        {
                            FileItem wimtag = result.First(x => x.Location.ToLower() == @"\sources\install.esd");

                            XmlSerializer xsSubmit = new(typeof(WindowsImageIndex[]));
                            string xml;

                            using (StringWriter sww = new())
                            {
                                XmlWriterSettings settings = new()
                                {
                                    Indent = true,
                                    IndentChars = "     ",
                                    NewLineOnAttributes = false,
                                    OmitXmlDeclaration = true
                                };

                                using XmlWriter writer = XmlWriter.Create(sww, settings);
                                xsSubmit.Serialize(writer, wimtag.Metadata.WindowsImageIndexes);
                                xml = sww.ToString();
                            }

                            File.WriteAllText(opts.Output, xml);
                        }

                        if (result.Any(x => x.Location.ToLower().EndsWith(@"\txtsetup.sif")))
                        {
                            IEnumerable<WindowsImageIndex[]> txtsetups = result.Where(x => x.Location.ToLower().EndsWith(@"\txtsetup.sif"))
                                .Select(x => x.Metadata.WindowsImageIndexes);

                            WindowsImageIndex[] indexes = null;
                            foreach (WindowsImageIndex[] arr in txtsetups)
                            {
                                if (indexes == null)
                                {
                                    indexes = arr;
                                }
                                else
                                {
                                    List<WindowsImageIndex> tmplist = indexes.ToList();
                                    tmplist.AddRange(arr);
                                    indexes = tmplist.ToArray();
                                }
                            }

                            XmlSerializer xsSubmit = new(typeof(WindowsImageIndex[]));
                            string xml;

                            using (StringWriter sww = new())
                            {
                                XmlWriterSettings settings = new()
                                {
                                    Indent = true,
                                    IndentChars = "     ",
                                    NewLineOnAttributes = false,
                                    OmitXmlDeclaration = true
                                };

                                using XmlWriter writer = XmlWriter.Create(sww, settings);
                                xsSubmit.Serialize(writer, indexes);
                                xml = sww.ToString();
                            }

                            File.WriteAllText(opts.Output, xml);
                        }

                        break;
                    }
                case "vmdk":
                    {
                        MediaHandler.IdentifyWindowsFromVmdk(file);
                        break;
                    }
                case "vdi":
                    {
                        MediaHandler.IdentifyWindowsFromVdi(file);
                        break;
                    }
                case "wim":
                case "esd":
                    {
                        WindowsImageIndex[] wimindexes =
                            MediaHandler.IdentifyWindowsFromWim(new FileStream(file, FileMode.Open, FileAccess.Read),
                                false);

                        XmlSerializer xsSubmit = new(typeof(WindowsImageIndex[]));
                        string xml;

                        using (StringWriter sww = new())
                        {
                            XmlWriterSettings settings = new()
                            {
                                Indent = true,
                                IndentChars = "     ",
                                NewLineOnAttributes = false,
                                OmitXmlDeclaration = true
                            };

                            using XmlWriter writer = XmlWriter.Create(sww, settings);
                            xsSubmit.Serialize(writer, wimindexes);
                            xml = sww.ToString();
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
            Console.WriteLine("Windows Build Identifier (WBI)");
            Console.WriteLine("Gustave Monce (@gus33000) (c) 2018-2021");
            Console.WriteLine("Thomas Hounsell (c) 2021-2022");
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

        [Verb("diff",
            HelpText =
                "Diff two builds based on their meta_index.xml files. Note: index files must have been generated with the Deep option.")]
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

            [Option('d', "deep", Required = false, Default = false,
                HelpText =
                    "Perform a deep scan. A deep scan will recursively index files inside of various recognized container types such as wims, isos and etc...")]
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
    }
}