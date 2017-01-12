using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;

namespace Jolly
{
using NT = Node.NodeType;
using OT = OperatorType;

static class Analyser
{
	[ThreadStatic] static List<Node> instructions, program;
	// Used to store the intermediary node nessasary for looking up the correct datatype
	// of the variable about to be defined
	[ThreadStatic] static Node definitionInstruction;
	[ThreadStatic] static int cursor = 0;
	
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
		
		Console.WriteLine("/");
		TableFolder.root.PrintTree("", 0);
		
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
			{ NT.FUNCTION, skipSymbol },
		},
		// Used to load the type before defining a variable
		variableDefinitionAnalysers = new Dictionary<NT, Action<Node>>() {
			{ NT.OPERATOR, node => {
				Operator o = (Operator)node;
				Debug.Assert(o.operation == OT.GET_MEMBER);
				operatorGetMember(o);
				definitionInstruction = o.result;
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
			{ NT.TYPE_TO_REFERENCE, node => {
				TypeToReference tToRef = (TypeToReference)node;
				getTypeFromName(tToRef.target);
				tToRef.dataType = DataType.getReferenceTo(tToRef.target.dataType, tToRef.referenceType);
				definitionInstruction = tToRef;
			} },
		},
		analysers = new Dictionary<NT, Action<Node>>() {
			{ NT.VARIABLE_DEFINITION, defineMemberOrVariable },
			{ NT.STRUCT, skipSymbol },
			{ NT.FUNCTION, node => instructions.Add(node) },
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
		if(symbol.dataType != null) {
			cursor += symbol.childNodeCount;
			return;
		}
		
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
		// Just to be sure
		definitionInstruction = null;
	}
	
	static readonly Dictionary<OT, Action<Operator>>
		operatorAnalysers = new Dictionary<OT, Action<Operator>>() {
			{ OT.GET_MEMBER, operatorGetMember },
			{ OT.MINUS, basicOperator },
			{ OT.PLUS, basicOperator },
			{ OT.MULTIPLY, basicOperator },
			{ OT.DIVIDE, basicOperator },
			{ OT.ASSIGN, op => {
				Node a = op.a, b = op.b;
				if(getTypeFromName(a)) {
					a.dataType = DataType.getReferenceTo(a.dataType, ReferenceType.VARIABLE);
				}
				
				if(a.dataType.referenceType != ReferenceType.VARIABLE) {
					throw Jolly.addError(a.location, "Cannot assign to this");
				}
				
				if(getTypeFromName(b)) {
					op.b.dataType = DataType.getReferenceTo(b.dataType, ReferenceType.VARIABLE);
					instructions.Add(b = new Operator(op.b.location, OT.READ, op.b, null, new Result(op.b.location)));
				}
				instructions.Add(op);
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
		
		DataType type = op.a.dataType as DataType;
		if(type == null) {
			throw Jolly.addError(bName.location, "The type \"{0}\" has no members".fill(type));
		}
		
		if(type.referenceType == ReferenceType.VARIABLE)
			type = type.referenced;
		op.result.dataType = type.getSibling(bName.name);
		
		if(op.result.dataType == null) {
			throw Jolly.addError(bName.location, "The type {0} does not contain a member \"{1}\"".fill(type, bName.name));
		}
		op.result.dataType = DataType.getReferenceTo(op.result.dataType, ReferenceType.VARIABLE);
	}
	
	static void basicOperator(Operator op)
	{
		Node a = op.a, b = op.b;
		if(getTypeFromName(a)) {
			op.b.dataType = DataType.getReferenceTo(a.dataType, ReferenceType.VARIABLE);
			instructions.Add(a = new Operator(op.a.location, OT.READ, op.a, null, new Result(op.a.location)));
		}
		if(getTypeFromName(b)) {
			op.b.dataType = DataType.getReferenceTo(b.dataType, ReferenceType.VARIABLE);
			instructions.Add(b = new Operator(op.b.location, OT.READ, op.b, null, new Result(op.b.location)));
		}
		if(a.dataType != b.dataType) {
			throw Jolly.addError(op.location, "Types not the same");
		}
		op.result.dataType = op.a.dataType;
		instructions.Add(op);
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