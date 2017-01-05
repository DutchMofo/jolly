using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;

namespace Jolly
{
using NT = Node.NodeType;
using TT = Token.Type;

static class Analyser
{
	static List<Node> instructions, program, staticInstructions = new List<Node>();
	
	static int cursor = 0;
	public static void analyse(List<Node> program)
	{
		Analyser.program = program;
		instructions = new List<Node>(program.Count);
		
		for(Node node = program[cursor]; cursor < program.Count; cursor += 1)
		{
			node = program[cursor];
			Action<Node> action;
			if(staticAnalysers.TryGetValue(node.nodeType, out action)) {
				action(node);
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
		},
		staticAnalysers = new Dictionary<NT, Action<Node>>() {
			{ NT.VARIABLE_DEFINITION, node => {
				int i = 1;
				Symbol symbol = (Symbol)node;
				for(; i <= symbol.childNodeCount; i += 1)
				{
					node = program[cursor + i];
					Action<Node> action;
					if(staticAnalysers.TryGetValue(node.nodeType, out action)) {
						action(node);
					} else {
						throw Jolly.unexpected(node);
					}
				}
				cursor += i;
			} },
			{ NT.OPERATOR, node => {
				Operator o = (Operator)node;
				Action<Operator> action;
				staticOperatorAnalysers.TryGetValue(o.operation, out action);
				if(action == null) {
					throw Jolly.unexpected(node);
				}
				action(o);
			} },
			{ NT.BASETYPE, node => { } },
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
			{ TT.GET_MEMBER, operatorGetMember },
		};
	
	static void operatorGetMember(Operator op) {
		Symbol bName = op.b as Symbol;
		if(bName == null) {
			throw Jolly.addError(op.b.location, "The right-hand side of the period operator must be a name");
		}
		
		if(op.a.dataType == null)
			getTypeFromName(op.a);
		
		TableFolder type = op.a.dataType as TableFolder;
		if(type == null) {
			throw Jolly.addError(op.b.location, "The type \"{0}\" has no members".fill(type));
		}
		
		op.b.dataType = type.getChild(bName.name)?.type;
		
		if(op.b.dataType == null) {
			string typeName = type?.parent.children.First(p => p.Value == type).Key;
			throw Jolly.addError(op.b.location, "The type {0} does not contain a member \"{1}\"".fill(typeName, bName.name));
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
				throw Jolly.addError(name.location, "The name \"{0}\" does not exist in the current context".fill(name.name));
			}
			name.dataType = item.type;
			return true;
		}
		return false;
	}
}
}