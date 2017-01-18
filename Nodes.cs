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
			
			MODIFY_TYPE,
			SCOPE_END,
			
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
			USING,
			MEMBER_DEFINITION,
			VARIABLE_DEFINITION,
			WHILE,
			
			COUNT // Must be last
		}
		
		public NodeType nodeType;
		public DataType dataType;
		public SourceLocation location;
		
		public override string ToString()
			=> "{0}:{1} {2}".fill(location.line, location.column, nodeType);
			
		public virtual string toDebugText() => "";
	}
	
	class NodeModifyType : Node
	{
		public NodeModifyType(SourceLocation loc, Node target, byte targetType)
			: base(NT.MODIFY_TYPE, loc)
		{
			this.targetType = targetType;
			this.target = target;
		}
		public const byte TO_REFERENCE = 1, TO_ARRAY = 2, TO_SLICE = 3;
		public byte targetType;
		public Node target;
	}
	
	class NodeBaseType : Node
	{
		public NodeBaseType(SourceLocation loc, DataType type)
			: base(NT.BASETYPE, loc) { dataType = type; }
	}
	
	class NodeSymbol : Node
	{
		public NodeSymbol(SourceLocation loc, string name, TableFolder definitionScope, NT type = NT.NAME)
			: base(type, loc) { this.name = name; this.definitionScope = definitionScope; }
		
		public TableFolder definitionScope;
		public int memberCount;
		public string name;
	}
			
	class NodeTupple : Node
	{
		public NodeTupple(SourceLocation loc)
			: base(NodeType.TUPPLE, loc) { }
		
		public List<Node> values = new List<Node>();
		public bool closed;
	}
	
	class NodeResult : Node
	{
		public NodeResult(SourceLocation loc, NT type = NT.RESULT)
			: base(type, loc) {  }
		// public Node resultData;
	}
		
	class NodeOperator : Node
	{
		public NodeOperator(SourceLocation loc, OperatorType operation, Node a, Node b, Node result)
			: base(NodeType.OPERATOR, loc)
		{
			this.operation = operation;
			this.result = result;
			this.a = a;
			this.b = b;
		}
		
		public override string ToString() { return base.ToString() + " " + operation.ToString(); }
		
		public OperatorType operation;
		public Node a, b, result;
		
		public override string toDebugText()
			{ /*Debugger.Break(); */return "{0} = {1} {2} {3}".fill(result.dataType, operation, a.dataType, b?.dataType); }
	}
	
	class NodeLiteral : Node
	{
		public NodeLiteral(SourceLocation loc, object data) : base(NT.LITERAL, loc) { this.data = data; }
		public object data;
	}
	
	class NodeFunctionCall : Node
	{
		public NodeFunctionCall(SourceLocation loc, string f, Node[] a)
			: base(NodeType.FUNCTION_CALL, loc)
		{
			functionName = f;
			arguments = a;
		}
		
		public string functionName;
		public Node[] arguments;
		
		public override string toDebugText()
			=> "call " + functionName;
	}
	
	class NodeFunction : NodeSymbol
	{
		public NodeFunction(SourceLocation loc, string name, TableFolder definitionScope)
			: base(loc, name, definitionScope, NodeType.FUNCTION) {  }
			
		public int returnDefinitionCount, argumentDefinitionCount;
		
		public override string toDebugText()
			=> "function " + name;
	}
}