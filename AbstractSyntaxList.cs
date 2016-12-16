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
		public string name;
		public Node node;
		
		public virtual void calculateSize() { }
		public TableItem(Node node) { this.node = node; }
		public TableItem(int size) { this.size = align = size; }
		public TableItem(int size, int align) { this.size = size; this.align = align; }
		
		// Debug print tree
		string qF(int number) => (number < 10 & number >= 0) ? " " + number.ToString() : number.ToString();
		protected string info(int indent) => "[{0}:{1}:{2}] ".fill(qF(offset), qF(align), qF(size)) + new string(' ', indent * 2);
		public virtual void PrintTree(int indent)
			=> System.Console.WriteLine(info(indent) + name);
	}

	class TableFolder : TableItem
	{
		// Debug print tree
		public override void PrintTree(int indent) {
            System.Console.WriteLine(info(indent) + name + '/');
			foreach(var i in children.Values)
				i.PrintTree(indent+1);
		}
		
		Dictionary<string, TableItem> children = new Dictionary<string, TableItem>();
		
		public TableFolder(Node node)
			: base(node) { flags = NameFlags.FOLDER; }
		
		public TableFolder(Node node, NameFlags flags)
			: base(node) { flags = NameFlags.FOLDER | flags; }
		
		public bool addChild(string childName, TableItem child)
		{
			TableFolder iterator = this;
			while(iterator != null) {
				if(iterator.children.ContainsKey(childName))
					return false;
				iterator = iterator.parent;
			}
			children.Add(childName, child);
			child.name = childName;
			child.parent = this;
			return true;
		}
		
		public override void calculateSize()
		{
			// Todo: Validate results, Look into optimization
			if((flags & NameFlags.UNION) != 0)
			{
				foreach(var child in children.Values)
				{
					child.calculateSize();
					// child.offset = 0; // Not nessesary
					if(child.size > size)
						size = child.size;
					if(child.align > align)
						align = child.align;
				}
			}
			else
			{
				int _offset = 0;
				foreach(var child in children.Values)
				{
					child.calculateSize();
					if(child.size == 0)
						continue;
					if(child.align > align)
						align = child.align;
					child.offset = _offset / child.align * child.align - _offset;
					_offset = child.offset + child.size; 
				}
				if(align > 0)
					size = ((_offset-1) / align + 1) * align;
			}
		}
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
			VARIABLE,
			VARIABLE_RW,
			WHILE,
			
			COUNT // Must be last
		}
		
		public NodeType nodeType;
		public TableItem dataType;
		public SourceLocation location;
		
		public override string ToString()
		{
			return "{0}:{1} {2}".fill(location.line, location.column, nodeType);
		}
	}
	
	class BaseType : Node
	{
		public BaseType(SourceLocation loc, Token.Type type)
			: base(NT.BASETYPE, loc) { baseType = type; }
		public Token.Type baseType;
	}
	
	class Symbol : Node
	{
		public Symbol(SourceLocation loc, string name, NT type = NT.NAME)
			: base(type, loc) { this.name = name; }
		public string name;
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
		public Result(SourceLocation loc)
			: base(NodeType.RESULT, loc) {  }
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
		public enum LiteralType
		{
			STRING,
			FLOAT,
			INTEGER
		}
		
		public Literal(SourceLocation loc, string s)
			: base(NodeType.LITERAL, loc) { literalType = LiteralType.STRING; data = (object)s; }
		public Literal(SourceLocation loc, ulong i)
			: base(NodeType.LITERAL, loc) { literalType = LiteralType.INTEGER; data = (object)i; }
		public Literal(SourceLocation loc, double f)
			: base(NodeType.LITERAL, loc) { literalType = LiteralType.FLOAT; data = (object)f; }
		
		public LiteralType literalType;
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
		public Symbol[] arguments;
		public Node returns;
		
		public Function(SourceLocation loc, Symbol[] arguments, TableFolder parentScope)
			: base(NodeType.FUNCTION, loc) { this.arguments = arguments; }
	}
	
	class If  : Node
	{
		public If(SourceLocation loc, Node[] condition, Node conditionValue)
			: base(NodeType.IF, loc) // Todo add label and parentScope
		{ 
			this.conditionValue = conditionValue;
			this.condition = condition;
		}
		
		public Node[] condition;
		public Node conditionValue;
	}
	
	class For : Node
	{
		public For(SourceLocation loc)
			: base(NodeType.FOR, loc) { }
		
		public Node[] counter, condition, increment;
		public Node conditionValue;
	}
}