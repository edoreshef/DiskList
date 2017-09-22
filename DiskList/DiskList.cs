using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace DiskList
{
    public class DiskList: IDisposable
    {
        private ReaderWriterLock m_PartsLock = new ReaderWriterLock();
        private List<DiskListPart> m_Parts = new List<DiskListPart>();
        private readonly FileAccess m_FileAccess;

        // FilePath related
        private int    m_Filename_NextPartIndex;
        private string m_Filename_Formatter;
        private int    m_Filename_CounterDigits;
        private string m_Filename_Directory;
        private string m_Filename_SearchPattern;
        private FileSystemWatcher m_FileSystemWatcher;

        public delegate void ValuesRemovedNotification(object sender, long fromIndex, long records);

        public event ValuesRemovedNotification ValuesRemoved;

        public int PartCapacity { get; private set; }

        public long Count { get; private set; }

        public long FirstAvailableIndex { get; private set; }

        public DiskList(string listFilename, FileAccess fileAccess, int defaultPartCapacity = 1024)
        {
            // Store parameters
            m_FileAccess = fileAccess;
            PartCapacity = defaultPartCapacity;

            // Decode filename
            var filenameMatch = Regex.Match(Path.GetFullPath(listFilename), @"(.*-)(\d+)(.[^-\n]*)");
            if (!filenameMatch.Success)
                throw new Exception("File name formatting must be 'filename-0000.ext'");
            m_Filename_Formatter = filenameMatch.Groups[1].Value + "{0}" + filenameMatch.Groups[3].Value;
            m_Filename_CounterDigits = filenameMatch.Groups[2].Value.Length;

            // Find what parts exists in disk
            var searchPath = string.Format(m_Filename_Formatter, new string('?', m_Filename_CounterDigits));
            m_Filename_Directory = Path.GetDirectoryName(searchPath);
            m_Filename_SearchPattern = Path.GetFileName(searchPath);

            // Load indices
            RebuildPartList();

            // Setup file watch
            m_FileSystemWatcher = new FileSystemWatcher(m_Filename_Directory, m_Filename_SearchPattern);
            m_FileSystemWatcher.Deleted += (sender, args) => RebuildPartList();
            m_FileSystemWatcher.Renamed += (sender, args) => RebuildPartList();
            m_FileSystemWatcher.Created += (sender, args) => RebuildPartList();
            m_FileSystemWatcher.NotifyFilter = NotifyFilters.FileName;
            m_FileSystemWatcher.EnableRaisingEvents = true;

            // Start delete pending check thread
            new Thread(DeletePendingBackgroundCheck) { IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();
        }

        private void DeletePendingBackgroundCheck()
        {
            while (true)
            {
                Thread.Sleep(500);
                CheckDeletePending();
            }
        }

        public void CheckDeletePending()
        { 
            try
            {
                m_PartsLock.AcquireReaderLock(int.MaxValue);

                // Create a delete pending list
                var deletePending = new List<int>();
                for (var iPart = 0; iPart < m_Parts.Count; iPart++)
                {
                    // Ignore missing parts
                    if (m_Parts[iPart] == null)
                        continue;

                    // try to open file, delete pending file will not open
                    try
                    {
                        using (new FileStream(m_Parts[iPart].FilePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Delete | FileShare.ReadWrite)) ;
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        deletePending.Add(iPart);
                    }
                }

                // Do we need to remove files?
                if (deletePending.Count > 0)
                {
                    // Upgrade lock
                    var lockCookie = m_PartsLock.UpgradeToWriterLock(int.MaxValue);
                    try
                    {
                        // Release files
                        foreach (var iPart in deletePending)
                        {
                            // Notify on part removal
                            ValuesRemoved?.Invoke(this, m_Parts[iPart].StartIndex, m_Parts[iPart].RecordCount);

                            // Release part
                            m_Parts[iPart].Dispose();
                            m_Parts[iPart] = null;
                        }

                        // Rebuild parts list
                        RebuildPartList();
                    }
                    finally
                    {
                        m_PartsLock.DowngradeFromWriterLock(ref lockCookie);
                    }
                }
            }
            finally
            {
                m_PartsLock.ReleaseReaderLock();
            }
        }

        private void RebuildPartList()
        {
            // Clone existing parts list (we want to resue DiskListPart)
            try
            {
                m_PartsLock.AcquireWriterLock(int.MaxValue);

                // Create a dictionary of files on disk
                var regex = new Regex(@"(.*-)(\d+)(.[^-\n]*)", RegexOptions.Compiled);
                var partsFiles = Directory.GetFiles(m_Filename_Directory, m_Filename_SearchPattern);
                var indexToFile = partsFiles.ToDictionary(partFile => int.Parse(regex.Match(partFile).Groups[2].Value));

                // Clone currently parts list
                var prevParts = m_Parts.Where(t => t != null).ToDictionary(t => t.PartIndex);
           
                // Create parts-to-load dictionary
                var newPartsList = new Dictionary<int, DiskListPart>();
                foreach (var partFile in indexToFile)
                {
                    try
                    {
                        // try to use existing part
                        DiskListPart part = null;
                        if (prevParts.ContainsKey(partFile.Key))
                        {
                            part = prevParts[partFile.Key];
                            prevParts.Remove(partFile.Key);
                        }

                        // Create if not existed before
                        if (part == null)
                            part = new DiskListPart(partFile.Value, m_FileAccess) {PartIndex = partFile.Key};

                        // Add to index list
                        newPartsList.Add(partFile.Key, part);
                    }
                    catch (DiskListPart.PartNotReady)
                    {
                        // Part is not ready yet, ignore part
                    }
                }

                // Cleanup deleted item
                foreach (var partToDelete in prevParts)
                {
                    // Notify on part removal
                    ValuesRemoved?.Invoke(this, partToDelete.Value.StartIndex, partToDelete.Value.RecordCount);

                    partToDelete.Value.Dispose();
                }
                prevParts.Clear();

                // Things to do on loading of an existing list
                if (newPartsList.Count > 0)
                {
                    // Ensure capacity is identical for all parts
                    PartCapacity = (int)newPartsList.First().Value.MaxCapacity;
                    if (newPartsList.Any(t => t.Value.MaxCapacity != PartCapacity))
                        throw new Exception("All parts must have identical capatiy");

                    // Create part list
                    var firstPartIndex = newPartsList.Keys.Min();
                    var lastPartIndex = newPartsList.Keys.Max();
                    var count = lastPartIndex - firstPartIndex + 1;
                    var newPartList = new List<DiskListPart>(count);
                    while (newPartList.Count < count) newPartList.Add(null);
                    foreach (var item in newPartsList)
                        newPartList[item.Key - firstPartIndex] = item.Value;

                    // Replace part list
                    m_Parts = newPartList;

                    // Store next part index
                    m_Filename_NextPartIndex = lastPartIndex + 1;

                    // Update count & FirstAvailableIndex
                    Count = m_Parts.Last().StartIndex + m_Parts.Last().RecordCount;
                    FirstAvailableIndex = m_Parts[0].StartIndex;
                }
                else
                {
                    // reset part list
                    m_Parts = new List<DiskListPart>();
                    Count = 0;
                    FirstAvailableIndex = 0;
                    m_Filename_NextPartIndex = 0;
                }
            }
            finally
            {
                m_PartsLock.ReleaseWriterLock();
            }
        }

        public void Add(byte[] data)
        {
            try
            {
                // Precent m_Parts from changing
                m_PartsLock.AcquireReaderLock(int.MaxValue);

                // Do we need to add another part?
                var part = m_Parts.LastOrDefault();
                if (part?.RecordCount == part?.MaxCapacity)
                {
                    // Add to part list
                    var lockCookie = new LockCookie();
                    try
                    {
                        // Writer lock
                        lockCookie = m_PartsLock.UpgradeToWriterLock(int.MaxValue);

                        // Create new part file
                        var filename = string.Format(m_Filename_Formatter, m_Filename_NextPartIndex.ToString().PadLeft(m_Filename_CounterDigits, '0'));
                        DiskListPart.CreatePart(filename, PartCapacity, Count);

                        // Load part
                        var newPart = new DiskListPart(filename, m_FileAccess);
                        newPart.MaxCapacity = PartCapacity;
                        newPart.PartIndex = m_Filename_NextPartIndex;
                        m_Parts.Add(newPart);

                        // Increment part counter
                        m_Filename_NextPartIndex++;
                    }
                    finally
                    {
                        m_PartsLock.DowngradeFromWriterLock(ref lockCookie);
                    }
                }

                // Append data
                m_Parts.Last().Add(data);

                // Update count
                Count++;
            }
            finally
            {
                m_PartsLock.ReleaseReaderLock();
            }
        }

        public bool IsIndexExists(long index)
        {
            try
            {
                // Make sure m_Parts isn't chaning
                m_PartsLock.AcquireReaderLock(int.MaxValue);

                // Empty array?
                if (m_Parts.Count == 0)
                    return false;

                // Deleted part?
                if (index < m_Parts[0].StartIndex)
                    return false;

                // compute index relative to m_Parts
                var startOffset = m_Parts[0].StartIndex;
                var partIndex = (index - startOffset) / m_Parts[0].MaxCapacity;

                // Make sure part exists
                if (partIndex >= m_Parts.Count)
                    return false;
                if (m_Parts[(int)partIndex] == null)
                    return false;

                // Get and return data
                return true;
            }
            finally
            {
                m_PartsLock.ReleaseReaderLock();
            }
        }

        public byte[] this[long index]
        {
            get
            {
                try
                {
                    // Make sure m_Parts isn't chaning
                    m_PartsLock.AcquireReaderLock(int.MaxValue);

                    // Empty array?
                    if (m_Parts.Count == 0)
                        return null;

                    // Deleted part?
                    if (index < m_Parts[0].StartIndex)
                        return null;

                    // compute index relative to m_Parts
                    var startOffset = m_Parts[0].StartIndex;
                    var partIndex   = (index - startOffset) / m_Parts[0].MaxCapacity;
                    var indexInPart = (index - startOffset) % m_Parts[0].MaxCapacity;

                    // Make sure part exists
                    if (partIndex >= m_Parts.Count)
                        return null;
                    if (m_Parts[(int)partIndex] == null)
                        return null;

                    // Get and return data
                    return m_Parts[(int)partIndex].Get(indexInPart);
                }
                finally
                {
                    m_PartsLock.ReleaseReaderLock();
                }
            }

            set
            {
                try
                {
                    // Make sure m_Parts isn't chaning
                    m_PartsLock.AcquireReaderLock(int.MaxValue);

                    // Check if we should create list from scratch starting at current index
                    if (m_Parts.Count == 0)
                        Count = index;

                    // Make sure index is sequentail
                    else if (Count != index)
                        throw new Exception($"Unable to append data at index {index} as next index should be {Count}");

                    // Add value to list
                    Add(value);
                }
                finally
                {
                    m_PartsLock.ReleaseReaderLock();
                }
            }
        }

        public void Flush()
        {
            try
            {
                // Make sure m_Parts isn't chaning
                m_PartsLock.AcquireReaderLock(int.MaxValue);

                // Flush everything
                foreach (var part in m_Parts.Where(t => t != null))
                    part.Flush();
            }
            finally
            {
                m_PartsLock.ReleaseReaderLock();
            }
        }

        public void Dispose()
        {
            m_FileSystemWatcher?.Dispose();
            m_Parts.ForEach(t => t.Dispose());
        }
    }
}