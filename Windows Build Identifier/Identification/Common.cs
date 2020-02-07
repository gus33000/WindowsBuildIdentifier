﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WindowsBuildIdentifier.Identification
{
    public class Common
    {
        public static MachineType GetMachineTypeFromFile(Stream fs)
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

        public static WindowsVersion ParseBuildString(string BuildString)
        {
            WindowsVersion verinfo = new WindowsVersion();

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

        public static WindowsVersion GetGreaterVersion(WindowsVersion version1, WindowsVersion version2)
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
