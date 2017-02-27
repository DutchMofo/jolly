using System.Collections.Generic;

namespace Jolly
{
	class Symbol
	{
		public Symbol(SymbolTable parent) { this.parent = parent; }
		public SymbolTable parent;
		public Symbol extends;
		public IR declaration;
		public bool isGeneric;
		public int defineIndex;
		
		public virtual Symbol searchSymbol(string name) => null;
		public virtual Symbol getChildSymbol(string name) => null;
	}
	
	class SymbolTable : Symbol
	{
		public SymbolTable(SymbolTable parent) : base(parent) { }
		
		public Dictionary<string, Symbol>       children = new Dictionary<string, Symbol>();
		public Dictionary<string, TemplateItem> template = new Dictionary<string, TemplateItem>();
		
		public TemplateItem getTemplate(string name)
		{
			SymbolTable iterator = this;
			TemplateItem item;
			do {
				if(iterator.template.TryGetValue(name, out item))
					return item;
				iterator = iterator.parent;
			} while(iterator != null);
			return null;
		}
		
		public override Symbol searchSymbol(string name)
		{
			SymbolTable iterator = this;
			Symbol item;
			do {
				if((item = iterator.getChildSymbol(name)) != null)
					return item;
				iterator = iterator.parent;
			} while(iterator != null);
			return null;
		}
		
		public override Symbol getChildSymbol(string name)
		{
			Symbol item;
			if(children.TryGetValue(name, out item)) return item;
			return null;
		}
		
		public bool addChild(string name, Symbol definition, bool allowCollision = false)
		{
			Symbol item;
			if(children.TryGetValue(name, out item)) {
				if(!allowCollision) return false;
				definition.extends = item.extends;
				item.extends = definition;
				return true;
			}
			
			SymbolTable iterator = parent;
			while(iterator != null)
			{
				if(iterator.children.TryGetValue(name, out item)) {
					if(!allowCollision) return false;
					definition.extends = item;
					break;
				}
				iterator = iterator.parent;
			}
			children.Add(name, definition);
			return true;
		}
	}

	class FunctionTable : SymbolTable
	{
		public FunctionTable(SymbolTable parent) : base(parent) { }	
		public Dictionary<string, Symbol> arguments = new Dictionary<string, Symbol>();
	}
}
