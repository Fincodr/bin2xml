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
        static uint ECU_FlashData_Start  = 0x28000;
        static uint ECU_FlashData_End    = 0x2FFFF;

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
            Console.WriteLine("Denso ECU map lookup table to Enginuity xml format converter");

            string IN_FILE = args[0];
            string OUT_FILE = IN_FILE.Replace(".bin", "") + ".xml";

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

            tout.WriteLine("  <romid>");
            tout.WriteLine("   <xmlid>XXX</xmlid>");
            tout.WriteLine("   <make>XXX</make>");
            tout.WriteLine("   <model>XXX</model>");
            tout.WriteLine("   <submodel>XXX</submodel>");
            tout.WriteLine("   <year>01/01</year>");
            tout.WriteLine("   <market>XXXX</market>");
            tout.WriteLine("   <transmission>XX</transmission>");
            tout.WriteLine("   <memmodel>SH7052</memmodel>");
            tout.WriteLine("   <internalidstring>XXXXX-XXXX</internalidstring>");
            tout.WriteLine("   <internalidaddress>0xXXXXX</internalidaddress>");
            tout.WriteLine("   <filesize>256kb</filesize>");
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

                // x = getbyte(n)
                x = bin.ReadByte();

                //IF x < 16 THEN          '2D map
                if (x<16) {     // 2D map
                    mapType = MAPTYPE.MAP2D;
                    // Xlen$ = STR$(getbyte(n + 1)): Xlen$ = RIGHT$(Xlen$, (LEN(Xlen$) - 1))
                    Xlen = bin.ReadByte();
                    // skip next 2 bytes
                    bin.ReadByte();
                    bin.ReadByte();
                    // Xaddr$ = getlong$(n + 4)
                    Xaddr = swapEndianness(bin.ReadUInt32());
                    // Maddr$ = getlong$(n + 8)
                    Maddr = swapEndianness(bin.ReadUInt32());
                    // n = n + 16
                    n += 16;
                    fsin.Seek(n, SeekOrigin.Begin);
                    // tabletype$ = "2D"
                    m2d++;
                }
                // ELSEIF x < 43 THEN                   '3d map
                else if (x<43)  // 3D map
                {
                    mapType = MAPTYPE.MAP3D;
                    // Xlen$ = STR$(getbyte(n + 1)): Xlen$ = RIGHT$(Xlen$, (LEN(Xlen$) - 1))
                    Xlen = bin.ReadByte();
                    // Ylen$ = STR$(getbyte(n + 2)): Ylen$ = RIGHT$(Ylen$, (LEN(Ylen$) - 1))
                    Ylen = bin.ReadByte();
                    // skip next byte
                    bin.ReadByte();
                    // Xaddr$ = getlong$(n + 4)
                    Xaddr = swapEndianness(bin.ReadUInt32());
                    // Yaddr$ = getlong$(n + 8)
                    Yaddr = swapEndianness(bin.ReadUInt32());
                    // Maddr$ = getlong$(n + 12)
                    Maddr = swapEndianness(bin.ReadUInt32());
                    // n = n + 20
                    n += 20;
                    fsin.Seek(n, SeekOrigin.Begin);
                    // tabletype$ = "3D"
                    m3d++;
                }

                if (mapType == MAPTYPE.MAP2D)
                {
                    tout.WriteLine("  <table type=\"2D\" name=\"Map 0x{0:X8}\" category=\"{1}\" storagetype=\"{2}\" endian=\"big\" sizex=\"{3}\" userlevel=\"1\" storageaddress=\"0x{4:X8}\">",
                        Maddr,
                        "x" + Xlen,
                        (x & (int)1)!=0 ? "uint8" : "uint16",
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

            bin.Close();
        }
    }
}
