using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace Jolly
{
using NT = Node.NodeType;
using TT = Token.Type;

static class Analyser
{
	static List<Node> instructions, program;
	
	static int cursor = 0;
	public static void analyse(List<Node> program)
	{
		Analyser.program = program;
		instructions = new List<Node>(program.Count);
		
		Action<Node> action;
		for(Node node = program[cursor]; cursor < program.Count; cursor += 1)
		{
			node = program[cursor];
			if(staticAnalysers.TryGetValue(node.nodeType, out action)) {
				action(node);
			} else {
				Jolly.unexpected(node);
				throw new ParseException();
			}
		}
	}
	
	static readonly Dictionary<NT, Action<Node>>
		analysers = new Dictionary<NT, Action<Node>>() {
			{ NT.STRUCT, node => {
				Console.WriteLine(node);
			} },
			{ NT.FUNCTION, node => {
				cursor += (node as Symbol).childNodeCount;
			} },
			{ NT.FUNCTION_CALL, node => {
				
			} },
			{ NT.OPERATOR, node => {
				Operator o = (Operator)node;
				operatorAnalysers[o.operation](o);
			} },
			{ NT.RETURN, node => {
				
			} },
			{ NT.VARIABLE_DEFINITION, node => {
				int i = 1;
				Symbol symbol = (Symbol)node;
				for(; i <= symbol.childNodeCount; i += 1) {
					Operator op = (Operator)program[cursor + i];
					staticOperatorAnalysers[op.operation](op);
				}
				cursor += i;
			} },
		},
		staticAnalysers = new Dictionary<NT, Action<Node>>() {
			{ NT.OPERATOR, node => {
				Operator o = (Operator)node;
				staticOperatorAnalysers[o.operation](o);
			} },
		};
	
	static readonly Dictionary<TT, Action<Operator>>
		operatorAnalysers = new Dictionary<TT, Action<Operator>>() {
			{ TT.PLUS, op => {
				getTypesAndLoadNames(op);
				
			} },
			{ TT.GET_MEMBER, op => {
				
			} },
			{ TT.READ, op => {
				
			} },
			{ TT.ASSIGN, op => {
				
			} },
		},
		staticOperatorAnalysers = new Dictionary<TT, Action<Operator>>() {
			{ TT.GET_MEMBER, op => { 
				operatorGetMember(op);
			} },
		};
	
	static void operatorGetMember(Operator op) {
		Symbol bName = op.b as Symbol;
		if(bName == null) {
			Jolly.addError(op.b.location, "The right-hand side of the period operator must be a name");
			throw new ParseException();
		}
		
		if(op.a.dataType == null)
			getTypeFromName(op.a);
		
		TableFolder type = op.a.dataType as TableFolder;
		if(type == null) {
			Jolly.addError(op.b.location, "The type \"{0}\" has no members".fill(type));
			throw new ParseException();
		}
		
		op.b.dataType = type.getChild(bName.name).type;
		
		if(op.b.dataType == null) {
			Jolly.addError(op.b.location, "The type {0} does not contain a member \"{1}\"".fill(op.b, bName.name));
			throw new ParseException();
		}
	}
	
	static void getTypesAndLoadNames(Operator op)
	{
		Operator cache;
		if(getTypeFromName(op.a)) {
			instructions.Add(cache = new Operator(op.a.location, TT.READ, op.a, null, new Result(op.a.location)));
			op.a = cache;
		}
		if(getTypeFromName(op.b)) {
			instructions.Add(cache = new Operator(op.b.location, TT.READ, op.b, null, new Result(op.b.location)));
			op.b = cache;
		}
	}
	
	static bool getTypeFromName(Node node)
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
			return true;
		}
		return false;
	}
}
}