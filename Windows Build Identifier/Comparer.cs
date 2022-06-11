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
            HashSet<string> build1Paths = build1.Select(x => Sanitize(x.Location)).Where(x => !ExcludedFromChecks(x)).ToHashSet();
            Console.WriteLine("Getting paths 2");
            HashSet<string> build2Paths = build2.Select(x => Sanitize(x.Location)).Where(x => !ExcludedFromChecks(x)).ToHashSet();

            Console.WriteLine("Getting common paths");
            HashSet<string> commonPaths = build1Paths.Intersect(build2Paths).ToHashSet();

            Console.WriteLine("Getting unique paths 1");
            HashSet<string> uniqueBuild1 = build1Paths.Where(x => !commonPaths.Contains(x)).ToHashSet();
            Console.WriteLine("Getting unique paths 2");
            HashSet<string> uniqueBuild2 = build2Paths.Where(x => !commonPaths.Contains(x)).ToHashSet();

            Console.WriteLine("Getting common resources");
            HashSet<string> commonResources = commonPaths.Where(x => x.Contains(@"\.rsrc\")).ToHashSet();

            Console.WriteLine($"{commonPaths.Count} common paths");
            Console.WriteLine($"{commonResources.Count} common resources");
            Console.WriteLine($"{uniqueBuild1.Count} unique 1");
            Console.WriteLine($"{uniqueBuild2.Count} unique 2");

            Console.WriteLine("Processing common resources");
            HashSet<string> modifiedResources = new();

            long total = commonResources.Count;
            long counter = 0;

            foreach (string resource in commonResources)
            {
                Console.Title = $"Processing {counter}/{total}";
                FileItem item1 = GetFileItem(build1, resource);
                FileItem item2 = GetFileItem(build2, resource);

                if (item1.Hash != null)
                {
                    if (item1.Hash.Sha1 != item2.Hash.Sha1)
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
            foreach (string file in uniqueBuild1)
            {
                Console.WriteLine(file);
            }

            Console.WriteLine();
            Console.WriteLine("Files unique to build 2");
            Console.WriteLine();
            foreach (string file in uniqueBuild2)
            {
                Console.WriteLine(file);
            }

            Console.WriteLine();
            Console.WriteLine("Modified resources between both builds");
            Console.WriteLine();
            foreach (string file in modifiedResources)
            {
                Console.WriteLine(file);
            }
        }

        private static string Sanitize(string path1)
        {
            if (path1.Contains(@"installedrepository\", StringComparison.OrdinalIgnoreCase))
            {
                string fold1T = path1.ToLower().Split(@"installedrepository\")[0];
                string fold1 = path1.ToLower().Split(@"installedrepository\")[1];

                int number = fold1.Count(x => x == '_');
                if (number < 1)
                {
                    return path1;
                }

                string[] fsplit1 = fold1.Split(@"\");

                string san1 = string.Join("_", fsplit1[0].Split("_")[..^1]);

                if (fsplit1.Length < 2)
                {
                    return san1;
                }

                string f1 = fold1T + "installedrepository\\" + san1 + "\\" + string.Join("\\", fsplit1[1..]);
                return f1;
            }

            if (path1.Contains(@"build\filerepository\", StringComparison.OrdinalIgnoreCase))
            {
                string fold1T = path1.ToLower().Split(@"build\filerepository\")[0];
                string fold1 = path1.ToLower().Split(@"build\filerepository\")[1];

                int number = fold1.Count(x => x == '_');
                if (number < 1)
                {
                    return path1;
                }

                string[] fsplit1 = fold1.Split(@"\");

                string san1 = string.Join("_", fsplit1[0].Split("_")[..^1]);

                if (fsplit1.Length < 2)
                {
                    return san1;
                }

                string f1 = fold1T + "build\\filerepository\\" + san1 + "\\" + string.Join("\\", fsplit1[1..]);
                return f1;
            }

            if (path1.Contains(@"Windows\winsxs\", StringComparison.OrdinalIgnoreCase))
            {
                string fold1T = path1.ToLower().Split(@"windows\winsxs\")[0];
                string fold1 = path1.ToLower().Split(@"windows\winsxs\")[1];

                int number = fold1.Count(x => x == '_');
                if (number < 1)
                {
                    return path1;
                }

                string[] fsplit1 = fold1.Split(@"\");

                string san1 = string.Join("_", fsplit1[0].Split("_")[..^1]);

                if (fsplit1.Length < 2)
                {
                    return san1;
                }

                string f1 = fold1T + "windows\\winsxs\\" + san1 + "\\" + string.Join("\\", fsplit1[1..]);
                return f1;
            }

            return path1;
        }

        private static FileItem GetFileItem(FileItem[] array, string path)
        {
            return array.First(x => Sanitize(x.Location) == path);
        }

        private static bool ExcludedFromChecks(string path)
        {
            string index = @"install.wim\3\";

            if (!path.Contains(index, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

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
            XmlSerializer deserializer = new(typeof(FileItem[]));
            TextReader reader = new StreamReader(path);
            object obj = deserializer.Deserialize(reader);
            FileItem[] xmlData = (FileItem[])obj;
            reader.Close();

            return xmlData;
        }
    }
}