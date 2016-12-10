using System.Collections.Generic;
using System.Linq;

namespace Jolly
{
	using NT = Node.NodeType;
	
	enum SymbolFlag
	{
		none				= 0,
		overwrite_global	= 1<<0,
		global				= 1<<1,
		private_members		= 1<<2,
	}
	
	class Symbol : Node
	{
		public string name;
		public SymbolFlag flags;
		public Symbol(NodeType type, SourceLocation loc, Scope parentScope, string name, SymbolFlag flags)
			: base(type, loc, parentScope)
		{
			this.name = name;
			this.flags = flags;
		}
	}
	
	class Node
	{
		public Node(NodeType nT, SourceLocation l, Scope s) { nType = nT; location = l; parentScope = s; }
		
		public enum NodeType
		{
			UNITIALIZED		= 0,
			BASETYPE,
			STRUCT,
			ENUM,
			USERTYPE,
			
			ALIAS,
			BLOCK,
			BREAK,
			CAST,
			FOR,
			FUNCTION,
			FUNCTION_CALL,
			GOTO,
			IF,
			IF_ELSE,
			LABEL,
			LIST,
			LITERAL,
			LOOP_CONTROL,
			NAME,
			OPERATOR,
			RESULT,
			RETURN,
			STATEMENT,
			USING,
			VARIABLE,
			VARIABLE_RW,
			WHILE,
			
			COUNT // Must be last
		}
		
		public Node type;
		public NodeType nType;
		public Scope parentScope;
		// public TypeInfo dType;
		public SourceLocation location;
		
		public override string ToString()
		{
			return "{0}:{1} {2}".fill(location.line, location.column, nType);
		}
	}
	
	class _List : Node 
	{
		public _List(SourceLocation loc, Scope parentScope) : base(NodeType.LIST, loc, parentScope) { }
		
		public List<Node> list = new List<Node>();
		public bool locked;
	}
	
	class Result : Node
	{
		public Result(SourceLocation loc, Scope parentScope/*, TypeInfo dType*/) : base(NodeType.RESULT, loc, parentScope) { /*this.dType = dType;*/ }
	}
	
	class Variable : Symbol
	{
		public Variable(SourceLocation loc, Scope parentScope, string name, Node type, SymbolFlag flags)
			: base(NodeType.VARIABLE, loc, parentScope, name, flags) { this.type = type; }
	}
	
	class Operator : Node
	{
		public Operator(SourceLocation loc, Scope parentScope, Token.Type operation, Node a, Node b, Node result)
			: base(NodeType.OPERATOR, loc, parentScope)
		{
			this.operation = operation;
			this.result = result;
			this.a = a;
			this.b = b;
		}
		
		public override string ToString() { return base.ToString() + " " + operation.ToString(); }
		
		public Token.Type operation;
		public Node a, b, result;
	}
	
	class Literal : Node
	{
		public enum LType
		{
			STRING,
			FLOAT,
			INTEGER
		}
		
		public Literal(SourceLocation loc, Scope parentScope, string s) : base(NodeType.LITERAL, loc, parentScope) { lType = LType.STRING; _string = s; }
		public Literal(SourceLocation loc, Scope parentScope, ulong i) : base(NodeType.LITERAL, loc, parentScope) { lType = LType.INTEGER; _integer = i; }
		public Literal(SourceLocation loc, Scope parentScope, double f) : base(NodeType.LITERAL, loc, parentScope) { lType = LType.FLOAT; _float = f; }
		
		public LType lType;
		// union {
		public string _string;
		public ulong _integer;
		public double _float;
		// }
	}
	
	class Function_call : Node
	{
		public Function_call(SourceLocation loc, Scope parentScope, string f, Node[] a)
			: base(NodeType.FUNCTION_CALL, loc, parentScope)
		{
			functionName = f; arguments = a;
		}
		
		public string functionName;
		public Node[] arguments;
	}
	
	class Return : Node
	{
		public Return(SourceLocation loc, Scope parentScope, Node returns) : base(NodeType.RETURN, loc, parentScope) { this.returns = returns; }
		public Node returns;
	}
	
	class Scope : Symbol
	{
		public List<Symbol> symbols = new List<Symbol>();
		
		public Scope(SourceLocation loc, NodeType nType, Scope parentScope, string name, SymbolFlag flags)
			: base(nType, loc, parentScope, name, flags) { }
		
		bool canDefine(Symbol symbol)
		{
			foreach(var sym in symbols) {
				if(sym.name == symbol.name) {
					if((symbol.flags & SymbolFlag.overwrite_global) != 0 && (sym.flags & SymbolFlag.global) != 0)
						continue;
					
					return false;
				}
			}
				
			return parentScope?.canDefine(symbol) ?? true;
		}
				
		public Symbol getSymbol(string name)
		{
			return symbols.FirstOrDefault(s => s.name == name);
		}
		
		public Function getFunction(string name)
		{
			return symbols.FirstOrDefault(s => s.nType == NT.FUNCTION && s.name == name) as Function;
		}
		
		public bool addSymbol(Symbol symbol)
		{
			if(symbols.Any(s => s.name == symbol.name) || !canDefine(symbol))
				return false;
			symbols.Add(symbol);
			return true;
		}
	}
	
	class Function : Scope
	{
		public Variable[] arguments;
		public Node returns;
		// public TypeInfo[] returns;
		
		public Function(SourceLocation loc, Scope scope, string name, Variable[] arguments, SymbolFlag flags)
			: base(loc, NodeType.FUNCTION, scope, name, flags) { this.arguments = arguments; }
		// public Function(SourceLocation loc, string name, Variable[] arguments, SymbolFlag flags, TypeInfo[] returns)
		// 	: base(loc, NodeType.FUNCTION, null, name, flags) { this.arguments = arguments; this.returns = returns; }
		
		public override bool Equals(object obj)
		{
			Function function = obj as Function;
			if( function == null ||
				function.arguments.Length != arguments.Length ||
				function.name != name)
				return false;
				
			for(int i = 0; i < arguments.Length; ++i)
				if(arguments[i].type != function.arguments[i].type)
					return false;
			
			return true;
		}

		public override int GetHashCode()
		{
			return name.GetHashCode();
		}
	}
	
	class If  : Scope
	{
		public If(SourceLocation loc, Node[] condition, Node conditionValue, SymbolFlag flags)
			: base(loc, NodeType.IF, null, null, flags)
		{ 
			this.conditionValue = conditionValue;
			this.condition = condition;
		}
		
		public Node[] condition;
		public Node conditionValue;
	}
	
	class For : Scope
	{
		public For(SourceLocation loc, string name, SymbolFlag flags)
			: base(loc, NodeType.FOR, null, name, flags) { }
		
		public Node[] counter, condition, increment;
		public Node conditionValue;
	}
}