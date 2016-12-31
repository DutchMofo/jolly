using System.Collections.Generic;

namespace Jolly
{
	using NT = Node.NodeType;
	// using TT = Token.Type;
	
	class Node
	{
		public Node(NodeType nT, SourceLocation l) { nodeType = nT; location = l; }
		
		public enum NodeType
		{
			UNITIALIZED		= 0,
			BASETYPE,
			STRUCT,
			UNION,
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
			TUPPLE,
			LITERAL,
			LOOP_CONTROL,
			NAME,
			OPERATOR,
			RESULT,
			RETURN,
			STATEMENT,
			USING,
			VARIABLE_DEFINITION,
			VARIABLE_RW,
			WHILE,
			
			COUNT // Must be last
		}
		
		public NodeType nodeType;
		public DataType dataType;
		public SourceLocation location;
		
		public override string ToString()
		{
			return "{0}:{1} {2}".fill(location.line, location.column, nodeType);
		}
	}
	
	class BaseType : Node
	{
		public BaseType(SourceLocation loc, DataType type)
			: base(NT.BASETYPE, loc) { dataType = type; }
	}
	
	class Symbol : Node
	{
		public Symbol(SourceLocation loc, string name, TableFolder definitionScope, NT type = NT.NAME)
			: base(type, loc) { this.name = name; this.definitionScope = definitionScope; }
		public string name;
		public TableFolder definitionScope;
	}
		
	class Tupple : Node 
	{
		public Tupple(SourceLocation loc)
			: base(NodeType.TUPPLE, loc) { }
		
		public List<Node> list = new List<Node>();
		public bool locked;
	}
	
	class Result : Node
	{
		public Result(SourceLocation loc, NT type = NT.RESULT)
			: base(type, loc) {  }
		public Node resultData;
	}
		
	class Operator : Node
	{
		public Operator(SourceLocation loc, Token.Type operation, Node a, Node b, Node result)
			: base(NodeType.OPERATOR, loc)
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
		public Literal(SourceLocation loc, object data) : base(NT.LITERAL, loc) { this.data = data; }
		public object data;
	}
	
	class Function_call : Node
	{
		public Function_call(SourceLocation loc, string f, Node[] a)
			: base(NodeType.FUNCTION_CALL, loc)
		{
			functionName = f; arguments = a;
		}
		
		public string functionName;
		public Node[] arguments;
	}
	
	class Function : Node
	{
		public Function(SourceLocation loc, Symbol[] arguments, TableFolder parentScope)
			: base(NodeType.FUNCTION, loc) { this.arguments = arguments; }
		
		public Symbol[] arguments;
		public Node returns;
	}
}