using System.Collections.Generic;

namespace Jolly
{
    using NT = AST_Node.Type;

	struct Value
	{
		public enum Kind : byte
		{
			UNDEFINED = 0,
			STATIC_TYPE,
			STATIC_VALUE,
			VALUE,
			ADDRES,
		}
		
		public DataType type;
		public Kind kind;
		public int tempID;
		public override string ToString() => "{0} %{1}".fill(type, tempID);
	}
	
	class AST_Node
	{
		public AST_Node(SourceLocation l, Type nT) { nodeType = nT; location = l; }
		
		public enum Type
		{
			UNITIALIZED = 0,
			BASETYPE,
			STRUCT,
			UNION,
			ENUM,
			USERTYPE,
			
			ARGUMENTS,
			RETURN_VALUES,
			NAME,
			MODIFY_TYPE,
			TUPLE,
			MEMBER_TUPLE,
			MEMBER_NAME,
			GLOBAL,
			STATEMENT,
			LOGIC,
			
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
			LITERAL,
			LOOP_CONTROL,
			OPERATOR,
			RETURN,
			USING,
			MEMBER_DEFINITION,
			VARIABLE_DEFINITION,
			WHILE,
			
			COUNT // Must be last
		}
		
		public Value result;
		public Type nodeType;
		public SourceLocation location;
		
		public override string ToString()
			=> "{0}:{1} {2}".fill(location.line, location.column, nodeType);
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
		public const byte TO_POINTER = 1, TO_ARRAY = 2, TO_SLICE = 3;
		public AST_Node target;
		public byte toType;
	}
	
	
	class AST_Symbol : AST_Node
	{
		public AST_Symbol(SourceLocation loc, string name, NT type = NT.NAME)
			: base(loc, type) { this.text = name; }
		
		public int memberCount;
		public string text;
	}
	
	class AST_VariableDefinition : AST_Scope
	{
		public AST_VariableDefinition(SourceLocation loc, Scope scope, string name, AST_Node typeFrom, NT type = NT.VARIABLE_DEFINITION)
			: base(loc, type, scope, name)
		{ this.typeFrom = typeFrom; }
		
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
		public AST_Scope(SourceLocation loc, NT type, Scope scope, string name)
			: base(loc, name, type) { this.scope = scope; }
		
		public Scope scope;
		public Value? getDefinition(string name)
			=> scope.searchItem(name);
	}
	
	class AST_Function : AST_Scope
	{
		public AST_Function(SourceLocation loc, Scope scope, string name)
			: base(loc, Type.FUNCTION, scope, name) {  }
		
		public AST_Node returns;
		public int returnDefinitionCount, argumentDefinitionCount, finishedArguments;
	}
		
	class AST_Tuple : AST_Scope
	{
		public AST_Tuple(SourceLocation loc, Scope scope, NT tupType)
			: base(loc, tupType, scope, null) { }
		
		public List<AST_Node> values = new List<AST_Node>();
		public AST_Node membersFrom;
		public bool closed;
	}
		
	class AST_Operator : AST_Node
	{
		public AST_Operator(SourceLocation loc, OperatorType operation, AST_Node a, AST_Node b)
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
	
	class AST_Literal : AST_Node
	{
		public AST_Literal(SourceLocation loc, object data)
			: base(loc, NT.LITERAL) { this.data = data; }
		public object data;
		public override string ToString()
			=> base.ToString() + " " + data;
	}
}