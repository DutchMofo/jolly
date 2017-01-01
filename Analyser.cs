using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace Jolly
{
using NT = Node.NodeType;
using TT = Token.Type;

static class Analyser
{
	static List<Node> instructions = new List<Node>();
	
	public static void analyse(List<Node> program)
	{
		Action<Node> action;
		foreach(Node node in program)
		{
			if(analysers.TryGetValue(node.nodeType, out action)) {
				action(node);
			} else {
				Jolly.unexpected(node);
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
		{ NT.NAME, node => { Debug.Assert(false); } },
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
		{ TT.PLUS, op => {
			getTypesAndLoadNames(op);
			
		} },
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
			Symbol bName = op.b as Symbol;
			if(bName == null) {
				Jolly.addError(op.b.location, "The right-hand side of the dot operator must be a name");
				throw new ParseException();
			}
			
			if(op.a.dataType == null)
				getTypeFromName(op.a);
			Debug.Assert(op.a.nodeType == NT.NAME);
			
			op.b.dataType = op.a.dataType.definedInScope.getChild(bName.name).type;
			if(op.b.dataType == null) {
				Jolly.addError(op.b.location, "The type {0} does not contain a member \"{1}\"".fill(op.b, bName.name));
				throw new ParseException();
			}
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
	
	static void getTypesAndLoadNames(Operator op)
	{
		getTypeFromName(op.a, true);
		getTypeFromName(op.b, true);
	}
	
	static void getTypeFromName(Node node, bool load = false)
	{
		if(node.nodeType == NT.NAME)
		{
			Symbol name = (Symbol)node;
			Debug.Assert(name.dataType == null);
			var item = name.definitionScope.searchItem(name.name);
			
			if(item == null) {
				Jolly.addError(name.location, "The name \"{0}\" does not exist in the current context".fill(name.name));
				throw new ParseException();
			}
			
			name.dataType = item.type;
		}
	}
}
}