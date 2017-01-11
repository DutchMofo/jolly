using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Jolly
{
	enum ReferenceType
	{
		NONE     = 0,
		VARIABLE = 1,
		POINTER  = 2,
		ARRAY    = 3,
		SLICE    = 4,
	};
	
	struct DataTypeFetcher
	{
		public ReferenceType reference_type;
		public DataType referenced;
	}
	
	class DataType
	{
		// TODO: Remove someday
		public string name;
		
		static int last_type_id;
		static Dictionary<DataTypeFetcher, DataType>
			allTypes = new Dictionary<DataTypeFetcher, DataType>();
		
		public int size, align, type_id;
		public bool is_baseType;
		public TableFolder scope;
		
		public ReferenceType reference_type;
		public DataType referenced;
		
		public DataType(TableFolder scope)
		{
			this.scope = scope;
		}
		
		public DataType(int size, int align)
		{
			// allTypes.Add(new );
			this.type_id = last_type_id++;
			this.align = align;
			this.size = size;
		}
		
		public DataType getSibling(string name)
		{
			return scope.getChild(name)?.type;
		}
		
		public override string ToString()
			=> reference_type == ReferenceType.NONE ? name : referenced.ToString() + '*'; // TODO: Not all references are pointers but it works for now
		
		public override int GetHashCode()
		{
			return reference_type == ReferenceType.NONE ?
				type_id :
				referenced.GetHashCode() & ((int)reference_type << 16);
		}
		
		public override bool Equals(Object obj)
		{
			Debug.Assert(obj is DataTypeFetcher);
			DataTypeFetcher other = (DataTypeFetcher)obj;
			return other.reference_type == this.reference_type & other.referenced == this.referenced;
		}
	}
	
	[Flags]
	enum NameFlags
	{
		NONE		= 0,
		FOLDER		= 1<<0,
		STATIC		= 1<<1,
		READ_ONLY	= 1<<2,
		UNION		= 1<<3,
		IS_TYPE		= 1<<4,
		IS_BASETYPE = 1<<5,
		IS_PURE		= 1<<6,
	};
	
	class TableItem
	{
		public TableFolder parent;
		public NameFlags flags;
		public DataType type;
		public int offset, size, align;
		
		public virtual void calculateSize(Stack<TableFolder> typeStack) { }
		public TableItem(DataType type) { this.type = type; }
	}
		
	class TableFolder : TableItem
	{
		public Dictionary<string, TableItem> children = new Dictionary<string, TableItem>();
		public static TableFolder root = new TableFolder();
		
		public TableFolder() : base(null) { }
		
		public void PrintTree(string path, int space)
		{
			foreach(var child in children)
			{
				TableFolder folder = child.Value as TableFolder;
				if(folder != null) {
					string tPath = path + '/' + child.Key;
					Console.WriteLine("{0} [{1}]".fill(tPath, folder.flags));
					folder.PrintTree(tPath, path.Length + 1);
				} else {
					Console.WriteLine("{0}{1} ({2})[{3}]".fill(new string(' ', space), child.Key, child.Value.type, child.Value.flags));
				}
			}
		}
		
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
		
		public bool Add(string childName, TableItem child)
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