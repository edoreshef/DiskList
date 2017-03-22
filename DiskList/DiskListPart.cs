using System;
using System.IO;

namespace DiskList
{
    internal class DiskListPart: IDisposable
    {
        public int    PartIndex;
        public string FilePath { get; }
        public bool IsWritable { get; }
        public FileStream DataStream;
        public BinaryReader Reader;
        public BinaryWriter Writer;
        public long   MaxCapacity;
        public long   StartIndex;
        public long   RecordCount;
        public long[] Index;

        public static void CreatePart(string filepath, long maxValuesCount, long startIndex)
        {
            using (var file =  new FileStream(filepath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            {
                var bw = new BinaryWriter(file);
                bw.Write(maxValuesCount);
                bw.Write(startIndex);
                var data = new byte[maxValuesCount * 8 * 2];
                file.Write(data, 0, data.Length);
            }
        }

        public DiskListPart(string filePath, FileAccess fileAccess)
        {
            FilePath = filePath;

            // Open streams
            IsWritable = (fileAccess & FileAccess.Write) != 0;
            var fileShare = FileShare.Delete | (IsWritable ? FileShare.ReadWrite : FileShare.Read);
            DataStream = new FileStream(filePath, FileMode.OpenOrCreate, fileAccess, fileShare);
            Reader = new BinaryReader(DataStream);
            Writer = new BinaryWriter(DataStream);

            // Read index size
            DataStream.Position = 0;
            MaxCapacity = Reader.ReadInt64();
            StartIndex  = Reader.ReadInt64();

            // Read index data
            var index = new byte[MaxCapacity * 8 * 2];
            DataStream.Read(index, 0, index.Length);
            Index = new long[MaxCapacity * 2];
            Buffer.BlockCopy(index, 0, Index, 0, index.Length);

            // Scan for records count
            for (var i = (int)(MaxCapacity - 1) * 2; i >= 0; i-= 2)
                if (Index[i] != 0)
                {
                    RecordCount = i / 2 + 1;
                    break;
                }

            DataStream.Seek(0, SeekOrigin.End);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                DataStream?.Dispose();
                DataStream = null;
                Reader?.Dispose();
                Reader = null;
                Writer?.Dispose();
                Writer = null;
            }
        }

        public byte[] Get(long index)
        {
            // Check index
            if (index < 0 || index >= MaxCapacity)
                throw new IndexOutOfRangeException();

            // Get offset in file
            var offset = Index[index * 2 + 0];
            var size   = Index[index * 2 + 1];

            // read data
            var data = new byte[size];
            DataStream.Position = offset;
            DataStream.Read(data, 0, (int)size);
            return data;
        }

        public void Add(byte[] data)
        {
            // Make sure list isn't read only
            if (!IsWritable)
                throw new Exception("'Add()' cannot be called on a read only list");

            // Allways append
            DataStream.Seek(0, SeekOrigin.End);

            // Store current position
            var offset = DataStream.Position;

            // Write data
            DataStream.Write(data, 0, data.Length);

            // Update meory index 
            Index[RecordCount * 2 + 0] = offset;
            Index[RecordCount * 2 + 1] = data.Length;

            // Update disk index
            DataStream.Position = 16 + 2 * 8 * RecordCount;
            Writer.Write(offset);
            Writer.Write(data.Length);

            // Increment record count
            RecordCount++;
        }

        public void Flush()
        {
            DataStream.Flush();
        }
    }
}