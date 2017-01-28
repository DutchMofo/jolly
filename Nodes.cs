using System.Collections.Generic;

namespace Jolly
{
	using NT = Node.NodeType;
	
	enum TypeKind
	{
		UNDEFINED,
		STATIC,
		VALUE,
		ADDRES
	}
	
	class Node
	{
		public Node(SourceLocation l, NodeType nT) { nodeType = nT; location = l; }
		
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
			TUPLE,
			MEMBER_TUPLE,
			MEMBER_NAME,
			GLOBAL,
			STATEMENT,
			
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
	}
	
	class NodeModifyType : Node
	{
		public NodeModifyType(SourceLocation loc, Node target, byte targetType)
			: base(loc, NT.MODIFY_TYPE)
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
		public NodeSymbol(SourceLocation loc, string name, NT type = NT.NAME)
			: base(loc, type) { this.text = name; }
		
		public int memberCount;
		public string text;
	}
	
	class NodeLogic : Node
	{
		public NodeLogic(SourceLocation location, NT type, OperatorType operation)
			: base(location, type)
		{
			this.operation = operation;
		}
		public OperatorType operation;
	}
	
	class NodeVariableDefinition : NodeScope
	{
		public NodeVariableDefinition(SourceLocation loc, Scope scope, string name, Node typeFrom, NT type = NT.VARIABLE_DEFINITION)
			: base(loc, type, scope, name)
		{ this.typeFrom = typeFrom; }
		
		public Node typeFrom;
	}
	
	class NodeFunctionCall : NodeSymbol
	{
		public NodeFunctionCall(SourceLocation loc, string function, Node[] a)
			: base(loc, function, NT.FUNCTION_CALL) { arguments = a; }
		
		public Node[] arguments;
	}
	
	class NodeScope : NodeSymbol
	{
		public NodeScope(SourceLocation loc, NT type, Scope scope, string name)
			: base(loc, name, type) { this.scope = scope; }
		
		public Scope scope;
		public Symbol? getDefinition(string name)
			=> scope.searchItem(name);
	}
	
	class NodeFunction : NodeScope
	{
		public NodeFunction(SourceLocation loc, Scope scope, string name)
			: base(loc, NodeType.FUNCTION, scope, name) {  }
		
		public Node returns;
		public int returnDefinitionCount, argumentDefinitionCount, finishedArguments;
	}
		
	class NodeTuple : NodeScope
	{
		public NodeTuple(SourceLocation loc, Scope scope, NT tupType)
			: base(loc, tupType, scope, null) { }
		
		public List<Node> values = new List<Node>();
		public Node scopeFrom;
		public bool closed;
	}
		
	class NodeOperator : Node
	{
		public NodeOperator(SourceLocation loc, OperatorType operation, Node a, Node b)
			: base(loc, NodeType.OPERATOR)
		{
			this.operation = operation;
			this.a = a;
			this.b = b;
		}
		
		public override string ToString() { return base.ToString() + " " + operation.ToString(); }
		
		public OperatorType operation;
		public Node a, b;
	}
	
	class NodeLiteral : Node
	{
		public NodeLiteral(SourceLocation loc, object data)
			: base(loc, NT.LITERAL) { this.data = data; }
		public object data;
	}
}