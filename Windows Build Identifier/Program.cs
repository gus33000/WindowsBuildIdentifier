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

using DiscUtils.Iso9660;
using DiscUtils.Udf;
using DiscUtils.Vfs;
using System;
using System.Diagnostics;
using System.IO;

namespace WindowsBuildIdentifier
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("Release Identifier Tool");
            Console.WriteLine("TBD Release Database Indexing Toolset");
            Console.WriteLine("TBD (c) 2008-2020");
            Console.WriteLine("Gustave Monce (@gus33000) (c) 2009-2020");
            Console.WriteLine();

            var ogcolor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Pre-release version. For evaluation purposes only.");
            Console.ForegroundColor = ogcolor;
            Console.WriteLine();

            if (args.Length < 1)
            {
                Console.WriteLine("Usage: <Path to directory containing ISO files>");
                Console.WriteLine();
                return;
            }

            DiscUtils.Complete.SetupHelper.SetupComplete();

            foreach (var isopath in Directory.GetFiles(args[0], "*.iso", SearchOption.AllDirectories))
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Opening ISO File");
                Console.WriteLine(isopath);
                try
                {
                    using FileStream isoStream = File.Open(isopath, FileMode.Open, FileAccess.Read);

                    VfsFileSystemFacade cd = new CDReader(isoStream, true);
                    if (cd.FileExists(@"README.TXT"))
                    {
                        cd = new UdfReader(isoStream);
                    }

                    //
                    // WIM Setup
                    //
                    if (cd.FileExists(@"sources\install.wim"))
                    {
                        try
                        {
                            //
                            // If this succeeds we are processing a properly supported final (or near final)
                            // WIM file format, so we use the adequate function to handle it.
                            //
                            Identification.MediaHandler.IdentifyWindowsNTFromWIM(cd.OpenFile(@"sources\install.wim", FileMode.Open, FileAccess.Read));
                        }
                        catch (Identification.MediaHandler.UnsupportedWIMException)
                        {
                            //
                            // If this fails we are processing an early
                            // WIM file format, so we use the adequate function to handle it.
                            //
                            Console.WriteLine("Early WIM Format TODO");
                        }
                    }
                    else if (cd.FileExists(@"sources\install.esd"))
                    {
                        try
                        {
                            //
                            // If this succeeds we are processing a properly supported final (or near final)
                            // WIM file format, so we use the adequate function to handle it.
                            //
                            Identification.MediaHandler.IdentifyWindowsNTFromWIM(cd.OpenFile(@"sources\install.esd", FileMode.Open, FileAccess.Read));
                        }
                        catch (Identification.MediaHandler.UnsupportedWIMException)
                        {
                            //
                            // If this fails we are processing an early
                            // WIM file format, so we use the adequate function to handle it.
                            //
                            Console.WriteLine("Early WIM Format TODO");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No idea");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Fail");
                    Console.WriteLine(ex.ToString());
                }
            }

            Console.WriteLine("Done.");

#if DEBUG
            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            }
#endif
        }
    }
}
