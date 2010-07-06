namespace Perst.Impl    
{
    using System;
	
    public class XMLExporter
    {
        public XMLExporter(StorageImpl storage, System.IO.StreamWriter writer)
        {
            this.storage = storage;
            this.writer = writer;
        }
		
        public virtual void  exportDatabase(int rootOid)
        {
            writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            writer.Write("<database root=\"" + rootOid + "\">\n");
            exportedBitmap = new int[(storage.currIndexSize + 31) / 32];
            markedBitmap = new int[(storage.currIndexSize + 31) / 32];
            markedBitmap[rootOid >> 5] |= 1 << (rootOid & 31);
            int nExportedObjects;
            do 
            {
                nExportedObjects = 0;
                for (int i = 0; i < markedBitmap.Length; i++)
                {
                    int mask = markedBitmap[i];
                    if (mask != 0)
                    {
                        for (int j = 0, bit = 1; j < 32; j++, bit <<= 1)
                        {
                            if ((mask & bit) != 0)
                            {
                                int oid = (i << 5) + j;
                                exportedBitmap[i] |= bit;
                                markedBitmap[i] &= ~ bit;
                                byte[] obj = storage.get(oid);
                                int typeOid = ObjectHeader.getType(obj, 0);
                                if (typeOid == storage.btreeClassOid)
                                {
                                    exportIndex(oid, obj);
                                }
                                else if (typeOid == storage.btree2ClassOid)
                                {
                                    exportFieldIndex(oid, obj);
                                }
                                else
                                {
                                    ClassDescriptor desc = (ClassDescriptor) storage.lookupObject(typeOid, typeof(ClassDescriptor));
                                    writer.Write(" <" + desc.name + " id=\"" + oid + "\">\n");
                                    exportObject(desc, obj, ObjectHeader.Sizeof, 2);
                                    writer.Write(" </" + desc.name + ">\n");
                                }
                                nExportedObjects += 1;
                            }
                        }
                    }
                }
            }
            while (nExportedObjects != 0);
            writer.Write("</database>\n");
        }
		
        internal void  exportIndex(int oid, byte[] data)
        {
            Btree btree = new Btree(data, ObjectHeader.Sizeof);
            storage.assignOid(btree, oid);
            writer.Write(" <btree-index id=\"" + oid + "\" unique=\"" + (btree.unique?'1':'0') + "\" type=\"" + btree.type + "\">\n");
            btree.export(this);
            writer.Write(" </btree-index>\n");
        }
		
        internal void  exportFieldIndex(int oid, byte[] data)
        {
            Btree btree = new Btree(data, ObjectHeader.Sizeof);
            storage.assignOid(btree, oid);
            writer.Write(" <btree-index id=\"" + oid + "\" unique=\"" + (btree.unique?'1':'0') + "\" class=");
            int offs = exportString(data, Btree.Sizeof);
            writer.Write(" field=");
            exportString(data, offs);
            writer.Write(">\n");
            btree.export(this);
            writer.Write(" </btree-index>\n");
        }
		
        internal void  exportAssoc(int oid, byte[] body, int offs, int size, ClassDescriptor.FieldType type)
        {
            writer.Write("  <ref id=\"" + oid + "\" key=\"");
            if ((exportedBitmap[oid >> 5] & (1 << (oid & 31))) == 0)
            {
                markedBitmap[oid >> 5] |= 1 << (oid & 31);
            }
            switch (type)
            {
                case ClassDescriptor.FieldType.tpBoolean: 
                    writer.Write(body[offs] != 0?"1":"0");
                    break;
				
                case ClassDescriptor.FieldType.tpByte: 
                    writer.Write(System.Convert.ToString((byte) body[offs]));
                    break;
				
                case ClassDescriptor.FieldType.tpSByte: 
                    writer.Write(System.Convert.ToString((sbyte) body[offs]));
                    break;
				
                case ClassDescriptor.FieldType.tpChar: 
                    writer.Write(System.Convert.ToString((char) Bytes.unpack2(body, offs)));
                    break;
				
                case ClassDescriptor.FieldType.tpShort: 
                    writer.Write(System.Convert.ToString(Bytes.unpack2(body, offs)));
                    break;
				
                case ClassDescriptor.FieldType.tpUShort: 
                    writer.Write(System.Convert.ToString((ushort)Bytes.unpack2(body, offs)));
                    break;
				
                case ClassDescriptor.FieldType.tpInt: 
                    writer.Write(System.Convert.ToString(Bytes.unpack4(body, offs)));
                    break;
				
                case ClassDescriptor.FieldType.tpUInt: 
                case ClassDescriptor.FieldType.tpObject:  
                case ClassDescriptor.FieldType.tpEnum:
                    writer.Write(System.Convert.ToString((uint)Bytes.unpack4(body, offs)));
                    break;
				
                case ClassDescriptor.FieldType.tpLong: 
                    writer.Write(System.Convert.ToString(Bytes.unpack8(body, offs)));
                    break;
				
                case ClassDescriptor.FieldType.tpULong: 
                    writer.Write(System.Convert.ToString((ulong)Bytes.unpack8(body, offs)));
                    break;
				
                case ClassDescriptor.FieldType.tpFloat: 
                    writer.Write(System.Convert.ToString(BitConverter.ToSingle(BitConverter.GetBytes(Bytes.unpack4(body, offs)), 0)));
                    break;
				
                case ClassDescriptor.FieldType.tpDouble: 
                    writer.Write(System.Convert.ToString(BitConverter.Int64BitsToDouble(Bytes.unpack8(body, offs))));
                    break;
				
                case ClassDescriptor.FieldType.tpString: 
                    for (int i = 0; i < size; i++)
                    {
                        exportChar((char) Bytes.unpack2(body, offs));
                        offs += 2;
                    }
                    break;
				
                case ClassDescriptor.FieldType.tpDate: 
                {
                    long msec = Bytes.unpack8(body, offs);
                    if (msec >= 0)
                    {
                        writer.Write(new System.DateTime(msec).ToString());
                    }
                    else
                    {
                        writer.Write("null");
                    }
                    break;
                }
	    }
            writer.Write("\"/>\n");
        }
		
        internal void  indentation(int indent)
        {
            while (--indent >= 0)
            {
                writer.Write(' ');
            }
        }
		
        internal void  exportChar(char ch)
        {
            switch (ch)
            {
                case '<': 
                    writer.Write("&lt;");
                    break;
				
                case '>': 
                    writer.Write("&gt;");
                    break;
				
                case '&': 
                    writer.Write("&amp;");
                    break;
				
                case '"': 
                    writer.Write("&quot;");
                    break;
				
                default: 
                    writer.Write(ch);
                    break;
				
            }
        }
		
        internal int exportString(byte[] body, int offs)
        {
            int len = Bytes.unpack4(body, offs);
            offs += 4;
            if (len >= 0)
            {
                writer.Write("\"");
                while (--len >= 0)
                {
                    exportChar((char) Bytes.unpack2(body, offs));
                    offs += 2;
                }
                writer.Write("\"");
            }
            else
            {
                writer.Write("null");
            }
            return offs;
        }
		
        internal static char[] hexDigit = new char[]{'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'};
		
        internal int exportObject(ClassDescriptor desc, byte[] body, int offs, int indent)
        {
            System.Reflection.FieldInfo[] all = desc.allFields;
            ClassDescriptor.FieldType[] type = desc.fieldTypes;
			
            for (int i = 0, n = all.Length; i < n; i++)
            {
                System.Reflection.FieldInfo f = all[i];
                indentation(indent);
                writer.Write("<" + f.Name + ">");
                switch (type[i])
                {
                    case ClassDescriptor.FieldType.tpBoolean: 
                        writer.Write(body[offs++] != 0?"1":"0");
                        break;
					
                    case ClassDescriptor.FieldType.tpByte: 
                        writer.Write(System.Convert.ToString((byte) body[offs++]));
                        break;
					
                    case ClassDescriptor.FieldType.tpSByte: 
                        writer.Write(System.Convert.ToString((sbyte) body[offs++]));
                        break;
					
                    case ClassDescriptor.FieldType.tpChar: 
                        writer.Write(System.Convert.ToString((char) Bytes.unpack2(body, offs)));
                        offs += 2;
                        break;
					
                    case ClassDescriptor.FieldType.tpShort: 
                        writer.Write(System.Convert.ToString(Bytes.unpack2(body, offs)));
                        offs += 2;
                        break;
					
                    case ClassDescriptor.FieldType.tpUShort: 
                        writer.Write(System.Convert.ToString((ushort)Bytes.unpack2(body, offs)));
                        offs += 2;
                        break;
					
                    case ClassDescriptor.FieldType.tpInt: 
                        writer.Write(System.Convert.ToString(Bytes.unpack4(body, offs)));
                        offs += 4;
                        break;
					
                    case ClassDescriptor.FieldType.tpEnum:
                        writer.Write("\"" + Enum.ToObject(f.FieldType, Bytes.unpack4(body, offs)) + "\"");
                        offs += 4;
                        break;

                    case ClassDescriptor.FieldType.tpUInt: 
                        writer.Write(System.Convert.ToString((uint)Bytes.unpack4(body, offs)));
                        offs += 4;
                        break;
					
                    case ClassDescriptor.FieldType.tpLong: 
                        writer.Write(System.Convert.ToString(Bytes.unpack8(body, offs)));
                        offs += 8;
                        break;
					
                    case ClassDescriptor.FieldType.tpULong: 
                        writer.Write(System.Convert.ToString((ulong)Bytes.unpack8(body, offs)));
                        offs += 8;
                        break;
					
                    case ClassDescriptor.FieldType.tpFloat: 
                        writer.Write(System.Convert.ToString(BitConverter.ToSingle(BitConverter.GetBytes(Bytes.unpack4(body, offs)), 0)));
                        offs += 4;
                        break;
				
                    case ClassDescriptor.FieldType.tpDouble: 
                        writer.Write(System.Convert.ToString(BitConverter.Int64BitsToDouble(Bytes.unpack8(body, offs))));
                        offs += 8;
                        break;

                    case ClassDescriptor.FieldType.tpString: 
                        offs = exportString(body, offs);
                        break;
					
                    case ClassDescriptor.FieldType.tpDate: 
                    {
                        long msec = Bytes.unpack8(body, offs);
                        offs += 8;
                        if (msec >= 0)
                        {
                            writer.Write("\"" + new System.DateTime(msec) + "\"");
                        }
                        else
                        {
                            writer.Write("null");
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpObject: 
                    {
                        int oid = Bytes.unpack4(body, offs);
                        writer.Write("<ref id=\"" + oid + "\"/>");
                        if (oid != 0 && (exportedBitmap[oid >> 5] & (1 << (oid & 31))) == 0)
                        {
                            markedBitmap[oid >> 5] |= 1 << (oid & 31);
                        }
                        offs += 4;
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpValue: 
                        writer.Write('\n');
                        offs = exportObject(storage.getClassDescriptor(f.FieldType), body, offs, indent + 1);
                        indentation(indent);
                        break;
					
                    case ClassDescriptor.FieldType.tpArrayOfByte: 
                    case ClassDescriptor.FieldType.tpArrayOfSByte: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\"');
                            while (--len >= 0)
                            {
                                byte b = body[offs++];
                                writer.Write(hexDigit[b >> 4]);
                                writer.Write(hexDigit[b & 0xF]);
                            }
                            writer.Write('\"');
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfBoolean: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<array-element>" + (body[offs++] != 0?"1":"0") + "</array-element>\n");
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfChar: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<array-element>" + (Bytes.unpack2(body, offs) & 0xFFFF) + "</array-element>\n");
                                offs += 2;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfShort: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<array-element>" + Bytes.unpack2(body, offs) + "</array-element>\n");
                                offs += 2;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfUShort: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<array-element>" + (ushort)Bytes.unpack2(body, offs) + "</array-element>\n");
                                offs += 2;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfInt: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<array-element>" + Bytes.unpack4(body, offs) + "</array-element>\n");
                                offs += 4;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfEnum: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            Type elemType = f.FieldType.GetElementType();
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<array-element>\"" + Enum.ToObject(elemType, Bytes.unpack4(body, offs)) + "\"</array-element>\n");
                                offs += 4;
                            }
                            indentation(indent);
                        }
                        break;
                    }

                    case ClassDescriptor.FieldType.tpArrayOfUInt: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<array-element>" + (uint)Bytes.unpack4(body, offs) + "</array-element>\n");
                                offs += 4;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfLong: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<array-element>" + Bytes.unpack8(body, offs) + "</array-element>\n");
                                offs += 8;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfULong: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<array-element>" + (ulong)Bytes.unpack8(body, offs) + "</array-element>\n");
                                offs += 8;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfFloat: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<array-element>" + BitConverter.ToSingle(BitConverter.GetBytes(Bytes.unpack4(body, offs)), 0) + "</array-element>\n");
                                offs += 4;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfDouble: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<array-element>" + BitConverter.Int64BitsToDouble(Bytes.unpack8(body, offs)) + "</array-element>\n");
                                offs += 8;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfDate: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                long msec = Bytes.unpack8(body, offs);
                                offs += 8;
                                if (msec >= 0)
                                {
                                    writer.Write("<array-element>\"" + new System.DateTime(Bytes.unpack8(body, offs)) + "\"</array-element>\n");
                                }
                                else
                                {
                                    writer.Write("<array-element>null</array-element>\n");
                                }
                            }
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfString: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<array-element>");
                                offs = exportString(body, offs);
                                writer.Write("</array-element>\n");
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpLink: 
                    case ClassDescriptor.FieldType.tpArrayOfObject: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                int oid = Bytes.unpack4(body, offs);
                                if (oid != 0 && (exportedBitmap[oid >> 5] & (1 << (oid & 31))) == 0)
                                {
                                    markedBitmap[oid >> 5] |= 1 << (oid & 31);
                                }
                                writer.Write("<array-element><ref id=\"" + oid + "\"/></array-element>\n");
                                offs += 4;
                            }
                            indentation(indent);
                        }
                        break;
                    }
					
                    case ClassDescriptor.FieldType.tpArrayOfValue: 
                    {
                        int len = Bytes.unpack4(body, offs);
                        offs += 4;
                        if (len < 0)
                        {
                            writer.Write("null");
                        }
                        else
                        {
                            writer.Write('\n');
                            while (--len >= 0)
                            {
                                indentation(indent + 1);
                                writer.Write("<array-element>\n");
                                offs = exportObject(storage.getClassDescriptor(f.FieldType), body, offs, indent + 2);
                                indentation(indent + 1);
                                writer.Write("</array-element>\n");
                            }
                            indentation(indent);
                        }
                    }
                        break;
					
                }
                writer.Write("</" + f.Name + ">\n");
            }
            return offs;
        }
		
		
        private StorageImpl storage;
        private System.IO.StreamWriter writer;
        private int[] markedBitmap;
        private int[] exportedBitmap;
    }
}