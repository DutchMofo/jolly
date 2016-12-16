using System.Collections.Generic;
using System.Linq;

namespace Jolly
{
	using NT = Node.NodeType;
	
	enum NameFlags
	{
		FOLDER		= 1<<0,
		STATIC		= 1<<1,
		READ_ONLY	= 1<<2,
		UNION		= 1<<3,
	}

	class TableItem
	{
		public int size, align, offset;
		public TableFolder parent;
		public NameFlags flags;
		public Symbol node;
		
		public TableItem(Symbol node) { this.node = node; }
		public virtual void calculateSize() { }
	}

	class TableName : TableItem
	{
		public TableName(Symbol node, int size)
			: base(node) { this.align = this.size = size; }
	}

	class TableFolder : TableItem
	{
		Dictionary<string, TableItem> children = new Dictionary<string, TableItem>();
		
		public TableFolder(Symbol node)
			: base(node) { flags = NameFlags.FOLDER }
		
		public TableFolder(Symbol node, NameFlags flags)
			: base(node) { flags = NameFlags.FOLDER | flags }
		
		public bool addChild(string childName, TableItem child)
		{
			TableFolder iterator = this;
			while(iterator != null) {
				if(iterator.children.ContainsKey(childName))
					return false;
				iterator = iterator.parent;
			}
			children.Add(childName, child);
			child.parent = this;
			return true;
		}
		
		public override void calculateSize()
		{
			// Todo: Validate results, Look into optimization
			if((flags & NameFlags.IS_UNION) != 0)
			{
				foreach(var child in children.Values)
				{
					child.calculateSize();
					//child.offset = 0; Not nessesary
					if(child.size > size)
						size = child.size;
					if(child.align > align)
						align = child.align;
				}
			}
			else
			{
				int offset = 0;
				foreach(var child in children.Values)
				{
					child.calculateSize();
					if(child.size == 0)
						continue;
					if(child.align > align)
						align = child.align;
					child.offset = ((size-1) / align + 1) * (align-1) - offset;
					offset = child.offset + child.size; 
				}
				size = ((size-1) / align + 1) * align;
			}
		}
	}
	
	class Node
	{
		public Node(NodeType nT, SourceLocation l) { nType = nT; location = l; }
		
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
		// public TypeInfo dType;
		public SourceLocation location;
		
		public override string ToString()
		{
			return "{0}:{1} {2}".fill(location.line, location.column, nType);
		}
	}

	class Symbol : Node
	{
		public Symbol(NodeType type, SourceLocation loc, TableFolder parentScope, string name)
			: base(type, loc) { this.name = name; }
		public string name;
	}
		
	class _List : Node 
	{
		public _List(SourceLocation loc)
			: base(NodeType.LIST, loc) { }
		
		public List<Node> list = new List<Node>();
		public bool locked;
	}
	
	class Result : Node
	{
		public Result(SourceLocation loc)
			: base(NodeType.RESULT, loc) {  }
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
		public enum LType
		{
			STRING,
			FLOAT,
			INTEGER
		}
		
		public Literal(SourceLocation loc, string s)
			: base(NodeType.LITERAL, loc) { lType = LType.STRING; data = (object)s; }
		public Literal(SourceLocation loc, ulong i)
			: base(NodeType.LITERAL, loc) { lType = LType.INTEGER; data = (object)i; }
		public Literal(SourceLocation loc, double f)
			: base(NodeType.LITERAL, loc) { lType = LType.FLOAT; data = (object)f; }
		
		public LType lType;
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
	
	class Return : Node
	{
		public Return(SourceLocation loc, Node returns)
			: base(NodeType.RETURN, loc) { this.returns = returns; }
		public Node returns;
	}

	class Function : Symbol
	{
		public Name[] arguments;
		public Node returns;
		// public TypeInfo[] returns;
		
		public Function(SourceLocation loc, Name[] arguments, TableFolder parentScope)
			: base(NodeType.FUNCTION, loc, parentScope) { this.arguments = arguments; }
	}
	
	class If  : Symbol
	{
		public If(SourceLocation loc, Node[] condition, Node conditionValue)
			: base(NodeType.IF, loc, null)
		{ 
			this.conditionValue = conditionValue;
			this.condition = condition;
		}
		
		public Node[] condition;
		public Node conditionValue;
	}
	
	class For : Symbol
	{
		public For(SourceLocation loc)
			: base(NodeType.FOR, loc, null) { }
		
		public Node[] counter, condition, increment;
		public Node conditionValue;
	}
}