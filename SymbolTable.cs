using System.Collections.Generic;
using System;

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
	
	class Symbol
	{
		public Symbol() { }
		public Symbol(SymbolTable parent) { this.parent = parent; }
		public IR declaration;
		public SymbolTable parent;
		
		public virtual Symbol searchSymbol(string name) => null;
		public virtual Symbol getChildSymbol(string name) => null;
	}
	
	class SymbolTable : Symbol
	{
		public Dictionary<string, Symbol> children = new Dictionary<string, Symbol>();
		
		public List<IR_Allocate> allocations = new List<IR_Allocate>();
		public bool canAllocate;
		
		
		public SymbolTable(SymbolTable parent) : base(parent) { }
		
		public override Symbol searchSymbol(string name)
		{
			SymbolTable iterator = this;
			Symbol item;
			do {
				if(iterator.children.TryGetValue(name, out item))
					return item;
				iterator = iterator.parent;
			} while(iterator != null);
			return null;
		}
		
		public override Symbol getChildSymbol(string name)
		{
			Symbol item;
			if(children.TryGetValue(name, out item))
				return item;
			return null;
		}
		
		public bool Add(string childName, Symbol definition)
		{
			SymbolTable iterator = this;
			do {
				if(iterator.children.ContainsKey(childName)) {
					return false;
				}
				iterator = iterator.parent;
			} while(iterator != null);
			
			children.Add(childName, definition);
			return true;
		}
		
		public IR_Allocate allocateVariable()
		{
			// Bubble allocations to functions or global scope
			if(canAllocate) {
				var alloc = new IR_Allocate();
				allocations.Add(alloc);
				return alloc;
			}
			return parent.allocateVariable();
		}
	}
}