using System;
using System.Collections;
using System.IO;
using System.Text;

namespace Bin2Xml
{
    class Program
    {
        // http://www.scoobypedia.co.uk/index.php/Knowledge/ECUMapIdentification
        static uint ECU_FlashCode_Start  = 0x0;
        static uint ECU_FlashCode_End    = 0x1FFFF;
        static uint ECU_RAM_Start        = 0x20000;
        static uint ECU_RAM_End          = 0x27FFF;

        public enum MAPTYPE { MAP2D = 1, MAP3D };

        static uint swapEndianness(uint x)
        {
            return ((x & 0x000000ff) << 24) +  // First byte
                   ((x & 0x0000ff00) << 8) +   // Second byte
                   ((x & 0x00ff0000) >> 8) +   // Third byte
                   ((x & 0xff000000) >> 24);   // Fourth byte
        }

        static void Main(string[] args)
        {
            uint ECU_FlashData_Start  = 0x28000; //0x2A544
            uint ECU_FlashData_End    = 0x2FFFF;

            Console.WriteLine("Denso ECU map lookup table to Enginuity xml format converter");
            Console.WriteLine("");
            Console.Write("Enter starting address (default = 0x{0:X8}): ", ECU_FlashData_Start);
            string sAddress = Console.ReadLine();
            if (sAddress.Length > 0)
            {
                if (sAddress.StartsWith("0x"))
                {
                    ECU_FlashData_Start = (uint)Convert.ToInt32(sAddress.Substring(2), 16);
                }
                else
                {
                    ECU_FlashData_Start = UInt32.Parse(sAddress);
                }
            }

            string IN_FILE = args[0];
            string OUT_FILE = IN_FILE.Replace(".bin", "") + ".xml";

            try
            {
                FileInfo fi = new FileInfo(IN_FILE);

                uint startAddress = ECU_FlashData_Start; // default start address

                FileStream fsin = new FileStream(IN_FILE, FileMode.Open);
                BinaryReader bin = new BinaryReader(fsin);

                FileStream fsout = new FileStream(OUT_FILE, FileMode.Create);
                TextWriter tout = new StreamWriter(fsout, System.Text.Encoding.Default);

                // Jump to start of data
                uint n = startAddress;
                fsin.Seek(n, SeekOrigin.Begin);

                tout.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                tout.WriteLine("<roms>");
                tout.WriteLine(" <rom>");

                tout.WriteLine("");

                // Read end
                fsin.Seek(-16, SeekOrigin.End);

                byte[] data = new byte[16];
                long internalIdAddress = fsin.Position;
                fsin.Read(data, 0, 16);
                string internalIdString = "unknown";
                // find start
                int iStart = 0;
                for (iStart = 0; iStart != 16; ++iStart)
                {
                    if (data[iStart] != 255) break;
                }
                if (iStart != 16)
                {
                    // find end
                    int iEnd = iStart;
                    for (; iEnd != 16; ++iEnd)
                    {
                        if (data[iEnd] == 255) break;
                    }
                    internalIdString = System.Text.Encoding.ASCII.GetString(data, iStart, iEnd - iStart - 2);
                    internalIdAddress += iStart;
                }

                fsin.Seek(n, SeekOrigin.Begin);

                tout.WriteLine("  <romid>");
                tout.WriteLine("   <xmlid>{0}</xmlid>", fi.Name.Replace(".bin", ""));
                tout.WriteLine("   <make>XXX</make>");
                tout.WriteLine("   <model>XXX</model>");
                tout.WriteLine("   <submodel>XXX</submodel>");
                tout.WriteLine("   <year>01/01</year>");
                tout.WriteLine("   <market>USDM</market>");
                tout.WriteLine("   <transmission>MT</transmission>");
                tout.WriteLine("   <memmodel>SH7052</memmodel>");
                tout.WriteLine("   <internalidstring>{0}</internalidstring>", internalIdString);
                tout.WriteLine("   <internalidaddress>{0:X8}</internalidaddress>", internalIdAddress);
                tout.WriteLine("   <filesize>{0}kb</filesize>", fi.Length / 1024);
                tout.WriteLine("  </romid>");
                tout.WriteLine("");

                MAPTYPE mapType = 0;
                int m2d = 0;
                int m3d = 0;
                UInt32 Xaddr = 0;
                UInt32 Yaddr = 0;
                UInt32 Maddr = 0;
                int x;
                byte Xlen = 0, Ylen = 0;
                do
                {

                    x = bin.ReadByte();

                    if (x < 16)
                    {     // 2D map
                        mapType = MAPTYPE.MAP2D;
                        Xlen = bin.ReadByte();
                        // skip next 2 bytes
                        bin.ReadByte();
                        bin.ReadByte();
                        Xaddr = swapEndianness(bin.ReadUInt32());
                        Maddr = swapEndianness(bin.ReadUInt32());
                        n += 16;
                        fsin.Seek(n, SeekOrigin.Begin);
                        m2d++;
                    }
                    else if (x < 43)  // 3D map
                    {
                        mapType = MAPTYPE.MAP3D;
                        Xlen = bin.ReadByte();
                        Ylen = bin.ReadByte();
                        // skip next byte
                        bin.ReadByte();
                        Xaddr = swapEndianness(bin.ReadUInt32());
                        Yaddr = swapEndianness(bin.ReadUInt32());
                        Maddr = swapEndianness(bin.ReadUInt32());
                        n += 20;
                        fsin.Seek(n, SeekOrigin.Begin);
                        m3d++;
                    }

                    if (mapType == MAPTYPE.MAP2D)
                    {
                        tout.WriteLine("  <table type=\"2D\" name=\"Map 0x{0:X8}\" category=\"{1}\" storagetype=\"{2}\" endian=\"big\" sizex=\"{3}\" userlevel=\"1\" storageaddress=\"0x{4:X8}\">",
                            Maddr,
                            "x" + Xlen,
                            (x & (int)1) != 0 ? "uint8" : "uint16",
                            Xlen,
                            Maddr
                        );
                        tout.WriteLine("   <scaling units=\"unknown\" expression=\"x\" to_byte=\"x\" format=\"#\" fineincrement=\"1\" coarseincrement=\"10\" />");
                        tout.WriteLine("   <table type=\"X Axis\" name=\"unknown\" storagetype=\"{0}\" endian=\"big\" sizex=\"{1}\" storageaddress=\"0x{2:X8}\">",
                            (x & (int)4) == 4 ? "uint8" : "uint16",
                            Xlen,
                            Xaddr
                        );

                        tout.WriteLine("    <scaling units=\"unknown\" expression=\"x\" to_byte=\"x\" format=\"#\" fineincrement=\"1\" coarseincrement=\"10\" />");
                        tout.WriteLine("   </table>");
                        tout.WriteLine("   <description>function unknown</description>");
                        tout.WriteLine("  </table>");
                        tout.WriteLine();
                    }

                    if (mapType == MAPTYPE.MAP3D)
                    {
                        tout.WriteLine("  <table type=\"3D\" name=\"Map 0x{0:X8}\" category=\"{1}\" storagetype=\"{2}\" endian=\"big\" sizex=\"{3}\" sizey=\"{4}\" userlevel=\"1\" storageaddress=\"0x{5:X8}\">",
                            Maddr,
                            "x" + Xlen + "y" + Ylen,
                            (x & (int)1) != 0 ? "uint8" : "uint16",
                            Xlen,
                            Ylen,
                            Maddr
                        );

                        tout.WriteLine("   <scaling units=\"unknown\" expression=\"x\" to_byte=\"x\" format=\"#\" fineincrement=\"1\" coarseincrement=\"10\" />");
                        tout.WriteLine("   <table type=\"X Axis\" name=\"unknown\" storagetype=\"{0}\" endian=\"big\" sizex=\"{1}\" storageaddress=\"0x{2:X8}\">",
                            (x & (int)4) == 4 ? "uint8" : "uint16",
                            Xlen,
                            Xaddr
                        );

                        tout.WriteLine("    <scaling units=\"unknown\" expression=\"x\" to_byte=\"x\" format=\"#\" fineincrement=\"1\" coarseincrement=\"10\" />");
                        tout.WriteLine("   </table>");
                        tout.WriteLine("   <table type=\"Y Axis\" name=\"unknown\" storagetype=\"{0}\" endian=\"big\" sizex=\"{1}\" storageaddress=\"0x{2:X8}\">",
                            (x & (int)16) == 6 ? "uint8" : "uint16",
                            Ylen,
                            Yaddr
                        );

                        tout.WriteLine("    <scaling units=\"unknown\" expression=\"x\" to_byte=\"x\" format=\"#\" fineincrement=\"1\" coarseincrement=\"10\" />");
                        tout.WriteLine("   </table>");
                        tout.WriteLine("   <description>function unknown</description>");
                        tout.WriteLine("  </table>");
                        tout.WriteLine();
                    }

                } while (x < 43);

                tout.WriteLine(" </rom>");
                tout.WriteLine("</roms>");
                tout.Flush();
                tout.Close();

                Console.WriteLine("2D maps = {0}", m2d);
                Console.WriteLine("3D maps = {0}", m3d);
                Console.WriteLine("internalIdString = {0}", internalIdString);
                Console.WriteLine("internalIdAddress = {0:X8}", internalIdAddress);

                bin.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Caught exception: {0}", ex.ToString());
            }

            Console.WriteLine("Press any key to continue.");
            ConsoleKeyInfo keyInfo = Console.ReadKey();
        }
    }
}
