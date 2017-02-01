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
	
	class Scope
	{
		Scope parent;
		public int variableCount;
		public Value scopeType;
		public Dictionary<string, Value> children = new Dictionary<string, Value>();
		
		public Scope(Scope parent)
			 { this.parent = parent; }
		
		public Value? searchItem(string name)
		{
			Scope iterator = this;
			Value item;
			do {
				if(iterator.children.TryGetValue(name, out item))
					return item;
				iterator = iterator.parent;
			} while(iterator != null);
			return null;
		}
		
		public Value? getDefinition(string name)
		{
			Value item;
			if(children.TryGetValue(name, out item))
				return item;
			return null;
		}
		
		public bool Add(string childName, Value definition)
		{
			Scope iterator = this;
			do {
				if(iterator.children.ContainsKey(childName))
					return false;
				iterator = iterator.parent;
			} while(iterator != null);
			
			children.Add(childName, definition);
			return true;
		}
	}
}