using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DiskListSample
{
    class Program
    {
        static void Main(string[] args)
        {

            // Stress write
            //var buf = new byte[1024*1024];
            //var writeCounter = 0;
            //var ts = Stopwatch.StartNew();
            //while (ts.ElapsedMilliseconds < 2000)
            //{
            //    writeCounter++;
            //    list.Add(buf);
            //}
            //ts.Stop();
            //var bufSizeMB = buf.Length/(1024.0*1024.0);
            //Console.WriteLine(((writeCounter * bufSizeMB) / ts.Elapsed.TotalSeconds).ToString("0.0") + " MB/s");
            //Console.ReadKey();
            //return;


            // Test part fragmentation (lowering PartCapacity to 2 helps)
            Console.WriteLine("Testing fragmentation:");
            Console.WriteLine("(A=add P=print M=measure)");

            var list = new DiskList("test1-0000.data", FileAccess.ReadWrite);
            list.PartCapacity = 2;

            while (true)
            {
                // Decide what show we do
                var key = Console.ReadKey();
                Console.WriteLine();

                // Add Items
                if (key.Key == ConsoleKey.A)
                    for (var i = 0; i < 5; i++)
                        list.Add(Encoding.UTF8.GetBytes($"add #{i}: list[{list.Count}] {DateTime.Now}"));

                // Print List
                if (key.Key == ConsoleKey.P)
                    for (var i = 0; i < list.Count; i++)
                    {
                        var item = list[i];
                        Console.WriteLine(" {0}: {1}", i, (item == null ? "null" : Encoding.UTF8.GetString(item)));
                    }

                if (key.Key == ConsoleKey.M)
                {
                    // Create a diffrent list for performance test
                    var list2 = new DiskList("test2-0000.data", FileAccess.ReadWrite);

                    // Stress write
                    var buf = new byte[1024*1024];
                    var writeCounter = 0;
                    var ts = Stopwatch.StartNew();
                    while (ts.ElapsedMilliseconds < 10000)
                    {
                        writeCounter++;
                        list2.Add(buf);
                    }
                    ts.Stop();
                    var bufSizeMB = buf.Length/(1024.0*1024.0);
                    Console.WriteLine(((writeCounter * bufSizeMB) / ts.Elapsed.TotalSeconds).ToString("0.0") + " MB/s");
                }

                // Quick
                if (key.Key == ConsoleKey.Q)
                    break;
            }
        }
    }
}
