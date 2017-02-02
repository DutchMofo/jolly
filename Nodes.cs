using System.Collections.Generic;
using System;

namespace Jolly
{
    using NT = AST_Node.Type;

	struct Value
	{
		public enum Trigger : byte
		{
			STORE = 1<<0,
			LOAD  = 1<<1,
		}
		
		public Action<Value> onStore;
		public Action<Value> onLoad;
		
		public enum Kind : byte
		{
			UNDEFINED = 0,
			STATIC_TYPE,
			STATIC_VALUE,
			STATIC_FUNCTION,
			VALUE,
			ADDRES,
		}
		
		public Trigger triggers;
		public DataType type;
		public object data;
		public int tempID;
		public Kind kind;
		
		public override string ToString() => (kind == Kind.STATIC_VALUE) ?
			"{0} {1}".fill(type, data) :
			"{0} %{1}".fill(type, tempID);
	}
	
	class AST_Node
	{
		public AST_Node() { }
		public AST_Node(SourceLocation l, Type nT) { nodeType = nT; location = l; }
		
		public enum Type
		{
			UNITIALIZED = 0,
			BASETYPE,
			STRUCT,
			UNION,
			ENUM,
			
			DEFINITION,
			FUNCTION_DEFINITION,
			FUNCTION,
			
			NAME,
			MEMBER_NAME,
			TUPLE,
			MEMBER_TUPLE,
			GLOBAL,
			MODIFY_TYPE,
			LOGIC,
			
			ALIAS,
			BLOCK,
			BREAK,
			CAST,
			FOR,
			FUNCTION_CALL,
			GOTO,
			IF,
			ELSE,
			LABEL,
			LITERAL,
			LOOP_CONTROL,
			OPERATOR,
			RETURN,
			USING,
			WHILE,
		}
		
		public SourceLocation location;
		public Type nodeType;
		public Value result;
		
		public override string ToString()
			=> "{0}:{1} {2}".fill(location.line, location.column, nodeType);
	}
	
	class AST_Return : AST_Node
		
	{
		public AST_Return(SourceLocation loc, AST_Node returns)
			: base(loc, NT.RETURN)
		{
			this.values = returns;
		}
		public AST_Node values;
	}
	
	class AST_Logic : AST_Node
	{
		public AST_Logic(SourceLocation loc, OperatorType operation, int memberCount, int count, AST_Node condition, AST_Node a, AST_Node b)
			: base(loc, NT.LOGIC)
		{
			this.memberCount = memberCount;
			this.condition = condition;
			this.operation = operation;
			this.count = count;
			this.a = a;
			this.b = b;
		}
		
		public int memberCount, count;
		public OperatorType operation;
		public AST_Node condition, a, b;
		public override string ToString()
			=> base.ToString() + " " + operation;
	}
	
	class AST_ModifyType : AST_Node
	{
		public AST_ModifyType(SourceLocation loc, AST_Node target, byte toType)
			: base(loc, NT.MODIFY_TYPE)
		{
			this.toType = toType;
			this.target = target;
		}
		// TODO: Should i put these in an enum to be consistant?
		public const byte TO_POINTER = 1, TO_ARRAY = 2, TO_SLICE = 3, TO_NULLABLE = 4;
		public AST_Node target;
		public byte toType;
	}
	
	
	class AST_Symbol : AST_Node
	{
		public AST_Symbol(SourceLocation loc, NT type) : base(loc, type) { }
		public AST_Symbol(SourceLocation loc, Symbol symbol, string name, NT type = NT.NAME)
			: base(loc, type) { this.text = name; }
		
		public Symbol symbol;
		public string text;
	}
	
	class AST_Definition : AST_Scope
	{
		public AST_Definition(SourceLocation loc, AST_Node typeFrom)
			: base(loc, NT.DEFINITION) { this.typeFrom = typeFrom; }
		
		public AST_Definition(SourceLocation loc, AST_Node typeFrom, SymbolTable scope, string name)
			: base(loc, NT.DEFINITION, scope, name) { this.typeFrom = typeFrom; }
		
		public IR_Allocate allocation;
		public AST_Node typeFrom;
	}
	
	class AST_FunctionCall : AST_Node
	{
		public AST_FunctionCall(SourceLocation loc, AST_Node f, AST_Node[] a)
			: base(loc, NT.FUNCTION_CALL) { arguments = a; function = f; }
		
		public AST_Node[] arguments;
		public AST_Node function;
	}
	
	class AST_Scope : AST_Symbol
	{
		public AST_Scope(SourceLocation loc, NT type) : base(loc, type) { }
		public AST_Scope(SourceLocation loc, NT type, SymbolTable scope, string name)
			: base(loc, scope, name, type) { }
			
		public int memberCount;
	}
	
	class AST_Function : AST_Scope
	{
		public AST_Function(SourceLocation loc) : base(loc, NT.FUNCTION) { }
		public AST_Function(SourceLocation loc, SymbolTable scope, string name)
			: base(loc, Type.FUNCTION, scope, name) {  }
		
		public int returnCount, definitionCount, finishedArguments;
		public AST_Node returns;
	}
		
	class AST_Tuple : AST_Node
	{
		public AST_Tuple(SourceLocation loc, NT tupType)
			: base(loc, tupType) { }
		
		public List<AST_Node> values = new List<AST_Node>();
		public AST_Node membersFrom;
		public int memberCount;
		public bool closed;
	}
		
	class AST_Operation : AST_Node
	{
		public AST_Operation(SourceLocation loc, OperatorType operation, AST_Node a, AST_Node b)
			: base(loc, Type.OPERATOR)
		{
			this.operation = operation;
			this.a = a;
			this.b = b;
		}
		
		public override string ToString() { return base.ToString() + " " + operation.ToString(); }
		
		public OperatorType operation;
		public AST_Node a, b;
	}
}