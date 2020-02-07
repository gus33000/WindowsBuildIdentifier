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
            Console.WriteLine("BetaArchive Release Database Indexing Toolset");
            Console.WriteLine("BetaArchive (c) 2008-2020");
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
                Identification.MediaHandler.IdentifyWindowsFromISO(isopath);
            }

            foreach (var vhdpath in Directory.GetFiles(args[0], "*.vhd", SearchOption.AllDirectories))
            {
                Identification.MediaHandler.IdentifyWindowsFromVHD(vhdpath);
            }

            foreach (var vhdpath in Directory.GetFiles(args[0], "*.vhdx", SearchOption.AllDirectories))
            {
                Identification.MediaHandler.IdentifyWindowsFromVHDX(vhdpath);
            }

            foreach (var vhdpath in Directory.GetFiles(args[0], "*.vmdk", SearchOption.AllDirectories))
            {
                Identification.MediaHandler.IdentifyWindowsFromVMDK(vhdpath);
            }

            foreach (var vhdpath in Directory.GetFiles(args[0], "*.vdi", SearchOption.AllDirectories))
            {
                Identification.MediaHandler.IdentifyWindowsFromVDI(vhdpath);
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
