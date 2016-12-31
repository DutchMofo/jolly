using System.Collections.Generic;
using System;

namespace Jolly
{
using NT = Node.NodeType;
using TT = Token.Type;

static class Analyser
{
	static readonly Dictionary<NT, Action<Node>> analysers = new Dictionary<NT, Action<Node>>(){
		{ NT.OPERATOR,	analyseOperator },
		{ NT.FUNCTION,	analyseFunction },
		{ NT.NAME,		analyseName		},
	};
	
	static Dictionary<TT, Action<Operator>> operatorAnalysers = new Dictionary<TT, Action<Operator>>() {
		{ TT.REFERENCE,		 o => { } },
		{ TT.DEREFERENCE,	 o => { } },
		{ TT.PLUS,			 o => { } },
		{ TT.MINUS,			 o => { } },
		{ TT.INCREMENT,		 o => { } },
		{ TT.DECREMENT,		 o => { } },
		{ TT.LOGIC_AND,		 o => { } },
		{ TT.EQUAL_TO,		 o => { } },
		{ TT.LOGIC_OR,		 o => { } },
		{ TT.LOGIC_NOT,		 o => { } },
		{ TT.BIT_NOT,		 o => { } },
		{ TT.BIT_AND,		 o => { } },
		{ TT.BIT_OR,		 o => { } },
		{ TT.BIT_XOR,		 o => { } },
		{ TT.MODULO,		 o => { } },
		{ TT.DIVIDE,		 o => { } },
		{ TT.MULTIPLY,		 o => { } },
		{ TT.GET_MEMBER,	 o => {
			
		} },
		{ TT.SUBSCRIPT,		 o => { } },
		{ TT.READ,			 o => { } },
		{ TT.ASSIGN,		 o => { } },
		{ TT.SHIFT_LEFT,	 o => { } },
		{ TT.SHIFT_RIGHT,	 o => { } },
		{ TT.LESS_EQUAL,	 o => { } },
		{ TT.GREATER_EQUAL,	 o => { } },
		{ TT.SLICE,			 o => { } },
		{ TT.CAST,			 o => { } },
	};
	
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
		n.ToString();
	}
}
}