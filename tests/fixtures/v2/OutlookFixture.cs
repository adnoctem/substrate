// Synthetic late-bound objects: no Outlook activation, profile changes, or mail access.
#if NETFRAMEWORK || NET
#pragma warning disable 8600, 8603, 8618
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace AdNoctem.Substrate.Tests.Fixtures
{
    public class OutlookFixture
    {
        public readonly FakeStores Stores = new FakeStores();
        public readonly FakeApplication Application = new FakeApplication();
        public int Added;
        public int Removed;
        public bool FailAfterAttach;
        public FakeStore DefaultStore
        {
            get { return Stores.Values[0]; }
        }

        public FakeStore AddExisting(string path, string id)
        {
            var store = new FakeStore(path, id);
            Stores.Values.Add(store);

            return store;
        }

        public void AddStore(string path)
        {
            Added++;
            AddExisting(path, "added-" + Added);

            if (FailAfterAttach)
                throw new InvalidOperationException("Synthetic attachment failure.");
        }

        public void AddStoreEx(string path, int kind)
        {
            if (kind != 2)
                throw new Exception("Expected Unicode.");

            AddStore(path);
        }

        public void RemoveStore(FakeFolder root)
        {
            Removed++;
            Stores.Values.RemoveAll(value => value.StoreID == root.StoreID);
        }

        public FakeStore GetStoreFromID(string id)
        {
            return Stores.Values.Single(store => store.StoreID == id);
        }

        public FakeFolder GetDefaultFolder(int kind)
        {
            return DefaultStore.GetDefaultFolder(kind);
        }

        public FakeFolder GetFolderFromID(string id, string storeId)
        {
            return Find(GetStoreFromID(storeId).Root, id);
        }

        private static FakeFolder Find(FakeFolder folder, string id)
        {
            if (folder.EntryID == id)
                return folder;

            foreach (var child in folder.Folders.Values)
            {
                var found = Find(child, id);

                if (found != null)
                    return found;
            }

            return null;
        }
    }

    public class FakeApplication
    {
        public string Version { get; set; }

        public FakeApplication()
        {
            Version = "16.0";
        }
    }

    public class FakeStores
    {
        public readonly List<FakeStore> Values = new List<FakeStore>();
        public int Count
        {
            get { return Values.Count; }
        }

        public FakeStore Item(int index)
        {
            return Values[index - 1];
        }
    }

    public class FakeStore
    {
        public string FilePath { get; set; }
        public string StoreID { get; set; }
        public string DisplayName
        {
            get { return "Synthetic"; }
        }
        public bool IsDefault
        {
            get { return true; }
        }
        public bool IsDataFileStore
        {
            get { return true; }
        }
        public readonly FakeFolder Root;
        public readonly Dictionary<int, FakeFolder> Standard = new Dictionary<int, FakeFolder>();
        public readonly FakeAccessor PropertyAccessor = new FakeAccessor();

        public FakeStore(string path, string id)
        {
            FilePath = path;
            StoreID = id;
            Root = new FakeFolder(this, "root", "Synthetic", "\\\\Synthetic");
            var inbox = Root.Folders.Add("Posteingang");
            Standard[6] = inbox;
            Standard[3] = Root.Folders.Add("Gelöscht");
            inbox.Folders.Add("Keep");
            inbox.Folders.Add("Skip").Folders.Add("NeverVisit");
            inbox.Folders.Add("Search").PropertyAccessor.FolderType = 2;
            var container = inbox.Folders.Add("CalendarContainer");
            container.DefaultItemType = 1;
            container.Folders.Add("MailChild");
        }

        public FakeFolder GetRootFolder()
        {
            return Root;
        }

        public FakeFolder GetDefaultFolder(int kind)
        {
            FakeFolder result;

            if (Standard.TryGetValue(kind, out result))
                return result;

            throw new COMException("Synthetic absent folder.", unchecked((int)0x8004010f));
        }
    }

    public class FakeFolder
    {
        public string Name { get; set; }
        public string EntryID { get; set; }
        public string StoreID
        {
            get { return Store.StoreID; }
        }
        public string FolderPath { get; set; }
        public int DefaultItemType { get; set; }
        public FakeStore Store { get; private set; }
        public FakeFolders Folders { get; private set; }
        public readonly FakeAccessor PropertyAccessor = new FakeAccessor();

        public FakeFolder(FakeStore store, string id, string name, string path)
        {
            Store = store;
            EntryID = id;
            Name = name;
            FolderPath = path;
            Folders = new FakeFolders(this);
        }
    }

    public class FakeFolders
    {
        private readonly FakeFolder parent;
        public readonly List<FakeFolder> Values = new List<FakeFolder>();

        public FakeFolders(FakeFolder parent)
        {
            this.parent = parent;
        }

        public int Count
        {
            get { return Values.Count; }
        }

        public FakeFolder Item(int index)
        {
            return Values[index - 1];
        }

        public FakeFolder Add(string name)
        {
            var path = parent.FolderPath + "\\" + name;
            var result = new FakeFolder(parent.Store, path, name, path);
            Values.Add(result);

            return result;
        }
    }

    public class FakeAccessor
    {
        public int FolderType = 1;
        public readonly Dictionary<string, object> Properties = new Dictionary<string, object>();

        public object GetProperty(string name)
        {
            if (name.EndsWith("36010003", StringComparison.Ordinal))
                return FolderType;

            object value;

            if (Properties.TryGetValue(name, out value))
                return value;

            throw new COMException("Synthetic missing property.", unchecked((int)0x8004010f));
        }

        public string BinaryToString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }
    }
}
