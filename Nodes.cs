using System.Collections.Generic;
using System;

namespace Jolly
{
    using NT = AST_Node.Type;
	using Hook = Func<AST_Node, AST_Node, List<IR>, bool>;
	
	struct Value
	{
		public enum Kind : byte
		{
			UNDEFINED = 0,
			STATIC_TYPE,
			STATIC_VALUE,
			STATIC_FUNCTION,
			VALUE,
			ADDRES,
		}
		
		public DataType type;
		public object data;
		public int tempID;
		public Kind kind;
		
		public override string ToString() => (kind == Kind.STATIC_VALUE) ?
			"{0} {1}".fill(type, data?.ToString().ToLower()) :
			"{0} %{1}".fill(type, tempID);
	}
	
	class AST_Node
	{
		public AST_Node() { }
		public AST_Node(SourceLocation l, Type nT) { nodeType = nT; location = l; }
		
		public enum Type
		{
			UNDEFINED = 0,
			BASETYPE,
			STRUCT,
			UNION,
			ENUM,
			
			DEFINITION,
			FUNCTION_DEFINITION,
			FUNCTION,
			
			NAME,
			MEMBER_NAME,
			OBJECT_MEMBER_NAME,
			TUPLE,
			MEMBER_TUPLE,
			GLOBAL,
			MODIFY_TYPE,
			LOGIC,
			OBJECT,
			
			ALIAS,
			BLOCK,
			BREAK,
			FOR,
			FUNCTION_CALL,
			GOTO,
			IF,
			LABEL,
			LITERAL,
			LOOP_CONTROL,
			RETURN,
			USING,
			WHILE,
			
			/*##############
				Operators
			##############*/
			REFERENCE,
			DEREFERENCE,
			PLUS,
			MINUS,
			LOGIC_AND,
			LOGIC_OR,
			LOGIC_NOT,
			BIT_NOT,
			BIT_AND,
			BIT_OR,
			BIT_XOR,
			MODULO,
			DIVIDE,
			MULTIPLY,
			GET_MEMBER,
			SUBSCRIPT,
			READ,
			ASSIGN,
			SHIFT_LEFT,
			SHIFT_RIGHT,
			SLICE,
			CAST,
			BITCAST,
			LESS,
			GREATER,
			NEW,
			DELETE,
			
			TERNARY,
			TERNARY_SELECT,
			/*##########################
				Compound assignment
			##########################*/
			AND_ASSIGN,
			OR_ASSIGN,
			ASTERISK_ASSIGN,
			MINUS_ASSIGN,
			PLUS_ASSIGN,
			SLASH_ASSIGN,
			PERCENT_ASSIGN,
			CARET_ASSIGN,
			/*##########################
				Relational operators 
			##########################*/
			EQUAL_TO,
			NOT_EQUAL,	
			LESS_EQUAL,
			GREATER_EQUAL,
			/*########################
				Not really operators
			########################*/
			LAMBDA,
			COLON,
			
			INITIALIZER,
			TYPE_TO_REFERENCE,
			BRACKET_OPEN,
			BRACKET_CLOSE,
			BRACE_OPEN,
			BRACE_CLOSE,
			PARENTHESIS_OPEN,
			PARENTHESIS_CLOSE,
			COMMA,
		}
		
		public SourceLocation location;
		public Type nodeType;
		public Value result;
		
		public Hook onUsed, infer;
		
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
	
	class AST_Logic : AST_Operation
	{
		public AST_Logic(SourceLocation loc, NT operation, int memberCount, int count, AST_Node condition, AST_Node a, AST_Node b)
			: base(loc, operation, a, b, true)
		{
			this.memberCount = memberCount;
			this.condition = condition;
			this.count = count;
		}
		
		public int memberCount, count, trueLabelId, falseLabelId;
		public AST_Node condition;
	}
	
	class AST_If : AST_Node
	{
		public AST_If(SourceLocation loc) : base(loc, NT.IF) { }
		
		public AST_Node condition;
		public int conditionCount, ifCount, elseCount,
			trueLabelId, falseLabelId, endLabelId;
		public SymbolTable ifScope, elseScope;
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
	
	class AST_Object : AST_Node
	{
		public AST_Object(SourceLocation loc, NT type) : base(loc, type) { }
		public int memberCount, startIndex;
		public AST_Node inferFrom;
		public bool isArray;
	}
	
	class AST_Symbol : AST_Node
	{
		public AST_Symbol(SourceLocation loc, NT type) : base(loc, type) { }
		public AST_Symbol(SourceLocation loc, Symbol symbol, string name, NT type = NT.NAME)
			: base(loc, type) { this.text = name; }
		
		public Symbol symbol;
		public string text;
	}
	
	class AST_Declaration : AST_Scope
	{
		public AST_Declaration(SourceLocation loc, AST_Node typeFrom)
			: base(loc, NT.DEFINITION) { this.typeFrom = typeFrom; }
		
		public AST_Declaration(SourceLocation loc, AST_Node typeFrom, SymbolTable scope, string name)
			: base(loc, NT.DEFINITION, scope, name) { this.typeFrom = typeFrom; }
		
		public IR_Allocate allocation;
		public AST_Node typeFrom;
		// public string[] names;
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
	
	class AST_Struct : AST_Scope
	{
		public AST_Struct(SourceLocation loc) : base(loc, NT.STRUCT) { }
		public AST_Node inherits;
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
		public AST_Operation(SourceLocation loc, NT operation, AST_Node a, AST_Node b, bool leftToRight)
			: base(loc, operation)
		{
			this.leftToRight = leftToRight;
			this.a = a;
			this.b = b;
		}
		
		public bool leftToRight;
		public AST_Node a, b;
	}
}