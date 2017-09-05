# DiskList
A C# library that provide disk persistent version of `List<byte[]>` with the following implementation details:
- List only support `.add(byte[] data)` modifications (ie, no value replacement)
- Values are stored across several files (parts), each part contains a fixed number (1024 by default) of byte arrays (records).
- Files (parts) are being held open and index-to-offset maps are loaded into memory to improve read access performance.
- parts can erased from disk at any time. When this happens, application is notified.
- Access to non-existing indices results in a `nil` byte[] return.

Performance (Tested on i7-76000U 2.80GHz, PC300 NVMe SK Hynix 512GB):
- Write Speed: 930 Mb/s
- Read Speed: 1950 Mb/s