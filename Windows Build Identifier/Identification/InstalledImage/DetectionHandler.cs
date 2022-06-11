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

using DiscUtils.Registry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using WindowsBuildIdentifier.Interfaces;

namespace WindowsBuildIdentifier.Identification.InstalledImage
{
    public class DetectionHandler
    {
        public static WindowsImage IdentifyWindowsNT(IWindowsInstallProviderInterface installProvider)
        {
            WindowsImage report = new();

            string[] fileentries = installProvider.GetFileSystemEntries();

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
            string hvPath = "";
            string shell32Path = "";
            string softwareHivePath = "";
            string systemHivePath = "";
            string userPath = "";
            string virtualEditionsPath = "";

            string kernelEntry = fileentries
                .Where(x =>
                    (x.EndsWith(@"\ntkrnlmp.exe", StringComparison.OrdinalIgnoreCase) ||
                     x.EndsWith(@"\ntoskrnl.exe", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(x => x.Contains("System32", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
            if (kernelEntry != null)
            {
                kernelPath = installProvider.ExpandFile(kernelEntry);
            }

            string hvEntry = fileentries.FirstOrDefault(x =>
                (x.EndsWith(@"\hvax64.exe", StringComparison.OrdinalIgnoreCase) // AMD64
                 || x.EndsWith(@"\hvix64.exe", StringComparison.OrdinalIgnoreCase) // Intel64
                 || x.EndsWith(@"\hvaa64.exe", StringComparison.OrdinalIgnoreCase)) // ARM64
                && x.Contains("System32", StringComparison.OrdinalIgnoreCase));
            if (hvEntry != null)
            {
                hvPath = installProvider.ExpandFile(hvEntry);
            }

            string shell32Entry = fileentries.FirstOrDefault(x =>
                x.EndsWith(@"system32\shell32.dll", StringComparison.OrdinalIgnoreCase));
            if (shell32Entry != null)
            {
                shell32Path = installProvider.ExpandFile(shell32Entry);
            }

            string softwareHiveEntry = fileentries.FirstOrDefault(x =>
                x.EndsWith(@"system32\config\software", StringComparison.OrdinalIgnoreCase));
            if (softwareHiveEntry != null)
            {
                softwareHivePath = installProvider.ExpandFile(softwareHiveEntry);
            }

            string systemHiveEntry = fileentries.FirstOrDefault(x =>
                x.EndsWith(@"system32\config\system", StringComparison.OrdinalIgnoreCase));
            if (systemHiveEntry != null)
            {
                systemHivePath = installProvider.ExpandFile(systemHiveEntry);
            }

            string userEntry = fileentries.FirstOrDefault(x =>
                x.EndsWith(@"\system32\user.exe", StringComparison.OrdinalIgnoreCase));
            if (userEntry != null)
            {
                userPath = installProvider.ExpandFile(userEntry);
            }

            string virtualEditionsEntry = fileentries.FirstOrDefault(x =>
                x.EndsWith(@"\Editions\EditionMappings.xml", StringComparison.OrdinalIgnoreCase));
            if (virtualEditionsEntry != null)
            {
                virtualEditionsPath = installProvider.ExpandFile(virtualEditionsEntry);
            }

            #region Version Gathering

            VersionInfo1 info = new();
            VersionInfo2 info2 = new();
            VersionInfo1 info3 = new();

            if (!string.IsNullOrEmpty(kernelPath))
            {
                Console.WriteLine("Extracting version information from the image 1");
                info = ExtractVersionInfo(kernelPath);

                report.Architecture = info.Architecture;
                report.BuildType = info.BuildType;

                if (report.Architecture == MachineType.x86)
                {
                    string possibleNec98 = fileentries.FirstOrDefault(x =>
                        x.Contains(@"system32\hal98", StringComparison.OrdinalIgnoreCase));
                    if (possibleNec98 != null)
                    {
                        report.Architecture = MachineType.nec98;
                    }
                }
            }

            if (!string.IsNullOrEmpty(softwareHivePath) && !string.IsNullOrEmpty(systemHivePath))
            {
                Console.WriteLine("Extracting version information from the image 2");
                info2 = ExtractVersionInfo2(softwareHivePath, systemHivePath, hvPath);
            }

            if (!string.IsNullOrEmpty(userPath))
            {
                Console.WriteLine("Extracting version information from the image 3");
                info3 = ExtractVersionInfo(userPath);
            }

            report.Tag = info2.Tag;
            report.Licensing = info2.Licensing;
            report.LanguageCodes = info2.LanguageCodes;

            if (!string.IsNullOrEmpty(kernelPath) && (report.LanguageCodes == null || report.LanguageCodes.Length == 0))
            {
                FileVersionInfo infover = FileVersionInfo.GetVersionInfo(kernelPath);

                if (infover.Language == "Language Neutral")
                {
                    infover = FileVersionInfo.GetVersionInfo(shell32Path);
                }

                CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
                foreach (CultureInfo culture in cultures)
                {
                    if (culture.EnglishName == infover.Language)
                    {
                        report.LanguageCodes = new[] { culture.Name };
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(kernelPath))
            {
                File.Delete(kernelPath);
            }

            if (!string.IsNullOrEmpty(hvPath))
            {
                File.Delete(hvPath);
            }

            WindowsVersion correctVersion = Common.GetGreaterVersion(info.Version, info2.Version);
            correctVersion = Common.GetGreaterVersion(correctVersion, info3.Version);

            if (correctVersion != null)
            {
                report.MajorVersion = correctVersion.MajorVersion;
                report.MinorVersion = correctVersion.MinorVersion;
                report.BuildNumber = correctVersion.BuildNumber;
                report.DeltaVersion = correctVersion.DeltaVersion;
                report.BranchName = correctVersion.BranchName;
                report.CompileDate = correctVersion.CompileDate;
            }

            if (report.BuildNumber == 0)
            {
                Console.WriteLine(
                    "Couldn't find Windows version data within this image.");
                // couldn't find a good install within this image
                return null;
            }

            // we have to scan all binaries because early versions of NT
            // do not report the same versions in all binaries
            if (report.BuildNumber < 1130)
            {
                Console.WriteLine(
                    "Determined the reported build number was unreliable. Checking a few more binaries...");
                ulong buildwindow = report.BuildNumber + 50;
                foreach (string binary in installProvider.GetFileSystemEntries())
                {
                    if (binary.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                        binary.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                        binary.EndsWith(".sys", StringComparison.OrdinalIgnoreCase))
                    {
                        string file = "";
                        try
                        {
                            file = installProvider.ExpandFile(binary);
                            VersionInfo1 verinfo = ExtractVersionInfo(file);

                            if (verinfo.Version != null && verinfo.Version.MajorVersion == report.MajorVersion &&
                                verinfo.Version.MinorVersion == report.MinorVersion &&
                                verinfo.Version.BuildNumber < buildwindow &&
                                report.BuildNumber < verinfo.Version.BuildNumber) // allow a gap of 50 builds max
                            {
                                Console.WriteLine("File with newer version found: " + binary + " => " +
                                                  verinfo.Version.BuildNumber);

                                report.BuildNumber = verinfo.Version.BuildNumber;
                                report.DeltaVersion = verinfo.Version.DeltaVersion;
                            }
                        }
                        catch
                        {
                        }
                        finally
                        {
                            if (!string.IsNullOrEmpty(file) && File.Exists(file))
                            {
                                try
                                {
                                    File.Delete(file);
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                }
            }

            #endregion

            #region Edition Gathering

            bool isUnstaged =
                fileentries.Any(x => x.StartsWith(@"packages\", StringComparison.OrdinalIgnoreCase));

            if (isUnstaged)
            {
                report.Sku = "Unstaged";

                Console.WriteLine("Image detected as unstaged, gathering target editions available in the image");

                // parse editions
                report.Editions = GatherUnstagedEditions(installProvider);
            }
            else if (fileentries.Contains(@"InstalledRepository\Windows.config"))
            {
                string packageFile = installProvider.ExpandFile(@"InstalledRepository\Windows.config");

                using FileStream fileStream = File.OpenRead(packageFile);
                using StreamReader streamReader = new(fileStream);
                string line = "";

                while (!line.Contains("configurationIdentity"))
                {
                    line = streamReader.ReadLine();
                }

                string editionPackageName = line.Split("name=\"")[1].Split("\"")[0];

                string editionName = string.Join("", editionPackageName.Split(" ")[1..]);
                if (editionName == "Server")
                {
                    editionName = "ServerStandard";
                }

                report.Sku = editionName;

                File.Delete(packageFile);
            }
            else if (!string.IsNullOrEmpty(systemHivePath))
            {
                Console.WriteLine("Extracting additional edition information");
                (string baseSku, string sku) = ExtractEditionFromRegistry(systemHivePath, softwareHivePath, virtualEditionsPath);
                report.BaseSku = baseSku;
                report.Sku = sku;
            }

            #endregion

            if (!string.IsNullOrEmpty(softwareHivePath))
            {
                File.Delete(softwareHivePath);
            }

            if (!string.IsNullOrEmpty(systemHivePath))
            {
                File.Delete(systemHivePath);
            }

            report = FixSkuNames(report, isUnstaged);

            return report;
        }

        public static WindowsImage FixSkuNames(WindowsImage report, bool isUnstaged)
        {
            if (!string.IsNullOrEmpty(report.Sku))
            {
                if (report.BuildNumber > 2195)
                {
                    if (report.Sku.Equals("personal", StringComparison.OrdinalIgnoreCase))
                    {
                        report.Sku = "Home";
                    }

                    if (report.Sku.Equals("advancedserver", StringComparison.OrdinalIgnoreCase))
                    {
                        report.Sku = "EnterpriseServer";
                    }
                }
                else
                {
                    if (report.Sku.Equals("EnterpriseServer", StringComparison.OrdinalIgnoreCase))
                    {
                        report.Sku = "AdvancedServer";
                    }
                }

                if (report.BuildNumber >= 1911)
                {
                    if (report.Sku.Equals("workstation", StringComparison.OrdinalIgnoreCase))
                    {
                        report.Sku = "Professional";
                    }
                }
            }

            if (isUnstaged && report.Editions != null)
            {
                foreach (string skuunstaged in report.Editions)
                {
                    if (skuunstaged.Contains("server", StringComparison.OrdinalIgnoreCase) &&
                        skuunstaged.EndsWith("hyperv", StringComparison.OrdinalIgnoreCase) ||
                        skuunstaged.Contains("server", StringComparison.OrdinalIgnoreCase) &&
                        skuunstaged.EndsWith("v", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!report.Types.Contains(Type.ServerV))
                        {
                            report.Types.Add(Type.ServerV);
                        }
                    }
                    else if (skuunstaged.Contains("server", StringComparison.OrdinalIgnoreCase))
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
            }
            else if (!string.IsNullOrEmpty(report.Sku))
            {
                if (report.Sku.Equals("ads", StringComparison.OrdinalIgnoreCase))
                {
                    report.Sku = "AdvancedServer";
                }

                if (report.Sku.Equals("pro", StringComparison.OrdinalIgnoreCase))
                {
                    report.Sku = "Professional";
                }

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

            return report;
        }

        private static VersionInfo1 ExtractVersionInfo(string kernelPath)
        {
            VersionInfo1 result = new();

            FileVersionInfo info = FileVersionInfo.GetVersionInfo(kernelPath);

            try
            {
                result.Architecture = Common.GetMachineTypeFromFile(new FileStream(kernelPath, FileMode.Open));
            }
            catch
            {
            }

            string ver = info.FileVersion;

            if (ver != null)
            {
                if (ver.Count(x => x == '.') < 4)
                {
                    ver = info.FileMajorPart + "." + info.FileMinorPart + "." + info.FileBuildPart + "." +
                          info.FilePrivatePart;
                }

                result.Version = Common.ParseBuildString(ver);
            }

            result.BuildType = info.IsDebug ? BuildType.chk : BuildType.fre;

            return result;
        }

        private static (string baseSku, string sku) ExtractEditionFromRegistry(string systemHivePath,
            string softwareHivePath, string virtualEditionsPath)
        {
            (string baseSku, string sku) ret = ("", "");
            Dictionary<string, string> virtualEditionsMapping = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(virtualEditionsPath))
            {
                using (FileStream virtualEditionsStream = new(virtualEditionsPath, FileMode.Open, FileAccess.Read))
                {
                    var veDoc = XDocument.Load(virtualEditionsStream);
                    virtualEditionsMapping = veDoc.Descendants("Edition")
                        .Where(e => e.Attribute("virtual")?.Value == "true" && e.Element("Name") != null && e.Element("ParentEdition") != null)
                        .ToDictionary(e => e.Element("Name").Value, e => e.Element("ParentEdition").Value);
                }
            }

            using (FileStream softHiveStream = new(softwareHivePath, FileMode.Open, FileAccess.Read))
            using (RegistryHive softHive = new(softHiveStream))
            {
                try
                {
                    RegistryKey subkey = softHive.Root.OpenSubKey(@"Microsoft\Windows NT\CurrentVersion");
                    if (subkey != null)
                    {
                        string edition = subkey.GetValue("EditionID") as string;
                        string compositionEdition = subkey.GetValue("CompositionEditionID") as string;

                        if (virtualEditionsMapping.ContainsKey(edition))
                        {
                            return (virtualEditionsMapping[edition], edition);
                        }

                        if (!string.IsNullOrEmpty(virtualEditionsPath))
                        {
                            return (edition, edition);
                        }

                        if (!string.IsNullOrEmpty(edition) && !string.IsNullOrEmpty(compositionEdition))
                        {
                            return (compositionEdition, edition);
                        }

                        if (!string.IsNullOrEmpty(edition))
                        {
                            return (edition, edition);
                        }
                    }
                }
                catch
                {
                }
            }

            using FileStream hiveStream = new(systemHivePath, FileMode.Open, FileAccess.Read);
            using RegistryHive hive = new(hiveStream);
            try
            {
                RegistryKey subkey = hive.Root.OpenSubKey(@"ControlSet001\Control\ProductOptions");

                byte[] prodpol = (byte[])subkey.GetValue("ProductPolicy");
                PolicyValue[] policies = Common.ParseProductPolicy(prodpol);

                PolicyValue pol = policies.FirstOrDefault(x => x.Name == "Kernel-ProductInfo");
                if (pol != null && pol.Type == 4)
                {
                    int product = BitConverter.ToInt32(pol.Data);
                    Console.WriteLine("Detected product id: " + product);

                    if (Enum.IsDefined(typeof(Product), product))
                    {
                        ret.baseSku = Enum.GetName(typeof(Product), product);
                    }
                    else
                    {
                        ret.baseSku = $"UnknownAdditional{product:X}";
                    }

                    ret.sku = ret.baseSku;
                    Console.WriteLine("Base SKU: " + ret.baseSku);
                }

                pol = policies.FirstOrDefault(x => x.Name == "Kernel-BrandingInfo");
                if (pol != null && pol.Type == 4)
                {
                    int product = BitConverter.ToInt32(pol.Data);
                    Console.WriteLine("Detected product id: " + product);

                    if (Enum.IsDefined(typeof(Product), product))
                    {
                        ret.sku = Enum.GetName(typeof(Product), product);
                    }
                    else
                    {
                        ret.sku = $"UnknownAdditional{product:X}";
                    }

                    if (string.IsNullOrEmpty(ret.baseSku))
                    {
                        ret.baseSku = ret.sku;
                    }

                    Console.WriteLine("Branding SKU: " + ret.sku);
                }

                return ret;
            }
            catch
            {
            }

            string sku = "";
            if (string.IsNullOrEmpty(sku))
            {
                try
                {
                    RegistryKey subkey = hive.Root.OpenSubKey(@"ControlSet001\Control\ProductOptions");

                    string[] list = (string[])subkey.GetValue("ProductSuite");

                    sku = list.Length > 0 ? list[0] : "";
                    if (string.IsNullOrEmpty(sku))
                    {
                        sku = "Workstation";
                    }

                    sku = sku.Replace(" ", "");

                    switch (sku.ToLower())
                    {
                        case "enterprise":
                        case "backoffice":
                        case "datacenter":
                        case "securityappliance":
                            {
                                sku += "Server";
                                break;
                            }
                        case "whserver":
                            {
                                sku = "HomeServer";
                                break;
                            }
                        case "smallbusiness":
                            {
                                sku = "SmallBusinessServer";
                                break;
                            }
                        case "smallbusiness(restricted)":
                            {
                                sku = "SmallBusinessServerRestricted";
                                break;
                            }
                        case "blade":
                            {
                                sku = "ServerWeb";
                                break;
                            }
                        case "embeddednt":
                            {
                                sku = "Embedded";
                                break;
                            }
                        case "embedded(restricted)":
                            {
                                sku = "EmbeddedRestricted";
                                break;
                            }
                    }
                }
                catch
                {
                }
            }

            return (sku, sku);
        }

        private static VersionInfo2 ExtractVersionInfo2(string softwareHivePath, string systemHivePath, string hvPath)
        {
            VersionInfo2 result = new()
            {
                Version = new WindowsVersion()
            };

            using (FileStream hiveStream = new(softwareHivePath, FileMode.Open, FileAccess.Read))
            using (RegistryHive hive = new(hiveStream))
            {
                try
                {
                    RegistryKey subkey = hive.Root.OpenSubKey(@"Microsoft\Windows NT\CurrentVersion");

                    string buildLab = (string)subkey.GetValue("BuildLab");
                    string buildLabEx = (string)subkey.GetValue("BuildLabEx");

                    string releaseId = (string)subkey.GetValue("ReleaseId");

                    int? major = (int?)subkey.GetValue("CurrentMajorVersionNumber");
                    string build = (string)subkey.GetValue("CurrentBuildNumber");
                    int? minor = (int?)subkey.GetValue("CurrentMinorVersionNumber");
                    int? ubr = (int?)subkey.GetValue("UBR");
                    string branch = null;

                    subkey = hive.Root.OpenSubKey(
                        @"Microsoft\Windows NT\CurrentVersion\Update\TargetingInfo\Installed");
                    if (subkey != null)
                    {
                        foreach (RegistryKey sub in subkey.SubKeys)
                        {
                            if (!sub.Name.Contains(".OS."))
                            {
                                continue;
                            }

                            branch = sub.GetValue("Branch") as string;
                        }
                    }

                    if (!string.IsNullOrEmpty(buildLab) && buildLab.Count(x => x == '.') == 2)
                    {
                        string[] splitLab = buildLab.Split('.');

                        result.Version.BranchName = splitLab[1];
                        result.Version.CompileDate = splitLab[2];
                        result.Version.BuildNumber = ulong.Parse(splitLab[0]);
                    }

                    if (!string.IsNullOrEmpty(buildLabEx) && buildLabEx.Count(x => x == '.') == 4)
                    {
                        string[] splitLabEx = buildLabEx.Split('.');

                        result.Version.BranchName = splitLabEx[3];
                        result.Version.CompileDate = splitLabEx[4];
                        result.Version.DeltaVersion = ulong.Parse(splitLabEx[1]);
                        result.Version.BuildNumber = ulong.Parse(splitLabEx[0]);
                    }

                    if (major.HasValue)
                    {
                        result.Version.MajorVersion = (ulong)major.Value;
                    }

                    if (minor.HasValue)
                    {
                        result.Version.MinorVersion = (ulong)minor.Value;
                    }

                    if (!string.IsNullOrEmpty(build))
                    {
                        result.Version.BuildNumber = ulong.Parse(build);
                    }

                    if (ubr.HasValue)
                    {
                        result.Version.DeltaVersion = (ulong)ubr.Value;
                    }

                    if (!string.IsNullOrEmpty(branch))
                    {
                        result.Version.BranchName = branch;
                    }

                    if (!string.IsNullOrEmpty(releaseId))
                    {
                        result.Tag = releaseId;
                    }
                }
                catch
                {
                }

                if (!string.IsNullOrEmpty(hvPath))
                {
                    try
                    {
                        ReadOnlySpan<char> content = File.ReadAllText(hvPath, Encoding.ASCII).AsSpan();
                        content = content[content.IndexOf("GitEnlistment")..];
                        content = content[(content.IndexOf('.') + 1)..];
                        content = content[..11];

                        result.Version.CompileDate = new string(content);
                    }
                    catch
                    {
                    }
                }

                try
                {
                    string productId = "";
                    bool found = false;

                    RegistryKey subkey = hive.Root.OpenSubKey(@"Microsoft\Windows NT\CurrentVersion");
                    if (subkey != null)
                    {
                        if (subkey.GetValue("DigitalProductId4") is byte[] pidData)
                        {
                            pidData = pidData[0x3f8..0x458];

                            Span<char> pidString = new char[0x30];
                            Encoding.Unicode.GetChars(pidData, pidString);
                            pidString = pidString[..pidString.IndexOf('\0')];

                            string licenseType = new(pidString);
                            result.Licensing = (Licensing)Enum.Parse(typeof(Licensing), licenseType.Split(':')[0]);
                            found = true;
                        }
                    }

                    if (!found)
                    {
                        subkey = hive.Root.OpenSubKey(@"Microsoft\Windows NT\CurrentVersion\DefaultProductKey");
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
                }
                catch
                {
                }
            }

            using (FileStream hiveStream = new(systemHivePath, FileMode.Open, FileAccess.Read))
            using (RegistryHive hive = new(hiveStream))
            {
                try
                {
                    RegistryKey subkey = hive.Root.OpenSubKey(@"ControlSet001\Control\MUI\UILanguages");

                    result.LanguageCodes = subkey.GetSubKeyNames();
                }
                catch
                {
                }

                try
                {
                    RegistryKey subkey = hive.Root.OpenSubKey(@"ControlSet001\Control\Nls\Language");

                    string langid = (string)subkey.GetValue("Default");

                    CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);

                    if (cultures.Any(x =>
                            x.LCID == int.Parse(langid, NumberStyles.HexNumber, CultureInfo.CurrentCulture)))
                    {
                        string name = cultures.First(x =>
                            x.LCID == int.Parse(langid, NumberStyles.HexNumber, CultureInfo.CurrentCulture)).Name;
                        if (result.LanguageCodes == null ||
                            result.LanguageCodes != null && !result.LanguageCodes.Any(x =>
                                x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (result.LanguageCodes == null)
                            {
                                result.LanguageCodes = new[] { name };
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            return result;
        }

        private static string[] GatherUnstagedEditions(IWindowsInstallProviderInterface installProvider)
        {
            SortedSet<string> report = new();

            IEnumerable<string> packages = installProvider.GetFileSystemEntries().Where(x =>
                x.StartsWith(@"packages\", StringComparison.OrdinalIgnoreCase));

            IEnumerable<string> files = packages.Where(x => x.Count(y => y == '\\') == 1 && x.Contains('.'));

            if (files.Any())
            {
                // This is the final layout

                IEnumerable<string> neutralNames = files.Select(x => string.Join(".", x.Split('.')[..^1])).Distinct();

                HashSet<string> fileArray = files.ToHashSet();

                foreach (string name in neutralNames)
                {
                    if (fileArray.Contains(name + ".mum") &&
                        fileArray.Contains(name + ".cat") &&
                        fileArray.Contains(name + ".xml"))
                    {
                        // This should be a target edition package
                        report.Add(name.Replace(@"packages\", "", StringComparison.OrdinalIgnoreCase));
                    }
                }
            }
            else
            {
                // This is the earlier layout, the folders present are thus the editions

                IEnumerable<string> editionFolders = packages.Where(x => x.Count(y => y == '\\') == 1);
                IEnumerable<string> editions = editionFolders.Select(x =>
                    x.Replace(@"packages\", "", StringComparison.OrdinalIgnoreCase));

                report.UnionWith(editions);
            }

            return report.ToArray();
        }
    }
}