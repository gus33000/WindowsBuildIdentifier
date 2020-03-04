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
using WindowsBuildIdentifier.Interfaces;

namespace WindowsBuildIdentifier.Identification.InstalledImage
{
    public class DetectionHandler
    {
        public static WindowsImage IdentifyWindowsNT(WindowsInstallProviderInterface installProvider)
        {
            WindowsImage report = new WindowsImage();

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
            string userPath = "";

            var kernelEntry = fileentries.FirstOrDefault(x =>
            x.EndsWith(
                @"\ntkrnlmp.exe", StringComparison.InvariantCultureIgnoreCase) &&
                !x.Contains("WinSxS", StringComparison.InvariantCultureIgnoreCase)) ??
            fileentries.FirstOrDefault(x =>
                x.EndsWith(@"\ntoskrnl.exe", StringComparison.InvariantCultureIgnoreCase) &&
                !x.Contains("WinSxS", StringComparison.InvariantCultureIgnoreCase));
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

            var userEntry = fileentries.FirstOrDefault(x => x.EndsWith(@"\system32\user.exe", StringComparison.InvariantCultureIgnoreCase));
            if (userEntry != null)
            {
                userPath = installProvider.ExpandFile(userEntry);
            }

            #region Version Gathering
            VersionInfo1 info = new VersionInfo1();
            VersionInfo2 info2 = new VersionInfo2();
            VersionInfo1 info3 = new VersionInfo1();

            if (!string.IsNullOrEmpty(kernelPath))
            {
                Console.WriteLine("Extracting version information from the image 1");
                info = ExtractVersionInfo(kernelPath);

                File.Delete(kernelPath);

                report.Architecture = info.Architecture;
                report.BuildType = info.BuildType;
            }

            if (!string.IsNullOrEmpty(softwareHivePath) && !string.IsNullOrEmpty(systemHivePath))
            {
                Console.WriteLine("Extracting version information from the image 2");
                info2 = ExtractVersionInfo2(softwareHivePath, systemHivePath);
            }

            if (!string.IsNullOrEmpty(userPath))
            {
                Console.WriteLine("Extracting version information from the image 3");
                info3 = ExtractVersionInfo(userPath);
            }

            report.Tag = info2.Tag;
            report.Licensing = info2.Licensing;
            report.LanguageCodes = info2.LanguageCodes;

            WindowsVersion correctVersion = Common.GetGreaterVersion(info.version, info2.version);
            correctVersion = Common.GetGreaterVersion(correctVersion, info3.version);

            if (correctVersion != null)
            {
                report.MajorVersion = correctVersion.MajorVersion;
                report.MinorVersion = correctVersion.MinorVersion;
                report.BuildNumber = correctVersion.BuildNumber;
                report.DeltaVersion = correctVersion.DeltaVersion;
                report.BranchName = correctVersion.BranchName;
                report.CompileDate = correctVersion.CompileDate;
            }
            #endregion

            #region Edition Gathering
            bool IsUnstaged = fileentries.Any(x => x.StartsWith(@"packages\", StringComparison.InvariantCultureIgnoreCase));
            
            if (IsUnstaged)
            {
                report.Sku = "Unstaged";

                Console.WriteLine("Image detected as unstaged, gathering target editions available in the image");

                // parse editions
                report.Editions = GatherUnstagedEditions(installProvider);
            }
            else if (fileentries.Contains(@"InstalledRepository\Windows.config"))
            {
                var packageFile = installProvider.ExpandFile(@"InstalledRepository\Windows.config");

                using (var fileStream = File.OpenRead(packageFile))
                using (var streamReader = new StreamReader(fileStream))
                {
                    string line = "";

                    while (!line.Contains("configurationIdentity"))
                        line = streamReader.ReadLine();

                    var editionPackageName = line.Split("name=\"")[1].Split("\"")[0];

                    var editionName = string.Join("", editionPackageName.Split(" ")[1..^0]);
                    if (editionName == "Server")
                    {
                        editionName = "ServerStandard";
                    }

                    report.Sku = editionName;
                }

                File.Delete(packageFile);
            }
            else if (!string.IsNullOrEmpty(systemHivePath))
            {
                Console.WriteLine("Extracting additional edition information");
                report.Sku = ExtractEditionFromRegistry(systemHivePath);
            }
            #endregion

            if (!string.IsNullOrEmpty(softwareHivePath))
                File.Delete(softwareHivePath);
            if (!string.IsNullOrEmpty(systemHivePath))
                File.Delete(systemHivePath);

            report = FixSkuNames(report, IsUnstaged);

            return report;
        }

        public static WindowsImage FixSkuNames(WindowsImage report, bool IsUnstaged)
        {
            if (!string.IsNullOrEmpty(report.Sku))
            {
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
            }

            if (IsUnstaged && report.Editions != null)
            {
                foreach (var skuunstaged in report.Editions)
                {
                    if ((skuunstaged.Contains("server", StringComparison.InvariantCultureIgnoreCase) && skuunstaged.EndsWith("hyperv", StringComparison.InvariantCultureIgnoreCase)) ||
                        (skuunstaged.Contains("server", StringComparison.InvariantCultureIgnoreCase) && skuunstaged.EndsWith("v", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        if (!report.Types.Contains(Type.ServerV))
                        {
                            report.Types.Add(Type.ServerV);
                        }
                    }
                    else if (skuunstaged.Contains("server", StringComparison.InvariantCultureIgnoreCase))
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
                    if (!report.Types.Contains(Type.ServerV))
                    {
                        report.Types.Add(Type.ServerV);
                    }
                }
                else if (report.Sku.Contains("server", StringComparison.InvariantCultureIgnoreCase))
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
            VersionInfo1 result = new VersionInfo1();

            FileVersionInfo info = FileVersionInfo.GetVersionInfo(kernelPath);

            try
            {
                result.Architecture = Common.GetMachineTypeFromFile(new FileStream(kernelPath, FileMode.Open));
            }
            catch
            {
                
            }

            var ver = info.FileVersion;

            if (ver.Count(x => x == '.') < 4)
            {
                ver = info.FileMajorPart + "." + info.FileMinorPart + "." + info.FileBuildPart + "." + info.FilePrivatePart;
            }

            result.version = Common.ParseBuildString(ver);

            result.BuildType = info.IsDebug ? BuildType.chk : BuildType.fre;

            return result;
        }

        private static string ExtractEditionFromRegistry(string systemHivePath)
        {
            string sku = "";

            using (var hiveStream = new FileStream(systemHivePath, FileMode.Open, FileAccess.Read))
            using (DiscUtils.Registry.RegistryHive hive = new DiscUtils.Registry.RegistryHive(hiveStream))
            {
                try
                {
                    DiscUtils.Registry.RegistryKey subkey = hive.Root.OpenSubKey(@"ControlSet001\Control\ProductOptions");

                    if (subkey.GetValue("SubscriptionPfnList") != null)
                    {
                        var pfn = ((string[])subkey.GetValue("SubscriptionPfnList"))[0];
                        var product = pfn.Split(".")[2];
                        product = product.Replace("Pro", "Professional");
                        sku = product;
                        Console.WriteLine("Effective SKU: " + sku);
                    }
                    else
                    {
                        var prodpol = (byte[])subkey.GetValue("ProductPolicy");

                        var policies = Common.ParseProductPolicy(prodpol);

                        if (policies.Any(x => x.Name == "Kernel-ProductInfo"))
                        {
                            var pol = policies.First(x => x.Name == "Kernel-ProductInfo");

                            if (pol.Type == 4)
                            {
                                int product = BitConverter.ToInt32(pol.Data);
                                Console.WriteLine("Detected product id: " + product);

                                if (Enum.IsDefined(typeof(Product), product))
                                {
                                    sku = Enum.GetName(typeof(Product), product);
                                }
                                else
                                {
                                    sku = $"UnknownAdditional{product.ToString("X")}";
                                }

                                Console.WriteLine("Effective SKU: " + sku);
                            }
                        }
                    }
                }
                catch { };

                if (string.IsNullOrEmpty(sku))
                {
                    try
                    {
                        DiscUtils.Registry.RegistryKey subkey = hive.Root.OpenSubKey(@"ControlSet001\Control\ProductOptions");

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
                                    sku = sku + "Server";
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
                    catch { };
                }
            }

            return sku;
        }

        private static VersionInfo2 ExtractVersionInfo2(string softwareHivePath, string systemHivePath)
        {
            VersionInfo2 result = new VersionInfo2();

            result.version = new WindowsVersion();

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

                        result.version.BranchName = splitLab[1];
                        result.version.CompileDate = splitLab[2];
                        result.version.BuildNumber = ulong.Parse(splitLab[0]);
                    }

                    if (!string.IsNullOrEmpty(buildLabEx) && buildLabEx.Count(x => x == '.') == 4)
                    {
                        var splitLabEx = buildLabEx.Split('.');

                        result.version.BranchName = splitLabEx[3];
                        result.version.CompileDate = splitLabEx[4];
                        result.version.DeltaVersion = ulong.Parse(splitLabEx[1]);
                        result.version.BuildNumber = ulong.Parse(splitLabEx[0]);
                    }

                    if (UBR.HasValue)
                    {
                        result.version.DeltaVersion = (ulong)UBR.Value;
                    }

                    if (Major.HasValue)
                    {
                        result.version.MajorVersion = (ulong)Major.Value;
                    }

                    if (Minor.HasValue)
                    {
                        result.version.MinorVersion = (ulong)Minor.Value;
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

            var packages = installProvider.GetFileSystemEntries().Where(x => x.StartsWith(@"packages\", StringComparison.InvariantCultureIgnoreCase));

            var files = packages.Where(x => x.Count(y => y == '\\') == 1 && x.Contains("."));

            if (files.Any())
            {
                // This is the final layout

                var neutralNames = files.Select(x => string.Join(".", x.Split('.')[0..^1])).Distinct();

                var fileArray = files.ToHashSet();

                foreach (var name in neutralNames)
                {
                    if (fileArray.Contains(name + ".mum") &&
                        fileArray.Contains(name + ".cat") &&
                        fileArray.Contains(name + ".xml"))
                    {
                        // This should be a target edition package
                        report.Add(name.Replace(@"packages\", "", StringComparison.InvariantCultureIgnoreCase));
                    }
                }
            }
            else
            {
                // This is the earlier layout, the folders present are thus the editions

                var editionFolders = packages.Where(x => x.Count(y => y == '\\') == 1);
                var editions = editionFolders.Select(x => x.Replace(@"packages\", "", StringComparison.InvariantCultureIgnoreCase));

                report.UnionWith(editions);
            }

            return report.ToArray();
        }
    }
}
