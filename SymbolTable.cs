using System.Collections.Generic;
using System.Linq;

namespace Jolly
{
	using NT = Node.NodeType;
	
	enum NameFlags
	{
		FOLDER		= 1<<0,
		STATIC		= 1<<1,
		READ_ONLY	= 1<<2,
		UNION		= 1<<3,
		IS_TYPE		= 1<<4,
	}
	
	enum ReferenceType
	{
		POINTER	= 1,
		ARRAY	= 2,
		SLICE	= 3,
	}
	
	class TableItem
	{
		public int size, align, offset;
		public TableFolder parent;
		public NameFlags flags;
		public TableItem type;
		public string name; // Debug only
		
		static List<TableReference> referenceItems = new List<TableItem>();
		
		public static TableReference getReference(TableItem baseItem, ReferenceType reference)
		{
			foreach(var referenceItem in referenceItems)
				if(referenceItem.baseItem == baseItem && referenceItem.reference == reference)
					return reference;
			
			var newItem = new TableReference(baseItem, reference);
			referenceItems.Add(newItem);
			return newItem;
		}
		
		public virtual void calculateSize() { }
		public TableItem(int size) { this.size = align = size; }
		public TableItem(int size, int align) { this.size = size; this.align = align; }
		
		// Debug print tree
		string qF(int number) => (number < 10 & number >= 0) ? " " + number.ToString() : number.ToString();
		protected string info(int indent) => "[{0}:{1}:{2}] ".fill(qF(offset), qF(align), qF(size)) + new string(' ', indent * 2);
		public virtual void PrintTree(int indent)
			=> System.Console.WriteLine(info(indent) + name);
	}
	
	class TableFolder : TableItem
	{
		// Debug print tree
		public override void PrintTree(int indent) {
            System.Console.WriteLine(info(indent) + name + '/');
			foreach(var i in children.Values)
				i.PrintTree(indent+1);
		}
		
		Dictionary<string, TableItem> children = new Dictionary<string, TableItem>();
		
		public TableFolder() { flags = NameFlags.FOLDER; }
		public TableFolder(NameFlags flags) { flags = NameFlags.FOLDER | flags; }
		
		public bool addChild(string childName, TableItem child)
		{
			TableFolder iterator = this;
			while(iterator != null) {
				if(iterator.children.ContainsKey(childName))
					return false;
				iterator = iterator.parent;
			}
			children.Add(childName, child);
			child.name = childName;
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
					if(child.size > size)
						size = child.size;
					if(child.align > align)
						align = child.align;
				}
			}
			else
			{
				int _offset = 0;
				foreach(var child in children.Values)
				{
					child.calculateSize();
					if(child.size == 0)
						continue;
					if(child.align > align)
						align = child.align;
					child.offset = _offset / child.align * child.align - _offset;
					_offset = child.offset + child.size; 
				}
				if(align > 0)
					size = ((_offset-1) / align + 1) * align;
			}
		}
	}
}