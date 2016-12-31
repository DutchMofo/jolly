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
		Action<Node> action;
		foreach(Node node in program)
		{
			if(analysers.TryGetValue(node.nodeType, out action)) {
				action(node);
			} else {
				Jolly.addError(new SourceLocation(), "");
				throw new ParseException();
			}
		}
	}
	
	static readonly Dictionary<NT, Action<Node>> analysers = new Dictionary<NT, Action<Node>>() {
		{ NT.ALIAS, node => { } },
		{ NT.BLOCK, node => { } },
		{ NT.BREAK, node => { } },
		{ NT.CAST, node => { } },
		{ NT.FOR, node => { } },
		{ NT.FUNCTION, node => { } },
		{ NT.FUNCTION_CALL, node => { } },
		{ NT.GOTO, node => { } },
		{ NT.IF, node => { } },
		{ NT.IF_ELSE, node => { } },
		{ NT.LABEL, node => { } },
		{ NT.TUPPLE, node => { } },
		{ NT.LITERAL, node => { } },
		{ NT.LOOP_CONTROL, node => { } },
		{ NT.NAME, node => {
			Debug.Assert(false);
		} },
		{ NT.OPERATOR, node => {
			Operator o = (Operator)node;
			operatorAnalysers[o.operation](o);
		} },
		{ NT.RESULT, node => { } },
		{ NT.RETURN, node => { } },
		{ NT.STATEMENT, node => { } },
		{ NT.USING, node => { } },
		{ NT.VARIABLE_DEFINITION, node => { } },
		{ NT.VARIABLE_RW, node => { } },
		{ NT.WHILE, node => { } },
	};
	
	static readonly Dictionary<TT, Action<Operator>> operatorAnalysers = new Dictionary<TT, Action<Operator>>() {
		{ TT.REFERENCE, op => { } },
		{ TT.DEREFERENCE, op => { } },
		{ TT.PLUS, op => { } },
		{ TT.MINUS, op => { } },
		{ TT.INCREMENT, op => { } },
		{ TT.DECREMENT,	op => { } },
		{ TT.LOGIC_AND,	op => { } },
		{ TT.EQUAL_TO, op => { } },
		{ TT.LOGIC_OR, op => { } },
		{ TT.LOGIC_NOT, op => { } },
		{ TT.BIT_NOT, op => { } },
		{ TT.BIT_AND, op => { } },
		{ TT.BIT_OR, op => { } },
		{ TT.BIT_XOR, op => { } },
		{ TT.MODULO, op => { } },
		{ TT.DIVIDE, op => { } },
		{ TT.MULTIPLY, op => { } },
		{ TT.GET_MEMBER,op => {
			if(op.a.dataType == null)
				getTypeFromName(op.a);
			
			if(op.a.dataType == null) {
				Jolly.addError(new SourceLocation(), "");
				throw new ParseException();
			}
			
			DataType type = op.a.dataType;
			// type.
			
			
		} },
		{ TT.SUBSCRIPT, op => { } },
		{ TT.READ, op => { } },
		{ TT.ASSIGN, op => { } },
		{ TT.SHIFT_LEFT, op => { } },
		{ TT.SHIFT_RIGHT, op => { } },
		{ TT.LESS_EQUAL, op => { } },
		{ TT.GREATER_EQUAL, op => { } },
		{ TT.SLICE, op => { } },
		{ TT.CAST, op => { } },
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
}
}