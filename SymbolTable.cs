using System.Collections.Generic;
using System;
using System.Diagnostics;

namespace Jolly
{	
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
	
	struct Symbol
	{
		public DataType dataType;
		public TypeKind typeKind;
	}
	
	class SymbolTable
	{
		public virtual Symbol? getDefinition(string name) { Debug.Assert(false); return null; }
		public virtual bool addDefinition(string name) { Debug.Assert(false); return false; }
		
		public virtual void finishDefinition(string name, DataType type) { Debug.Assert(false); }
	}
	
	class Scope : SymbolTable
	{
		Scope parent;
		public DataType dataType;
		public int childVariableCount;
		public Dictionary<string, Symbol> children = new Dictionary<string, Symbol>();
		
		public Scope(Scope parent)
			 { this.parent = parent; }
		
		public Symbol? searchItem(string name)
		{
			Scope iterator = this;
			Symbol item;
			do {
				if(iterator.children.TryGetValue(name, out item))
					return item;
				iterator = iterator.parent;
			} while(iterator != null);
			return null;
		}
		
		public override void finishDefinition(string name, DataType type)
		{
			// WTF c# why doesn't children[name].dataType = type; work
			var t = children[name];
			t.dataType = type;
			children[name] = t;
		}
		
		public override Symbol? getDefinition(string name)
		{
			Symbol item;
			if(children.TryGetValue(name, out item))
				return item;
			return null;
		}
		
		public bool Add(string childName, DataType child, TypeKind typeKind)
		{
			Scope iterator = this;
			do {
				if(iterator.children.ContainsKey(childName))
					return false;
				iterator = iterator.parent;
			} while(iterator != null);
			
			children.Add(childName, new Symbol{ dataType = child, typeKind = typeKind });
			return true;
		}
	}
}