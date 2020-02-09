using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace WindowsBuildIdentifier
{
    public class Comparer
    {
        public static void CompareBuilds(string xml1, string xml2)
        {
            Console.WriteLine("Reading 1");
            FileItem[] build1 = Deserialize(xml1);
            Console.WriteLine("Reading 2");
            FileItem[] build2 = Deserialize(xml2);

            Console.WriteLine("Getting paths 1");
            var build1Paths = build1.Select(x => Sanitize(x.Location)).Where(x => !ExcludedFromChecks(x)).ToHashSet();
            Console.WriteLine("Getting paths 2");
            var build2Paths = build2.Select(x => Sanitize(x.Location)).Where(x => !ExcludedFromChecks(x)).ToHashSet();

            Console.WriteLine("Getting common paths");
            var commonPaths = build1Paths.Intersect(build2Paths).ToHashSet();

            Console.WriteLine("Getting unique paths 1");
            var uniqueBuild1 = build1Paths.Where(x => !commonPaths.Contains(x)).ToHashSet();
            Console.WriteLine("Getting unique paths 2");
            var uniqueBuild2 = build2Paths.Where(x => !commonPaths.Contains(x)).ToHashSet();

            Console.WriteLine("Getting common resources");
            var commonResources = commonPaths.Where(x => x.Contains(@"\.rsrc\")).ToHashSet();

            Console.WriteLine($"{commonPaths.Count()} common paths");
            Console.WriteLine($"{commonResources.Count()} common resources");
            Console.WriteLine($"{uniqueBuild1.Count()} unique 1");
            Console.WriteLine($"{uniqueBuild2.Count()} unique 2");

            Console.WriteLine("Processing common resources");
            HashSet<string> modifiedResources = new HashSet<string>();

            long total = commonResources.Count;
            long counter = 0;

            foreach (var resource in commonResources)
            {
                Console.Title = $"Processing {counter}/{total}";
                var item1 = GetFileItem(build1, resource);
                var item2 = GetFileItem(build2, resource);

                if (item1.Hash != null)
                {
                    if (item1.Hash.SHA1 != item2.Hash.SHA1)
                    {
                        Console.WriteLine("modified");
                        modifiedResources.Add(item1.Location);
                    }
                }
                counter++;
            }

            Console.WriteLine();
            Console.WriteLine("Files unique to build 1");
            Console.WriteLine();
            foreach (var file in uniqueBuild1)
            {
                Console.WriteLine(file);
            }

            Console.WriteLine();
            Console.WriteLine("Files unique to build 2");
            Console.WriteLine();
            foreach (var file in uniqueBuild2)
            {
                Console.WriteLine(file);
            }

            Console.WriteLine();
            Console.WriteLine("Modified resources between both builds");
            Console.WriteLine();
            foreach (var file in modifiedResources)
            {
                Console.WriteLine(file);
            }
        }

        private static string Sanitize(string path1)
        {
            if (path1.Contains(@"installedrepository\", StringComparison.InvariantCultureIgnoreCase))
            {
                var fold1t = path1.ToLower().Split(@"installedrepository\")[0];
                var fold1 = path1.ToLower().Split(@"installedrepository\")[1];

                var number = fold1.Count(x => x == '_');
                if (number < 1)
                {
                    return path1;
                }

                var fsplit1 = fold1.Split(@"\");

                var san1 = string.Join("_", fsplit1[0].Split("_")[0..^1]);

                if (fsplit1.Length < 2)
                {
                    return san1;
                }

                var f1 = fold1t + "installedrepository\\" + san1 + "\\" + string.Join("\\", fsplit1[1..^0]);
                return f1;
            }
            else if (path1.Contains(@"build\filerepository\", StringComparison.InvariantCultureIgnoreCase))
            {
                var fold1t = path1.ToLower().Split(@"build\filerepository\")[0];
                var fold1 = path1.ToLower().Split(@"build\filerepository\")[1];

                var number = fold1.Count(x => x == '_');
                if (number < 1)
                {
                    return path1;
                }

                var fsplit1 = fold1.Split(@"\");

                var san1 = string.Join("_", fsplit1[0].Split("_")[0..^1]);

                if (fsplit1.Length < 2)
                {
                    return san1;
                }

                var f1 = fold1t + "build\\filerepository\\" + san1 + "\\" + string.Join("\\", fsplit1[1..^0]);
                return f1;
            }
            else if (path1.Contains(@"Windows\winsxs\", StringComparison.InvariantCultureIgnoreCase))
            {
                var fold1t = path1.ToLower().Split(@"windows\winsxs\")[0];
                var fold1 = path1.ToLower().Split(@"windows\winsxs\")[1];

                var number = fold1.Count(x => x == '_');
                if (number < 1)
                {
                    return path1;
                }

                var fsplit1 = fold1.Split(@"\");

                var san1 = string.Join("_", fsplit1[0].Split("_")[0..^1]);

                if (fsplit1.Length < 2)
                {
                    return san1;
                }

                var f1 = fold1t + "windows\\winsxs\\" + san1 + "\\" + string.Join("\\", fsplit1[1..^0]);
                return f1;
            }
            else
            {
                return path1;
            }
        }

        private static FileItem GetFileItem(FileItem[] array, string path)
        {
            return array.First(x => Sanitize(x.Location) == path);
        }

        private static bool ExcludedFromChecks(string path)
        {
            string index = @"install.wim\3\";

            if (!path.Contains(index, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (path.Contains(@"\.rsrc\"))
            {
                if (path.EndsWith("version.txt"))
                {
                    return true;
                }
            }

            return false;
        }

        private static FileItem[] Deserialize(string path)
        {
            XmlSerializer deserializer = new XmlSerializer(typeof(FileItem[]));
            TextReader reader = new StreamReader(path);
            object obj = deserializer.Deserialize(reader);
            FileItem[] XmlData = (FileItem[])obj;
            reader.Close();

            return XmlData;
        }
    }
}
