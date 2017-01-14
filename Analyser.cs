using System.Collections.Generic;
using System.Diagnostics;
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
				NodeOperator o = (NodeOperator)node;
				Debug.Assert(o.operation == OT.GET_MEMBER);
				operatorGetMember(o);
				definitionInstruction = o.result;
			} },
			{ NT.BASETYPE, node => {
				definitionInstruction = node;
			} },
			{ NT.NAME, node => {
				getTypeFromName(ref node, false);
				definitionInstruction = node;
			} },
			{ NT.MODIFY_TYPE, node => {
				NodeModifyType tToRef = (NodeModifyType)node;
				getTypeFromName(ref tToRef.target, false);
				definitionInstruction = tToRef.target;
			} },
		},
		analysers = new Dictionary<NT, Action<Node>>() {
			{ NT.VARIABLE_DEFINITION, defineMemberOrVariable },
			{ NT.STRUCT, node => {
				DataType.makeUnique(ref node.dataType);
				skipSymbol(node);
			} },
			{ NT.FUNCTION, node => {
				DataType.makeUnique(ref node.dataType);
				instructions.Add(node);
			} },
			{ NT.OPERATOR, node => {
				NodeOperator o = (NodeOperator)node;
				operatorAnalysers[o.operation](o);
			} },
			{ NT.RETURN, node => {
				
			} },
		};
	
	static void skipSymbol(Node node)
		=> cursor += (node as NodeSymbol).childNodeCount;
	
	static void defineMemberOrVariable(Node node)
	{
		NodeSymbol symbol = (NodeSymbol)node;
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
		symbol.dataType = symbol.definitionScope.children[symbol.name].dataType = definitionInstruction.dataType;
		instructions.Add(symbol);
		// Just to be sure
		definitionInstruction = null;
	}
	
	static readonly Dictionary<OT, Action<NodeOperator>>
		operatorAnalysers = new Dictionary<OT, Action<NodeOperator>>() {
			{ OT.GET_MEMBER, operatorGetMember },
			{ OT.MINUS, basicOperator },
			{ OT.PLUS, basicOperator },
			{ OT.MULTIPLY, basicOperator },
			{ OT.DIVIDE, basicOperator },
			{ OT.ASSIGN, op => {
				getTypeFromName(ref op.a, false);
				
				if(!(op.a.dataType is DataTypeReference)) {
					throw Jolly.addError(op.a.location, "Cannot assign to this");
				}
				getTypeFromName(ref op.b, false);
				instructions.Add(op);
			} },
			{ OT.REFERENCE, op => {
				getTypeFromName(ref op.a, true);
				op.result.dataType = new DataTypeReference(op.result.dataType);
				DataType.makeUnique(ref op.result.dataType);
			} },
		};
	
	static void operatorGetMember(NodeOperator op)
	{
		NodeSymbol bName = op.b as NodeSymbol;
		if(bName == null) {
			throw Jolly.addError(op.b.location, "The right-hand side of the period operator must be a name");
		}
		
		if(op.a.dataType == null) {
			getTypeFromName(ref op.a, false);
		}
		
		var refType = op.a.dataType as DataTypeReference;
		op.result.dataType = (refType != null) ? 
			refType.getMember(bName.name) ?? refType.referenced.getMember(bName.name) :
			op.a.dataType.getMember(bName.name);
		
		if(op.result.dataType == null) {
			throw Jolly.addError(bName.location, "The type does not contain a member \"{0}\"".fill(bName.name));
		}
		op.result.dataType = new DataTypeReference(op.result.dataType) as DataType; 
		DataType.makeUnique(ref op.result.dataType);
	}
	
	static void basicOperator(NodeOperator op)
	{
		getTypeFromName(ref op.a, true);
		getTypeFromName(ref op.b, true);
		if(op.a.dataType != op.b.dataType) {
			throw Jolly.addError(op.location, "Types not the same");
		}
		op.result.dataType = op.a.dataType;
		instructions.Add(op);
	}
	
	static bool getTypeFromName(ref Node node, bool load)
	{
		if(node.nodeType == NT.NAME)
		{
			NodeSymbol name = (NodeSymbol)node;
			Debug.Assert(name.dataType == null);
			var item = name.definitionScope.searchItem(name.name);
			
			if(item == null) {
				throw Jolly.addError(name.location, "The name \"{0}\" does not exist in the current context".fill(name.name));
			}
			
			if(load) {
				name.dataType = item.dataType;
				instructions.Add(node = new NodeOperator(name.location, OT.READ, name, null, new NodeResult(name.location)));
			} else {
				node.dataType = new DataTypeReference(item.dataType) as DataType; 
				DataType.makeUnique(ref node.dataType);
			}
			return true;
		}
		return false;
	}
}
}