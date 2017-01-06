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
	static List<Node> instructions, program;
	static Node definitionInstruction;
	
	static int cursor = 0;
	public static List<Node> analyse(List<Node> program)
	{
		Analyser.program = program;
		instructions = new List<Node>(program.Count);
		
		for(Node node = program[cursor]; cursor < program.Count; cursor += 1)
		{
			node = program[cursor];
			Action<Node> action;
			if(typeDefinitionAnalysers.TryGetValue(node.nodeType, out action)) {
				action(node);
			}
		}
		
		cursor = 0;
		for(Node node = program[cursor]; cursor < program.Count; cursor += 1)
		{
			node = program[cursor];
			Action<Node> action;
			if(analysers.TryGetValue(node.nodeType, out action)) {
				if(action != null) // Null means skip
					action(node);
			} else {
				throw Jolly.unexpected(node);
			}
		}
		
		return instructions;
	}
	
	static readonly Dictionary<NT, Action<Node>>
		// Used for the first pass to define all the struct members
		typeDefinitionAnalysers = new Dictionary<NT, Action<Node>>() {
			{ NT.VARIABLE_DEFINITION, defineMemberOrVariable },
			{ NT.MEMBER_DEFINITION, defineMemberOrVariable },
			{ NT.FUNCTION, skipSymbol },
		},
		// Used to load the type before defining a variable
		variableDefinitionAnalysers = new Dictionary<NT, Action<Node>>() {
			{ NT.OPERATOR, node => {
				Operator o = (Operator)node;
				if(o.operation == TT.GET_MEMBER) {
					operatorGetMember(o);
					definitionInstruction = o.b;
				} else {
					throw Jolly.unexpected(node);
				}
			} },
			{ NT.BASETYPE, node => {
				definitionInstruction = node;
			} },
			{ NT.NAME, node => {
				if(!getTypeFromName(node)) {
					throw Jolly.addError(node.location, "Does not exist in the current context");
				}
				definitionInstruction = node;
			} },
		},
		analysers = new Dictionary<NT, Action<Node>>() {
			{ NT.VARIABLE_DEFINITION, defineMemberOrVariable },
			{ NT.MEMBER_DEFINITION, skipSymbol },
			{ NT.STRUCT, skipSymbol },
			{ NT.FUNCTION, null },
			{ NT.OPERATOR, node => {
				Operator o = (Operator)node;
				operatorAnalysers[o.operation](o);
			} },
			{ NT.RETURN, node => {
				
			} },
		};
	
	static void skipSymbol(Node node)
		=> cursor += (node as Symbol).childNodeCount;
	
	static void defineMemberOrVariable(Node node)
	{
		Symbol symbol = (Symbol)node;
		for(int i = 1; i <= symbol.childNodeCount; i += 1)
		{
			node = program[cursor + i];
			Action<Node> action;
			if(variableDefinitionAnalysers.TryGetValue(node.nodeType, out action)) {
				action(node);
			} else {
				throw Jolly.unexpected(node);
			}
		}
		cursor += symbol.childNodeCount;
		
		Debug.Assert(definitionInstruction.dataType != null);
		symbol.dataType = symbol.definitionScope.children[symbol.name].type = definitionInstruction.dataType;
		instructions.Add(symbol);
		definitionInstruction = null;
	}
	
	static readonly Dictionary<TT, Action<Operator>>
		operatorAnalysers = new Dictionary<TT, Action<Operator>>() {
			{ TT.PLUS, op => {
				getTypesAndLoadNames(op);
				
				"".ToString();
			} },
			{ TT.GET_MEMBER, operatorGetMember },
			{ TT.READ, op => {
				
			} },
			{ TT.ASSIGN, op => {
				
			} },
		};
	
	static void operatorGetMember(Operator op)
	{
		Symbol bName = op.b as Symbol;
		if(bName == null) {
			throw Jolly.addError(op.b.location, "The right-hand side of the period operator must be a name");
		}
		
		if(op.a.dataType == null) {
			if(!getTypeFromName(op.a)) {
				throw Jolly.addError(op.a.location, "Does not exist in the current context");
			}
		}
		
		TableFolder type = op.a.dataType as TableFolder;
		if(type == null) {
			throw Jolly.addError(bName.location, "The type \"{0}\" has no members".fill(type));
		}
		
		bName.dataType = type.getChild(bName.name)?.type;
		
		if(bName.dataType == null) {
			string typeName = type?.parent.children.First(p => p.Value == type).Key;
			throw Jolly.addError(bName.location, "The type {0} does not contain a member \"{1}\"".fill(typeName, bName.name));
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