using System;
using System.Collections.Generic;

namespace Jolly
{
	enum ReferenceType
	{
		POINTER	= 1,
		ARRAY	= 2,
		SLICE	= 3,
	}
	
	// class DataType
	// {
	// 	public int size, align;
	// 	public bool is_baseType;
	// 	public TableFolder definedInScope;
		
	// 	public DataType() { System.Diagnostics.Debug.Assert(this as DataReferenceType != null); }
	// 	public DataType(int size, int align) { this.size = size; this.align = align; }
	// }
	
	// class DataReferenceType : DataType
	// {
	// 	public DataReferenceType(DataType referenced, ReferenceType reference)
	// 	{
	// 		this.referenced = referenced;
	// 		int pSize = Jolly.SIZE_T_BYTES;
	// 		switch(reference) {
	// 			case ReferenceType.POINTER:	size = pSize;		align = pSize; break;
	// 			case ReferenceType.ARRAY:	size = pSize * 2;	align = pSize; break;
	// 			case ReferenceType.SLICE:	size = pSize * 2;	align = pSize; break;
	// 			default: throw new ParseException();
	// 		}
	// 	}
	// 	public ReferenceType reference;
	// 	public DataType referenced;
	// }
	
	[Flags]
	enum NameFlags
	{
		FOLDER		= 1<<0,
		STATIC		= 1<<1,
		READ_ONLY	= 1<<2,
		UNION		= 1<<3,
		IS_TYPE		= 1<<4,
		IS_BASETYPE = 1<<5,
		IS_PURE		= 1<<6,
	}
	
	class TableItem
	{
		public TableFolder parent;
		public NameFlags flags;
		public TableItem type;
		public int offset, size, align;
		
		public virtual void calculateSize(Stack<TableFolder> typeStack) { }
		public TableItem(TableItem type) { this.type = type; }
	}
		
	class TableFolder : TableItem
	{
		public Dictionary<string, TableItem> children = new Dictionary<string, TableItem>();
		public static TableFolder root = new TableFolder();
		
		public TableFolder() : base(null) { }
				
		public TableItem searchItem(string name)
		{
			TableFolder iterator = this;
			TableItem item;
			do {
				if(iterator.children.TryGetValue(name, out item))
					return item;
				iterator = iterator.parent;
			} while(iterator != null);
			return null;
		}
		
		public TableItem getChild(string name)
		{
			TableItem item;
			children.TryGetValue(name, out item);
			return item;
		}
		
		public bool addChild(string childName, TableItem child)
		{
			TableFolder iterator = this;
			do {
				if(iterator.children.ContainsKey(childName))
					return false;
				iterator = iterator.parent;
			} while(iterator != null);
			
			if((child.flags & NameFlags.IS_BASETYPE) != 0)
				flags &= ~NameFlags.IS_PURE;
			
			children.Add(childName, child);
			child.parent = this;
			return true;
		}
		
		/*
		public override void calculateSize(Stack<TableFolder> typeStack)
		{
			if(typeStack.Contains(this)) {
				Jolly.addError(new SourceLocation(), "Recursive type");
				throw new ParseException();
			}
			typeStack.Push(this);
			
			if((flags & NameFlags.UNION) != 0)
			{
				foreach(var child in children.Values)
				{
					child.calculateSize(typeStack);
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
					child.calculateSize(typeStack);	
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
            System.Diagnostics.Debug.Assert(typeStack.Pop() == this);
		}
		*/
	}
}