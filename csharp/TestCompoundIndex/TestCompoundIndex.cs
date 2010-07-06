using System;
using Perst;

public class TestCompoundIndex { 
    const int nRecords = 100000;
    const int pagePoolSize = 32*1024*1024;

    class Record : Persistent { 
        internal String strKey;
        internal int    intKey;
    };

    static public void Main(string[] args) {	
        Storage db = StorageFactory.Instance.CreateStorage();

	db.Open("testcidx.dbs", pagePoolSize);
        FieldIndex root = (FieldIndex)db.Root;
        if (root == null) { 
            root = db.CreateFieldIndex(typeof(Record), new string[]{"intKey", "strKey"}, true);
            db.Root = root;
        }
        DateTime start = DateTime.Now;
        long key = 1999;
        int i;
        for (i = 0; i < nRecords; i++) { 
            Record rec = new Record();
            key = (3141592621L*key + 2718281829L) % 1000000007L;
            rec.intKey = (int)((ulong)key >> 32);
            rec.strKey = Convert.ToString((int)key);
            root.Put(rec);                
        }
        db.Commit();
        Console.WriteLine("Elapsed time for inserting " + nRecords + " records: " + (DateTime.Now - start));
        
        start = DateTime.Now;
        key = 1999;
        int minKey = Int32.MaxValue;
        int maxKey = Int32.MinValue;
        for (i = 0; i < nRecords; i++) { 
            key = (3141592621L*key + 2718281829L) % 1000000007L;
            int intKey = (int)((ulong)key >> 32);            
            String strKey = Convert.ToString((int)key);
            Record rec = (Record)root.Get(new Key(new Object[]{intKey, strKey}));
            Assert.That(rec != null && rec.intKey == intKey && rec.strKey.Equals(strKey));
            if (intKey < minKey) { 
                minKey = intKey;
            }
            if (intKey > maxKey) { 
                maxKey = intKey;
            }
        }
        Console.WriteLine("Elapsed time for performing " + nRecords + " index searches: " + (DateTime.Now - start));
        
        start = DateTime.Now;
        int n = 0;
        string prevStr = "";
        int prevInt = minKey;
        foreach (Record rec in root.Range(new Key(minKey, ""), 
                                          new Key(maxKey+1, "???"), 
                                          IterationOrder.AscentOrder)) 
        {
            Assert.That(rec.intKey > prevInt || rec.intKey == prevInt && rec.strKey.CompareTo(prevStr) > 0);
            prevStr = rec.strKey;
            prevInt = rec.intKey;
            n += 1;
        }
        Assert.That(n == nRecords);
        
        n = 0;
        prevInt = maxKey+1;
        foreach (Record rec in root.Range(new Key(minKey, "", false), 
                                          new Key(maxKey+1, "???", false), 
                                          IterationOrder.DescentOrder))
        {
            Assert.That(rec.intKey < prevInt || rec.intKey == prevInt && rec.strKey.CompareTo(prevStr) < 0);
            prevStr = rec.strKey;
            prevInt = rec.intKey;
            n += 1;
        }
        Assert.That(n == nRecords);
        Console.WriteLine("Elapsed time for iterating through " + (nRecords*2) + " records: " + (DateTime.Now - start));
        start = DateTime.Now;
        key = 1999;
        for (i = 0; i < nRecords; i++) { 
            key = (3141592621L*key + 2718281829L) % 1000000007L;
            int intKey = (int)((ulong)key >> 32);            
            String strKey = Convert.ToString((int)key);
            Record rec = (Record)root.Get(new Key(new Object[]{intKey, strKey}));
            Assert.That(rec != null && rec.intKey == intKey && rec.strKey.Equals(strKey));
            Assert.That(root.Contains(rec));
            root.Remove(rec);
            rec.Deallocate();
        }
        Assert.That(!root.GetEnumerator().MoveNext());
        Assert.That(!root.GetEnumerator(null, null, IterationOrder.DescentOrder).MoveNext());
        Assert.That(!root.GetEnumerator(null, null, IterationOrder.AscentOrder).MoveNext());
        Console.WriteLine("Elapsed time for deleting " + nRecords + " records: " + (DateTime.Now - start));
        db.Close();
    }
}
