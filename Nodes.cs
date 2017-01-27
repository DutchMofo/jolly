using System.Collections.Generic;

namespace Jolly
{
	using NT = Node.NodeType;
	// using TT = Token.Type;
	
	enum TypeKind
	{
		UNDEFINED,
		STATIC,
		VALUE,
		ADDRES
	}
	
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
			
			ARGUMENTS,
			RETURN_VALUES,
			NAME,
			MODIFY_TYPE,
			TUPPLE,
			MEMBER_NAME,
			GLOBAL,
			MEMBER_TUPPLE_NAME,
			
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
		
		public TypeKind typeKind;
		public NodeType nodeType;
		public DataType dataType;
		public SourceLocation location;
		
		public override string ToString()
			=> "{0}:{1} {2}".fill(location.line, location.column, nodeType);
			
		public virtual string toDebugText()
			=> (nodeType == NT.RETURN) ? "return" : "";
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
	
	class NodeSymbol : Node
	{
		public NodeSymbol(SourceLocation loc, string name, Scope definitionScope, NT type = NT.NAME)
			: base(type, loc) { this.text = name; this.scope = definitionScope; }
		
		public Scope scope;
		public string text;
		public int memberCount;
		
		public override string toDebugText()
			=> "define " + text;
	}
	
	class NodeVariableDefinition : NodeSymbol
	{
		public NodeVariableDefinition(SourceLocation loc, string name, Scope definitionScope, Node typeFrom, NT type = NT.VARIABLE_DEFINITION)
			: base(loc, name, definitionScope, type)
		{ this.typeFrom = typeFrom; }
		
		public Node typeFrom;
	}
	
	class NodeFunctionCall : NodeSymbol
	{
		public NodeFunctionCall(SourceLocation loc, string function, Scope definitionScope, Node[] a)
			: base(loc, function, definitionScope, NT.FUNCTION_CALL)
		{
			arguments = a;
		}
		
		public Node[] arguments;
		
		public override string toDebugText()
			=> "call " + text;
	}
	
	class NodeFunction : NodeSymbol
	{
		public NodeFunction(SourceLocation loc, string name, Scope definitionScope)
			: base(loc, name, definitionScope, NodeType.FUNCTION) {  }
			
		public int returnDefinitionCount, argumentDefinitionCount;
		
		public override string toDebugText()
			=> "function " + text;
	}
	
	class NodeEnclosure : Node
	{
		public NodeEnclosure(SourceLocation loc, NT type)
			: base(type, loc) { }
		public int memberCount;
	}
		
	class NodeTupple : Node
	{
		public NodeTupple(SourceLocation loc)
			: base(NodeType.TUPPLE, loc) { }
		
		public List<Node> values = new List<Node>();
		public bool closed;
	}
		
	class NodeOperator : Node
	{
		public NodeOperator(SourceLocation loc, OperatorType operation, Node a, Node b)
			: base(NodeType.OPERATOR, loc)
		{
			this.operation = operation;
			this.a = a;
			this.b = b;
		}
		
		public override string ToString() { return base.ToString() + " " + operation.ToString(); }
		
		public OperatorType operation;
		public Node a, b;
		
		public override string toDebugText()
			=>  "{0} = {1} {2} {3}".fill(dataType, operation, a.dataType, b?.dataType);
	}
	
	class NodeLiteral : Node
	{
		public NodeLiteral(SourceLocation loc, object data) : base(NT.LITERAL, loc) { this.data = data; }
		public object data;
	}
}