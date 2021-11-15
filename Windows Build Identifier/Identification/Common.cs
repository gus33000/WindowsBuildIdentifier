using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace WindowsBuildIdentifier.Identification
{
    public static class Common
    {
        public static MachineType GetMachineTypeFromFile(Stream fs)
        {
            BinaryReader br = new(fs);
            fs.Seek(0x3c, SeekOrigin.Begin);
            uint peOffset = br.ReadUInt32();
            fs.Seek(peOffset, SeekOrigin.Begin);
            uint peHead = br.ReadUInt32();
            if (peHead != 0x00004550) // "PE\0\0", little-endian
            {
                throw new Exception("Can't find PE header");
            }

            ushort type = br.ReadUInt16();

            Console.WriteLine("Machine type: " + type);

            MachineType machineType = (MachineType)type;
            br.Close();
            fs.Close();
            return machineType;
        }

        public static WindowsVersion ParseBuildString(string buildString)
        {
            WindowsVersion verinfo = new();

            if (buildString.Contains(" built by: ") && buildString.Contains(" at: ") &&
                buildString.Count(x => x == '.') == 3)
            {
                // Early version
                string[] splitparts = buildString.Split(' ');
                string rawversion = splitparts[0];
                string branch = splitparts[3];
                string compiledate = splitparts[5];

                string[] splitver = rawversion.Split('.');

                verinfo.MajorVersion = ulong.Parse(splitver[0]);
                verinfo.MinorVersion = ulong.Parse(splitver[1]);
                verinfo.BuildNumber = ulong.Parse(splitver[2]);
                verinfo.DeltaVersion = ulong.Parse(splitver[3]);

                verinfo.BranchName = branch;
                verinfo.CompileDate = compiledate;
            }
            else if (buildString.Contains(" (") && buildString.Contains(')') && buildString.Count(x => x == '.') >= 4)
            {
                if (buildString.Contains(" (WinBuild."))
                {
                    // MS new thing

                    string[] splitparts = buildString.Split(' ');
                    string rawversion = splitparts[0];

                    string[] splitver = rawversion.Split('.');

                    verinfo.MajorVersion = ulong.Parse(splitver[0]);
                    verinfo.MinorVersion = ulong.Parse(splitver[1]);
                    verinfo.BuildNumber = ulong.Parse(splitver[2]);
                    verinfo.DeltaVersion = ulong.Parse(splitver[3]);
                }
                else
                {
                    // Normal thing

                    string[] splitparts = buildString.Split(' ');
                    string rawversion = splitparts[0];
                    string rawdetails = splitparts[1];

                    string[] splitdetails = rawdetails.TrimStart('(').TrimEnd(')').Split('.');

                    string branch = splitdetails[0];
                    string compiledate = splitdetails[1];

                    string[] splitver = rawversion.Split('.');

                    Console.WriteLine(rawversion);

                    verinfo.MajorVersion = ulong.Parse(splitver[0]);
                    verinfo.MinorVersion = ulong.Parse(splitver[1]);
                    verinfo.BuildNumber = ulong.Parse(splitver[2]);
                    verinfo.DeltaVersion = ulong.Parse(splitver[3]);

                    verinfo.BranchName = branch;
                    verinfo.CompileDate = compiledate;
                }
            }
            else if (buildString.Count(x => x == '.') == 3)
            {
                string[] splitver = buildString.Split('.');

                verinfo.MajorVersion = ulong.Parse(splitver[0]);
                verinfo.MinorVersion = ulong.Parse(splitver[1]);
                verinfo.BuildNumber = ulong.Parse(splitver[2]);
                verinfo.DeltaVersion = ulong.Parse(splitver[3]);
            }

            return verinfo;
        }

        public static WindowsVersion GetGreaterVersion(WindowsVersion version1, WindowsVersion version2)
        {
            if (version1 == null)
            {
                return version2;
            }

            if (version2 == null)
            {
                return version1;
            }

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

                const string format = "yyMMdd-HHmm";

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

        public static PolicyValue[] ParseProductPolicy(byte[] productPolicy)
        {
            HashSet<PolicyValue> policyValues = new();

            Console.WriteLine("Parsing product policy");

            int totalSize = BitConverter.ToInt32(productPolicy, 0);
            int valuesSize = BitConverter.ToInt32(productPolicy, 4);
            int endMarkerSize = BitConverter.ToInt32(productPolicy, 8);

            int headerSize = totalSize - valuesSize - endMarkerSize;

            byte[] valueBuffer = productPolicy[headerSize..(headerSize + valuesSize - 1)];
            using (MemoryStream innerStream = new(valueBuffer))
            using (BinaryReader stream = new(innerStream))
            {
                while (innerStream.Position <= innerStream.Length)
                {
                    long currentPosition = innerStream.Position;

                    short totalLength = stream.ReadInt16();
                    short nameLength = stream.ReadInt16();
                    short valueType = stream.ReadInt16();
                    short valueLength = stream.ReadInt16();

                    //
                    // Earlier product policy format (5112 for example)
                    // Doesn't have 2 extra values
                    // We check if we have enough space for 2 extra values
                    // If we do we skip both
                    //
                    if (valueLength + nameLength + 8 <= totalLength)
                    {
                        innerStream.Seek(8, SeekOrigin.Current);
                    }

                    string valueName = Encoding.Unicode.GetString(stream.ReadBytes(nameLength));
                    byte[] value = stream.ReadBytes(valueLength);
                    innerStream.Seek(currentPosition + totalLength, SeekOrigin.Begin);

                    policyValues.Add(new PolicyValue { Name = valueName, Type = valueType, Data = value });
                }
            }

            return policyValues.ToArray();
        }

        public static void DisplayReport(WindowsImage report)
        {
            string typedisp = report.Types != null
                ? string.Join(", ", report.Types.Select(e => e.ToString()))
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
            Console.WriteLine("Types        : " + typedisp);
            Console.WriteLine("Base Sku     : " + report.BaseSku);
            Console.WriteLine("Sku          : " + report.Sku);
            Console.WriteLine("Editions     : " + editiondisp);
            Console.WriteLine("Licensing    : " + report.Licensing);
            Console.WriteLine("LanguageCodes: " + langdisp);
            Console.WriteLine();
        }
    }
}