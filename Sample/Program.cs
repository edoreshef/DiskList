using System;
using System.Diagnostics;
using System.IO;
using System.Net.Mime;
using System.Reflection;
using System.Text;

namespace DiskListSample
{
    class Program
    {
        static void Main()
        {
            // Test part fragmentation (lowering PartCapacity to 2 helps)
            Console.WriteLine("DiskList Test Application");

            // Open list
            Console.WriteLine("- Opening list0-0000.dlist (default part capacity 8)");
            Directory.CreateDirectory("Storage");
            var list = new DiskList.DiskList(@"Storage\list0-0000.dlist", FileAccess.ReadWrite, 8);
            Console.WriteLine($"  RecordCount={list.Count} FirstAvailableIndex={list.FirstAvailableIndex}");

            // Register to part removals
            list.ValuesRemoved += (sender, index, records) => Console.WriteLine($"Values are no longer available ({index}..{index + records - 1})");

            while (true)
            {
                // Print options
                Console.WriteLine("");
                Console.WriteLine("Options:");
                Console.WriteLine("  'A' -> Add 5 records");
                Console.WriteLine("  'P' -> Print all records");
                Console.WriteLine("  'O' -> Open Explorer (and delete parts manually)");
                Console.WriteLine("  'Q' -> Quit");
                Console.Write(" Press A,P,O or Q:");

                // Decide what show we do
                var key = Console.ReadKey();
                Console.WriteLine();

                // Add Items
                if (key.Key == ConsoleKey.A)
                {
                    Console.WriteLine("Adding Values");
                    for (var i = 0; i < 5; i++)
                        list.Add(Encoding.UTF8.GetBytes($"add #{i}: list[{list.Count}] {DateTime.Now}"));
                }

                // Print List
                if (key.Key == ConsoleKey.P)
                    for (var i = 0; i < list.Count; i++)
                    {
                        var item = list[i];
                        Console.WriteLine(" {0}: {1}", i, (item == null ? "null" : Encoding.UTF8.GetString(item)));
                    }

                if (key.Key == ConsoleKey.O)
                {

                    Console.WriteLine("Opening Explorer");
                    Process.Start("explorer.exe", Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Storage"));
                }

                // Quick
                if (key.Key == ConsoleKey.Q)
                    break;
            }
        }
    }
}
