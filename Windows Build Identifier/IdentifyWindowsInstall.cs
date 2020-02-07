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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WindowsBuildIdentifier
{
    public class IdentifyWindowsInstall
    {
        public enum BuildType
        {
            fre,
            chk
        }

        public enum Type
        {
            Client,
            Server,
            ServerV
        }

        public enum Licensing
        {
            Retail,
            OEM,
            Volume
        }

        public class Report
        {
            public ulong MajorVersion;
            public ulong MinorVersion;
            public ulong BuildNumber;
            public ulong DeltaVersion;
            public string BranchName;
            public string CompileDate;
            public string Tag;
            public MachineType Architecture;
            public BuildType BuildType;
            public HashSet<Type> Type = new HashSet<Type>();
            public string Sku;
            public string[] Editions;
            public Licensing Licensing;
            public string[] LanguageCodes;
        }

        public enum MachineType : ushort
        {
            unknown = 0x0,
            am33 = 0x1d3,
            amd64 = 0x8664,
            arm = 0x1c0,
            arm64 = 0xaa64,
            woa = 0x1c4,
            ebc = 0xebc,
            x86 = 0x14c,
            ia64 = 0x200,
            m32r = 0x9041,
            mips16 = 0x266,
            mipsfpu = 0x366,
            mipsfpu16 = 0x466,
            powerpc = 0x1f0,
            powerpcfp = 0x1f1,
            r4000 = 0x166,
            sh3 = 0x1a2,
            sh3dsp = 0x1a3,
            sh4 = 0x1a6,
            sh5 = 0x1a8,
            thumb = 0x1c2,
            wcemipsv2 = 0x169,
        }

        private class VersionInfo1
        {
            public ulong MajorVersion;
            public ulong MinorVersion;
            public ulong BuildNumber;
            public ulong DeltaVersion;
            public string BranchName;
            public string CompileDate;
            public MachineType Architecture;
            public BuildType BuildType;
        }

        private class VersionInfo2
        {
            public ulong MajorVersion;
            public ulong MinorVersion;
            public ulong BuildNumber;
            public ulong DeltaVersion;
            public string BranchName;
            public string CompileDate;
            public string Tag;
            public Licensing Licensing;
            public string[] LanguageCodes;
        }

        public static Report IdentifyWindows(WindowsInstallProviderInterface installProvider)
        {
            Report report = new Report();

            var fileentries = installProvider.GetFileSystemEntries();

            //
            // We need a few files from the install to gather enough information
            // These files are:
            //
            // - \ntkrnlmp.exe
            // or \ntoskrnl.exe
            // - windows\system32\config\software
            // - windows\system32\config\system
            //

            string kernelPath = "";
            string softwareHivePath = "";
            string systemHivePath = "";

            var kernelEntry = fileentries.FirstOrDefault(x => x.EndsWith(@"\ntkrnlmp.exe", StringComparison.InvariantCultureIgnoreCase))
                ?? fileentries.FirstOrDefault(x => x.EndsWith(@"\ntoskrnl.exe", StringComparison.InvariantCultureIgnoreCase));
            if (kernelEntry != null)
            {
                kernelPath = installProvider.ExpandFile(kernelEntry);
            }

            var softwareHiveEntry = fileentries.FirstOrDefault(x => x.Equals(@"windows\system32\config\software", StringComparison.InvariantCultureIgnoreCase));
            if (softwareHiveEntry != null)
            {
                softwareHivePath = installProvider.ExpandFile(softwareHiveEntry);
            }

            var systemHiveEntry = fileentries.FirstOrDefault(x => x.Equals(@"windows\system32\config\system", StringComparison.InvariantCultureIgnoreCase));
            if (systemHiveEntry != null)
            {
                systemHivePath = installProvider.ExpandFile(systemHiveEntry);
            }

            bool IsUnstaged = !fileentries.Any(x => x.Contains(@"system32\ntoskrnl.exe", StringComparison.InvariantCultureIgnoreCase)) &&
                !fileentries.Any(x => x.Contains(@"system32\ntkrnlmp.exe", StringComparison.InvariantCultureIgnoreCase));

            Console.WriteLine("Extracting version information from the image 1");
            VersionInfo1 info = ExtractVersionInfo(kernelPath);

            File.Delete(kernelPath);

            report.Architecture = info.Architecture;
            report.BuildType = info.BuildType;

            if (IsUnstaged)
            {
                report.Sku = "Unstaged";

                Console.WriteLine("Image detected as unstaged, gathering target editions available in the image");

                // parse editions
                report.Editions = GatherUnstagedEditions(installProvider);
            }
            else
            {
                Console.WriteLine("Extracting additional edition information");
                report.Sku = ExtractEditionInfo(systemHivePath);
            }

            Console.WriteLine("Extracting version information from the image 2");
            VersionInfo2 info2 = ExtractVersionInfo2(softwareHivePath, systemHivePath);

            File.Delete(softwareHivePath);
            File.Delete(systemHivePath);

            report.Tag = info2.Tag;
            report.Licensing = info2.Licensing;
            report.LanguageCodes = info2.LanguageCodes;

            WindowsVersion version1 = new WindowsVersion
            {
                MajorVersion = info.MajorVersion,
                MinorVersion = info.MinorVersion,
                BuildNumber = info.BuildNumber,
                DeltaVersion = info.DeltaVersion,
                BranchName = info.BranchName,
                CompileDate = info.CompileDate
            };

            WindowsVersion version2 = new WindowsVersion
            {
                MajorVersion = info2.MajorVersion,
                MinorVersion = info2.MinorVersion,
                BuildNumber = info2.BuildNumber,
                DeltaVersion = info2.DeltaVersion,
                BranchName = info2.BranchName,
                CompileDate = info2.CompileDate
            };

            WindowsVersion correctVersion = GetGreaterVersion(version1, version2);

            report.MajorVersion = correctVersion.MajorVersion;
            report.MinorVersion = correctVersion.MinorVersion;
            report.BuildNumber = correctVersion.BuildNumber;
            report.DeltaVersion = correctVersion.DeltaVersion;
            report.BranchName = correctVersion.BranchName;
            report.CompileDate = correctVersion.CompileDate;

            if (report.BuildNumber > 2195)
            {
                if (report.Sku.Equals("personal", StringComparison.InvariantCultureIgnoreCase))
                {
                    report.Sku = "Home";
                }
            }

            if (report.BuildNumber >= 1911)
            {
                if (report.Sku.Equals("workstation", StringComparison.InvariantCultureIgnoreCase))
                {
                    report.Sku = "Professional";
                }
            }

            if (IsUnstaged && report.Editions != null)
            {
                foreach (var skuunstaged in report.Editions)
                {
                    if ((skuunstaged.Contains("server", StringComparison.InvariantCultureIgnoreCase) && skuunstaged.EndsWith("hyperv", StringComparison.InvariantCultureIgnoreCase)) ||
                        (skuunstaged.Contains("server", StringComparison.InvariantCultureIgnoreCase) && skuunstaged.EndsWith("v", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        if (!report.Type.Contains(Type.ServerV))
                        {
                            report.Type.Add(Type.ServerV);
                        }
                    }
                    else if (skuunstaged.Contains("server", StringComparison.InvariantCultureIgnoreCase))
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
            }
            else if (!string.IsNullOrEmpty(report.Sku))
            {
                if (report.Sku.Equals("ads", StringComparison.InvariantCultureIgnoreCase))
                {
                    report.Sku = "AdvancedServer";
                }

                if (report.Sku.Equals("pro", StringComparison.InvariantCultureIgnoreCase))
                {
                    report.Sku = "Professional";
                }

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

            return report;
        }

        private static MachineType GetMachineTypeFromFile(Stream fs)
        {
            BinaryReader br = new BinaryReader(fs);
            fs.Seek(0x3c, SeekOrigin.Begin);
            Int32 peOffset = br.ReadInt32();
            fs.Seek(peOffset, SeekOrigin.Begin);
            UInt32 peHead = br.ReadUInt32();
            if (peHead != 0x00004550) // "PE\0\0", little-endian
            {
                throw new Exception("Can't find PE header");
            }

            MachineType machineType = (MachineType)br.ReadUInt16();
            br.Close();
            fs.Close();
            return machineType;
        }

        private static VersionInfo1 ParseBuildString(VersionInfo1 verinfo, string BuildString)
        {
            if (BuildString.Contains(" built by: ") && BuildString.Contains(" at: ") && BuildString.Count(x => x == '.') == 3)
            {
                // Early version
                var splitparts = BuildString.Split(' ');
                var rawversion = splitparts[0];
                var branch = splitparts[3];
                var compiledate = splitparts[5];

                var splitver = rawversion.Split('.');

                verinfo.MajorVersion = ulong.Parse(splitver[0]);
                verinfo.MinorVersion = ulong.Parse(splitver[1]);
                verinfo.BuildNumber = ulong.Parse(splitver[2]);
                verinfo.DeltaVersion = ulong.Parse(splitver[3]);

                verinfo.BranchName = branch;
                verinfo.CompileDate = compiledate;
            }
            else if (BuildString.Contains(" (") && BuildString.Contains(")") && BuildString.Count(x => x == '.') >= 4)
            {
                if (BuildString.Contains(" (WinBuild."))
                {
                    // MS new thing

                    var splitparts = BuildString.Split(' ');
                    var rawversion = splitparts[0];

                    var splitver = rawversion.Split('.');

                    verinfo.MajorVersion = ulong.Parse(splitver[0]);
                    verinfo.MinorVersion = ulong.Parse(splitver[1]);
                    verinfo.BuildNumber = ulong.Parse(splitver[2]);
                    verinfo.DeltaVersion = ulong.Parse(splitver[3]);
                }
                else
                {
                    // Normal thing

                    var splitparts = BuildString.Split(' ');
                    var rawversion = splitparts[0];
                    var rawdetails = splitparts[1];

                    var splitdetails = rawdetails.TrimStart('(').TrimEnd(')').Split('.');

                    var branch = splitdetails[0];
                    var compiledate = splitdetails[1];

                    var splitver = rawversion.Split('.');

                    verinfo.MajorVersion = ulong.Parse(splitver[0]);
                    verinfo.MinorVersion = ulong.Parse(splitver[1]);
                    verinfo.BuildNumber = ulong.Parse(splitver[2]);
                    verinfo.DeltaVersion = ulong.Parse(splitver[3]);

                    verinfo.BranchName = branch;
                    verinfo.CompileDate = compiledate;
                }
            }


            return verinfo;
        }

        private static VersionInfo1 ExtractVersionInfo(string kernelPath)
        {
            VersionInfo1 result = new VersionInfo1();

            FileVersionInfo info = FileVersionInfo.GetVersionInfo(kernelPath);

            result.Architecture = GetMachineTypeFromFile(new FileStream(kernelPath, FileMode.Open));

            result = ParseBuildString(result, info.FileVersion);
            result.BuildType = info.IsDebug ? BuildType.chk : BuildType.fre;

            return result;
        }

        private class WindowsVersion
        {
            public ulong MajorVersion;
            public ulong MinorVersion;
            public ulong BuildNumber;
            public ulong DeltaVersion;
            public string BranchName;
            public string CompileDate;
        }

        private static VersionInfo2 ExtractVersionInfo2(string softwareHivePath, string systemHivePath)
        {
            VersionInfo2 result = new VersionInfo2();

            using (var hiveStream = new FileStream(softwareHivePath, FileMode.Open, FileAccess.Read))
            using (DiscUtils.Registry.RegistryHive hive = new DiscUtils.Registry.RegistryHive(hiveStream))
            {
                try
                {
                    DiscUtils.Registry.RegistryKey subkey = hive.Root.OpenSubKey(@"Microsoft\Windows NT\CurrentVersion");

                    string buildLab = (string)subkey.GetValue("BuildLab");
                    string buildLabEx = (string)subkey.GetValue("BuildLabEx");

                    string releaseId = (string)subkey.GetValue("ReleaseId");

                    int? UBR = (int?)subkey.GetValue("UBR");
                    int? Major = (int?)subkey.GetValue("CurrentMajorVersionNumber");
                    int? Minor = (int?)subkey.GetValue("CurrentMinorVersionNumber");

                    if (!string.IsNullOrEmpty(buildLab) && buildLab.Count(x => x == '.') == 2)
                    {
                        var splitLab = buildLab.Split('.');

                        result.BranchName = splitLab[1];
                        result.CompileDate = splitLab[2];
                        result.BuildNumber = ulong.Parse(splitLab[0]);
                    }

                    if (!string.IsNullOrEmpty(buildLabEx) && buildLabEx.Count(x => x == '.') == 4)
                    {
                        var splitLabEx = buildLabEx.Split('.');

                        result.BranchName = splitLabEx[3];
                        result.CompileDate = splitLabEx[4];
                        result.DeltaVersion = ulong.Parse(splitLabEx[1]);
                        result.BuildNumber = ulong.Parse(splitLabEx[0]);
                    }

                    if (UBR.HasValue)
                    {
                        result.DeltaVersion = (ulong)UBR.Value;
                    }

                    if (Major.HasValue)
                    {
                        result.MajorVersion = (ulong)Major.Value;
                    }

                    if (Minor.HasValue)
                    {
                        result.MinorVersion = (ulong)Minor.Value;
                    }

                    if (!string.IsNullOrEmpty(releaseId))
                    {
                        result.Tag = releaseId;
                    }
                }
                catch { };

                try
                {
                    string productId = "";

                    DiscUtils.Registry.RegistryKey subkey = hive.Root.OpenSubKey(@"Microsoft\Windows NT\CurrentVersion\DefaultProductKey");
                    if (subkey != null)
                    {
                        productId = (string)subkey.GetValue("ProductId");
                    }
                    else
                    {
                        subkey = hive.Root.OpenSubKey(@"Microsoft\Windows\CurrentVersion");
                        if (subkey != null)
                        {
                            productId = (string)subkey.GetValue("ProductId");
                        }
                    }

                    if (!string.IsNullOrEmpty(productId))
                    {
                        result.Licensing = productId.Contains("OEM") ? Licensing.OEM : Licensing.Retail;
                    }
                }
                catch { };
            }

            using (var hiveStream = new FileStream(systemHivePath, FileMode.Open, FileAccess.Read))
            using (DiscUtils.Registry.RegistryHive hive = new DiscUtils.Registry.RegistryHive(hiveStream))
            {
                try
                {
                    DiscUtils.Registry.RegistryKey subkey = hive.Root.OpenSubKey(@"ControlSet001\Control\MUI\UILanguages");

                    result.LanguageCodes = subkey.GetSubKeyNames();
                }
                catch { };

                try
                {
                    DiscUtils.Registry.RegistryKey subkey = hive.Root.OpenSubKey(@"ControlSet001\Control\Nls\Language");

                    string langid = (string)subkey.GetValue("Default");

                    var cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);

                    if (cultures.Any(x => x.LCID == int.Parse(langid, NumberStyles.HexNumber, CultureInfo.CurrentCulture)))
                    {
                        var name = cultures.First(x => x.LCID == int.Parse(langid, NumberStyles.HexNumber, CultureInfo.CurrentCulture)).Name;
                        if (result.LanguageCodes == null ||
                            result.LanguageCodes != null && !result.LanguageCodes.Any(x => x.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            if (result.LanguageCodes == null)
                            {
                                result.LanguageCodes = new string[] { name };
                            }
                            else
                            {
                                result.LanguageCodes = result.LanguageCodes.Append(name).ToArray();
                            }
                        }
                    }
                }
                catch { };
            }

            return result;
        }

        private static WindowsVersion GetGreaterVersion(WindowsVersion version1, WindowsVersion version2)
        {
            if (version1.MajorVersion != version2.MajorVersion)
            {
                if (version1.MajorVersion > version2.MajorVersion)
                {
                    return version1;
                }
                return version2;
            }

            if (version1.MinorVersion != version2.MinorVersion)
            {
                if (version1.MinorVersion > version2.MinorVersion)
                {
                    return version1;
                }
                return version2;
            }

            if (version1.BuildNumber != version2.BuildNumber)
            {
                if (version1.BuildNumber > version2.BuildNumber)
                {
                    return version1;
                }
                return version2;
            }

            if (version1.DeltaVersion != version2.DeltaVersion)
            {
                if (version1.DeltaVersion > version2.DeltaVersion)
                {
                    return version1;
                }
                return version2;
            }

            if (version1.CompileDate != version2.CompileDate)
            {
                if (string.IsNullOrEmpty(version1.CompileDate))
                {
                    return version2;
                }
                if (string.IsNullOrEmpty(version2.CompileDate))
                {
                    return version1;
                }

                CultureInfo provider = CultureInfo.InvariantCulture;

                string format = "yyMMDD-HHmm";

                DateTime date1 = DateTime.ParseExact(version1.CompileDate, format, provider);
                DateTime date2 = DateTime.ParseExact(version2.CompileDate, format, provider);

                if (date1 > date2)
                {
                    return version1;
                }
                return version2;
            }

            return version1;
        }

        private static string ExtractEditionInfo(string systemHivePath)
        {
            string result = "";

            using (var hiveStream = new FileStream(systemHivePath, FileMode.Open, FileAccess.Read))
            using (DiscUtils.Registry.RegistryHive hive = new DiscUtils.Registry.RegistryHive(hiveStream))
            {
                try
                {
                    DiscUtils.Registry.RegistryKey subkey = hive.Root.OpenSubKey(@"ControlSet001\Control\ProductOptions");

                    string[] list = (string[])subkey.GetValue("ProductSuite");

                    result = list.Length > 0 ? list[0] : "";
                    if (string.IsNullOrEmpty(result))
                    {
                        result = "Workstation";
                    }

                    result = result.Replace(" ", "");

                    switch (result.ToLower())
                    {
                        case "enterprise":
                        case "backoffice":
                        case "datacenter":
                        case "securityappliance":
                            {
                                result = result + "Server";
                                break;
                            }
                        case "whserver":
                            {
                                result = "HomeServer";
                                break;
                            }
                        case "smallbusiness":
                            {
                                result = "SmallBusinessServer";
                                break;
                            }
                        case "smallbusiness(restricted)":
                            {
                                result = "SmallBusinessServerRestricted";
                                break;
                            }
                        case "blade":
                            {
                                result = "ServerWeb";
                                break;
                            }
                        case "embeddednt":
                            {
                                result = "Embedded";
                                break;
                            }
                        case "embedded(restricted)":
                            {
                                result = "EmbeddedRestricted";
                                break;
                            }
                    }
                }
                catch { };
            }

            return result;
        }

        private static string[] GatherUnstagedEditions(WindowsInstallProviderInterface installProvider)
        {
            SortedSet<string> report = new SortedSet<string>();
            foreach (var x in installProvider.GetFileSystemEntries())
            {
                bool foundsomething = false;

                var filename = x.ToLower();
                if (filename.StartsWith("packages") &&
                    filename.Contains("sku") &&
                    filename.IndexOf("sku") > ("packages").Length &&
                    filename.Contains("security-licensing-slc-component-sku") &&
                    filename.Contains("pl") &&
                    filename.Contains("xrm"))
                {
                    foundsomething = true;

                    var split = x.Split('\\');
                    var packagename = split[^2];
                    var lastpart = packagename.Split('-').Last();
                    var skufound = lastpart.Split('_')[0];

                    if (!report.Contains(skufound))
                    {
                        report.Add(skufound);
                    }
                }

                if (!foundsomething)
                {
                    foundsomething = false;
                    var filenamelast = filename.Split('\\').Last();

                    if (filename.StartsWith("packages") &&
                        filenamelast.StartsWith("update") &&
                        filenamelast.Contains("update.mum"))
                    {
                        foundsomething = true;

                        var split = x.Split('\\');
                        var packagename = split[^2];

                        if (!report.Contains(packagename))
                        {
                            report.Add(packagename);
                        }
                    }

                    if (!foundsomething)
                    {
                        if (filename.StartsWith("packages") &&
                        filenamelast.StartsWith("shellbrd") &&
                        filenamelast.EndsWith("dll"))
                        {
                            var split = x.Split('\\');
                            var packagename = split[^2];
                            var splitpkg = packagename.Split('-');
                            var lastpart = splitpkg.Last();
                            var skufound = lastpart.Split('_')[0];
                            if (skufound.Equals("edition", StringComparison.InvariantCultureIgnoreCase))
                            {
                                skufound = splitpkg[^2];
                            }

                            if (!report.Contains(skufound))
                            {
                                report.Add(skufound);
                            }
                        }
                    }
                }
            }

            return report.ToArray();
        }

        public static void DisplayReport(Report report)
        {
            string typedisp = report.Type != null
                ? string.Join(", ", report.Type.Select(e => e.ToString()))
                : "";

            string editiondisp = report.Editions != null
                ? string.Join(", ", report.Editions.Select(e => e.ToString()))
                : "";

            string langdisp = report.LanguageCodes != null
                ? string.Join(", ", report.LanguageCodes.Select(e => e.ToString()))
                : "";

            Console.WriteLine();
            Console.WriteLine("MajorVersion : " + report.MajorVersion);
            Console.WriteLine("MinorVersion : " + report.MinorVersion);
            Console.WriteLine("BuildNumber  : " + report.BuildNumber);
            Console.WriteLine("DeltaVersion : " + report.DeltaVersion);
            Console.WriteLine("BranchName   : " + report.BranchName);
            Console.WriteLine("CompileDate  : " + report.CompileDate);
            Console.WriteLine("Tag          : " + report.Tag);
            Console.WriteLine("Architecture : " + report.Architecture);
            Console.WriteLine("BuildType    : " + report.BuildType);
            Console.WriteLine("Type         : " + typedisp);
            Console.WriteLine("Sku          : " + report.Sku);
            Console.WriteLine("Editions     : " + editiondisp);
            Console.WriteLine("Licensing    : " + report.Licensing);
            Console.WriteLine("LanguageCodes: " + langdisp);
            Console.WriteLine();
        }

    }
}
