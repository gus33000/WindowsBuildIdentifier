﻿/*
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

using SevenZipExtractor;
using System;
using System.IO;
using System.Linq;

namespace WindowsBuildIdentifier.Interfaces
{
    public class WIMInstallProviderInterface : WindowsInstallProviderInterface
    {
        private readonly Stream _wimstream;
        private readonly string _index;
        private readonly ArchiveFile archiveFile;

        public WIMInstallProviderInterface(Stream wimstream, string index)
        {
            _wimstream = wimstream;
            _index = index;
            archiveFile = new ArchiveFile(_wimstream, SevenZipFormat.Wim);
        }

        public string ExpandFile(string Entry)
        {
            string pathprefix = string.IsNullOrEmpty(_index)
                ? ""
                : _index + (Entry.StartsWith('\\') ? "" : "\\");

            if (!archiveFile.Entries.Any(x => x.FileName.Equals(pathprefix + Entry, StringComparison.InvariantCultureIgnoreCase)))
            {
                return null;
            }

            Entry wimEntry = archiveFile.Entries.First(x => x.FileName.Equals(pathprefix + Entry, StringComparison.InvariantCultureIgnoreCase));

            string tmp = Path.GetTempFileName();
            wimEntry.Extract(tmp);

            return tmp;
        }

        public string[] GetFileSystemEntries()
        {
            string pathprefix = string.IsNullOrEmpty(_index) ? "" : _index + @"\";

            string[] entries = archiveFile.Entries.Where(x => x.FileName.StartsWith(pathprefix)).Select(x =>
            {
                if (!string.IsNullOrEmpty(pathprefix))
                {
                    return x.FileName.Substring(2);
                }

                return x.FileName;
            }).ToArray();

            return entries;
        }

        public void Close()
        {
            archiveFile.Dispose();
        }
    }
}
