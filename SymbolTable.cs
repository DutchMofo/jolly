using System.Collections.Generic;

namespace Jolly
{
	enum ReferenceType
	{
		POINTER	= 1,
		ARRAY	= 2,
		SLICE	= 3,
	}
	
	class DataType
	{
		public int size, align;
		
		public DataType() { System.Diagnostics.Debug.Assert(this as DataReferenceType != null); }
		public DataType(int size, int align) { this.size = size; this.align = align; }
	}
	
	class DataReferenceType : DataType
	{
		public DataReferenceType(DataType referenced, ReferenceType reference)
		{
			this.referenced = referenced;
			int pSize = Jolly.SIZE_T_BYTES;
			switch(reference) {
				case ReferenceType.POINTER:	size = pSize;		align = pSize; break;
				case ReferenceType.ARRAY:	size = pSize * 2;	align = pSize; break;
				case ReferenceType.SLICE:	size = pSize * 2;	align = pSize; break;
				default: throw new ParseException();
			}
		}
		public ReferenceType reference;
		public DataType referenced;
	}
	
	enum NameFlags
	{
		FOLDER			= 1<<0,
		STATIC			= 1<<1,
		READ_ONLY		= 1<<2,
		UNION			= 1<<3,
		IS_TYPE			= 1<<4,
	}
	
	class TableItem
	{
		public static TableFolder root = new TableFolder();
		
		public TableFolder parent;
		public NameFlags flags;
		public DataType type;
		public int offset;
		
		public virtual void calculateSize() { }
		public TableItem(DataType type) { this.type = type; }
	}
		
	class TableFolder : TableItem
	{
		Dictionary<string, TableItem> children = new Dictionary<string, TableItem>();
		
		public TableFolder() : base(null) { flags = NameFlags.FOLDER; }
		public TableFolder(NameFlags flags) : base(null) { flags = NameFlags.FOLDER | flags; }
		
		public bool addChild(string childName, TableItem child)
		{
			TableFolder iterator = this;
			while(iterator != null) {
				if(iterator.children.ContainsKey(childName))
					return false;
				iterator = iterator.parent;
			}
			children.Add(childName, child);
			child.parent = this;
			return true;
		}
		
		public override void calculateSize()
		{
			if((flags & NameFlags.UNION) != 0)
			{
				foreach(var child in children.Values)
				{
					child.calculateSize();
					// child.offset = 0; // Not nessesary
					if(child.type.size > type.size)
						type.size = child.type.size;
					if(child.type.align > type.align)
						type.align = child.type.align;
				}
			}
			else
			{
				int _offset = 0;
				foreach(var child in children.Values)
				{
					child.calculateSize();
					if(child.type.size == 0)
						continue;
					if(child.type.align > type.align)
						type.align = child.type.align;
					child.offset = _offset / child.type.align * child.type.align - _offset;
					_offset = child.offset + child.type.size; 
				}
				if(type.align > 0)
					type.size = ((_offset-1) / type.align + 1) * type.align;
			}
		}
	}
}