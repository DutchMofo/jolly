using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace Jolly
{
using NT = Node.NodeType;
using TT = Token.Type;

static class Analyser
{
	public static void analyse(List<Node> program)
	{
		foreach(Node node in program)
		{
			if(analysers.ContainsKey(node.nodeType)) {
				analysers[node.nodeType](node);
			} else {
				// Jolly.addError(new SourceLocation(), "");
				// throw new ParseException();
			}
		}
	}
	
	static readonly Dictionary<NT, Action<Node>> analysers = new Dictionary<NT, Action<Node>>(){
		{ NT.OPERATOR,	analyseOperator },
		{ NT.FUNCTION,	analyseFunction },
		{ NT.NAME,		analyseName		},
	};
	
	static readonly Dictionary<TT, Action<Operator>> operatorAnalysers = new Dictionary<TT, Action<Operator>>() {
		{ TT.REFERENCE,		 op => { } },
		{ TT.DEREFERENCE,	 op => { } },
		{ TT.PLUS,			 op => { } },
		{ TT.MINUS,			 op => { } },
		{ TT.INCREMENT,		 op => { } },
		{ TT.DECREMENT,		 op => { } },
		{ TT.LOGIC_AND,		 op => { } },
		{ TT.EQUAL_TO,		 op => { } },
		{ TT.LOGIC_OR,		 op => { } },
		{ TT.LOGIC_NOT,		 op => { } },
		{ TT.BIT_NOT,		 op => { } },
		{ TT.BIT_AND,		 op => { } },
		{ TT.BIT_OR,		 op => { } },
		{ TT.BIT_XOR,		 op => { } },
		{ TT.MODULO,		 op => { } },
		{ TT.DIVIDE,		 op => { } },
		{ TT.MULTIPLY,		 op => { } },
		{ TT.GET_MEMBER,	 op => {
			if(op.a.dataType == null)
				getTypeFromName(op.a);
			
			if(op.a.dataType == null) {
				Jolly.addError(new SourceLocation(), "");
				throw new ParseException();
			}
			
			DataType type = op.a.dataType;
			// type.
			
			
		} },
		{ TT.SUBSCRIPT,		 op => { } },
		{ TT.READ,			 op => { } },
		{ TT.ASSIGN,		 op => { } },
		{ TT.SHIFT_LEFT,	 op => { } },
		{ TT.SHIFT_RIGHT,	 op => { } },
		{ TT.LESS_EQUAL,	 op => { } },
		{ TT.GREATER_EQUAL,	 op => { } },
		{ TT.SLICE,			 op => { } },
		{ TT.CAST,			 op => { } },
	};
	
	static void getTypeFromName(Node node)
	{
		if(node.nodeType == NT.NAME)
		{
			Symbol name = (Symbol)node;
			Debug.Assert(name.dataType == null);
			var item = name.definitionScope.searchItem(name.name);
			
			if(item == null) {
				Jolly.addError(new SourceLocation(), "Undefined symbol \"{0}\"".fill(name.name));
				throw new ParseException();
			}
			
			name.dataType = item.type;
		}
	}
	
	static void analyseOperator(Node n)
	{
		Operator o = (Operator)n;
		operatorAnalysers[o.operation](o);
	}
	
	static void analyseFunction(Node n)
	{
		
	}
	
	static void analyseName(Node n)
	{
		Debug.Assert(false);
	}
}
}