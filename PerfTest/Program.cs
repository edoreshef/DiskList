using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PerfTest
{
    class Program
    {
        static void Main()
        {
            // Create a diffrent list for performance test
            var list = new DiskList.DiskList("PerfTest-0000.dlist", FileAccess.ReadWrite);

            // Create buffer
            var bufSizeMb = 1;
            var buf = new byte[bufSizeMb * 1024 * 1024];

            // Stress write
            Console.WriteLine("Starting a 5 second write test");
            var writeCounter = 0;
            var writeStopwatch = Stopwatch.StartNew();
            while (writeStopwatch.ElapsedMilliseconds < 5000)
            {
                writeCounter++;
                list.Add(buf);
            }
            writeStopwatch.Stop();

            // Compute and print performance
            Console.WriteLine($"Created {writeCounter * bufSizeMb} MBs in {writeStopwatch.Elapsed.TotalSeconds:0.0} seconds");
            Console.WriteLine("Write speed: " + (writeCounter * bufSizeMb / writeStopwatch.Elapsed.TotalSeconds).ToString("0.0") + " MB/s");
            Console.WriteLine("");

            // Perform read test
            Console.WriteLine("Starting a read test");
            var readStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < list.Count; i++)
                list[i].Count();
            readStopwatch.Stop();

            // Write read test result
            Console.WriteLine("Read speed: " + (writeCounter * bufSizeMb / readStopwatch.Elapsed.TotalSeconds).ToString("0.0") + " MB/s");

            // Delete created files
            foreach (var file in Directory.GetFiles(".", "PerfTest-????.dlist"))
                File.Delete(file);

            Console.ReadKey();
        }
    }
}
