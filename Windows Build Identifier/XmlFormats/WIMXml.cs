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

using System.Xml.Serialization;

namespace WindowsBuildIdentifier.XmlFormats
{
    public class WIMXml
    {
        [XmlRoot(ElementName = "CREATIONTIME")]
        public class CREATIONTIME
        {
            [XmlElement(ElementName = "HIGHPART")]
            public string HIGHPART { get; set; }

            [XmlElement(ElementName = "LOWPART")]
            public string LOWPART { get; set; }
        }

        [XmlRoot(ElementName = "LASTMODIFICATIONTIME")]
        public class LASTMODIFICATIONTIME
        {
            [XmlElement(ElementName = "HIGHPART")]
            public string HIGHPART { get; set; }

            [XmlElement(ElementName = "LOWPART")]
            public string LOWPART { get; set; }
        }

        [XmlRoot(ElementName = "LANGUAGES")]
        public class LANGUAGES
        {
            [XmlElement(ElementName = "LANGUAGE")]
            public string LANGUAGE { get; set; }

            [XmlElement(ElementName = "DEFAULT")]
            public string DEFAULT { get; set; }
        }

        [XmlRoot(ElementName = "VERSION")]
        public class VERSION
        {
            [XmlElement(ElementName = "MAJOR")]
            public string MAJOR { get; set; }

            [XmlElement(ElementName = "MINOR")]
            public string MINOR { get; set; }

            [XmlElement(ElementName = "BUILD")]
            public string BUILD { get; set; }

            [XmlElement(ElementName = "SPBUILD")]
            public string SPBUILD { get; set; }

            [XmlElement(ElementName = "SPLEVEL")]
            public string SPLEVEL { get; set; }
        }

        [XmlRoot(ElementName = "WINDOWS")]
        public class WINDOWS
        {
            [XmlElement(ElementName = "ARCH")]
            public string ARCH { get; set; }

            [XmlElement(ElementName = "PRODUCTNAME")]
            public string PRODUCTNAME { get; set; }

            [XmlElement(ElementName = "EDITIONID")]
            public string EDITIONID { get; set; }

            [XmlElement(ElementName = "INSTALLATIONTYPE")]
            public string INSTALLATIONTYPE { get; set; }

            [XmlElement(ElementName = "HAL")]
            public string HAL { get; set; }

            [XmlElement(ElementName = "PRODUCTTYPE")]
            public string PRODUCTTYPE { get; set; }

            [XmlElement(ElementName = "PRODUCTSUITE")]
            public string PRODUCTSUITE { get; set; }

            [XmlElement(ElementName = "LANGUAGES")]
            public LANGUAGES LANGUAGES { get; set; }

            [XmlElement(ElementName = "VERSION")]
            public VERSION VERSION { get; set; }

            [XmlElement(ElementName = "SYSTEMROOT")]
            public string SYSTEMROOT { get; set; }
        }

        [XmlRoot(ElementName = "IMAGE")]
        public class IMAGE
        {
            [XmlElement(ElementName = "DIRCOUNT")]
            public string DIRCOUNT { get; set; }

            [XmlElement(ElementName = "FILECOUNT")]
            public string FILECOUNT { get; set; }

            [XmlElement(ElementName = "TOTALBYTES")]
            public string TOTALBYTES { get; set; }

            [XmlElement(ElementName = "HARDLINKBYTES")]
            public string HARDLINKBYTES { get; set; }

            [XmlElement(ElementName = "CREATIONTIME")]
            public CREATIONTIME CREATIONTIME { get; set; }

            [XmlElement(ElementName = "LASTMODIFICATIONTIME")]
            public LASTMODIFICATIONTIME LASTMODIFICATIONTIME { get; set; }

            [XmlElement(ElementName = "WINDOWS")]
            public WINDOWS WINDOWS { get; set; }

            [XmlElement(ElementName = "NAME")]
            public string NAME { get; set; }

            [XmlElement(ElementName = "DESCRIPTION")]
            public string DESCRIPTION { get; set; }

            [XmlElement(ElementName = "FLAGS")]
            public string FLAGS { get; set; }

            [XmlAttribute(AttributeName = "INDEX")]
            public string INDEX { get; set; }
        }

        [XmlRoot(ElementName = "WIM")]
        public class WIM
        {
            [XmlElement(ElementName = "TOTALBYTES")]
            public string TOTALBYTES { get; set; }

            [XmlElement(ElementName = "IMAGE")]
            public IMAGE[] IMAGE { get; set; }
        }
    }
}