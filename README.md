# DiskList
A C# library that provide disk persistent version of `List<byte[]>` with the following implementation details:
- List only support `.add(byte[] data)` modifications (ie, no value replacement)
- Values are stored across several files (parts), each part contains a fixed number (1024 by default) of values.
- Files (parts) are being held open to improve read access performance.
- parts can deleted at any time. access to a values in delted parts will result in a `nil` value return.
