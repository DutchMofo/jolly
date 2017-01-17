using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace Jolly
{
using NT = Node.NodeType;
using OT = OperatorType;

static class Analyser
{
	[ThreadStatic] static Stack<NodeSymbol> scopeStack = new Stack<NodeSymbol>(16);
	[ThreadStatic] static List<Node> instructions, program;
	// Used to store the intermediary node nessasary for looking up the correct datatype
	// of the variable about to be defined
	[ThreadStatic] static Node definitionInstruction;
	[ThreadStatic] static int cursor = 0;
		
	public static List<Node> analyse(List<Node> program)
	{
		Analyser.program = program;
		var debug = instructions = new List<Node>(program.Count);
				
		for(Node node = program[cursor];
			cursor < program.Count;
			cursor += 1)
		{
			node = program[cursor];
			Action<Node> action;
			if(typeDefinitionAnalysers.TryGetValue(node.nodeType, out action)) {
				action(node);
			}
		}
		
		cursor = 0;
		for(Node node = program[cursor];
			cursor < program.Count;
			cursor += 1)
		{
			node = program[cursor];
			Action<Node> action;
			if(!analysers.TryGetValue(node.nodeType, out action)) {
				throw Jolly.unexpected(node);
			}
			action(node);
		}
		
		return instructions;
	}
	
	static void scopeEnd(Node scopeHeader)
	{
		if(scopeHeader.nodeType != NT.FUNCTION) // TODO: Remove this
		DataType.makeUnique(ref scopeHeader.dataType);
	}
	
	static readonly Dictionary<NT, Action<Node>>
		// Used for the first pass to define all the struct members
		typeDefinitionAnalysers = new Dictionary<NT, Action<Node>>() {
			{ NT.SCOPE_END, node => scopeEnd(scopeStack.Pop()) },
			{ NT.VARIABLE_DEFINITION, defineMemberOrVariable },
			{ NT.FUNCTION, skipSymbol },
			{ NT.STRUCT, node => {
				instructions.Add(node);
				scopeStack.Push((NodeSymbol)node);
			} },
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
				getTypeFromName(ref node);
				definitionInstruction = node;
			} },
			{ NT.MODIFY_TYPE, node => {
				NodeModifyType tToRef = (NodeModifyType)node;
				getTypeFromName(ref tToRef.target);
				definitionInstruction = tToRef.target;
			} },
		},
		analysers = new Dictionary<NT, Action<Node>>() {
			{ NT.SCOPE_END, node => scopeEnd(scopeStack.Pop()) },
			{ NT.VARIABLE_DEFINITION, defineMemberOrVariable },
			{ NT.STRUCT, skipSymbol },
			{ NT.FUNCTION, node => {
				scopeStack.Push((NodeSymbol)node);
				instructions.Add(node);
			} },
			{ NT.OPERATOR, node => {
				NodeOperator o = (NodeOperator)node;
				operatorAnalysers[o.operation](o);
			} },
			{ NT.FUNCTION_CALL, node => {
				// TODO: Validate datatype's
				instructions.Add(node);
			} },
			{ NT.RETURN, node => {
				// TODO: Validate datatype's
				instructions.Add(node);
			} },
		};
	
	static void skipSymbol(Node node)
		=> cursor += (node as NodeDefinition).childNodeCount;
	
	static void defineMemberOrVariable(Node node)
	{
		NodeDefinition symbol = (NodeDefinition)node;
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
		symbol.dataType = definitionInstruction.dataType;
		
		if((symbol.definitionScope.flags & NameFlags.IS_TYPE) == 0) {
			DataType refToData = new DataTypeReference(symbol.dataType);
			DataType.makeUnique(ref refToData);
			symbol.definitionScope.children[symbol.name].dataType = refToData;
		} else {
			symbol.definitionScope.children[symbol.name].dataType = symbol.dataType;
		}
		instructions.Add(symbol);
		definitionInstruction = null; // Just to be sure
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
				
				var target = op.a.dataType as DataTypeReference;
				if(target == null) {
					throw Jolly.addError(op.a.location, "Cannot assign to this");
				}
				
				getTypeFromName(ref op.b);
				if(target.referenced != op.b.dataType) {
					throw Jolly.addError(op.a.location, "Cannot assign this value type");
				}
				op.result.dataType = op.b.dataType;
				instructions.Add(op);
			} },
			{ OT.REFERENCE, op => {
				getTypeFromName(ref op.a);
				op.result.dataType = new DataTypeReference(op.a.dataType);
				DataType.makeUnique(ref op.result.dataType);
				instructions.Add(op);
			} },
			{ OT.CAST, op => {
				getTypeFromName(ref op.a);
				getTypeFromName(ref op.b);
				op.result.dataType = op.b.dataType;
				instructions.Add(op);
			} },
		};
	
	static void operatorGetMember(NodeOperator op)
	{
		getTypeFromName(ref op.a, false);
		if(op.a.dataType == null) {
			throw Jolly.addError(op.a.location, "Can't load type");
		}
		NodeSymbol bName = op.b as NodeSymbol;
		if(bName == null) {
			throw Jolly.addError(op.b.location, "The right-hand side of the period operator must be a name");
		}
		
		var refType = op.a.dataType as DataTypeReference;
		op.result.dataType = (refType != null) ? 
			refType.getMember(bName.name) ?? refType.referenced.getMember(bName.name) :
			op.a.dataType.getMember(bName.name);
		
		if(op.result.dataType == null) {
			throw Jolly.addError(bName.location, "The type does not contain a member \"{0}\"".fill(bName.name));
		}
		if(refType != null) {
			op.result.dataType = new DataTypeReference(op.result.dataType) as DataType; 
			DataType.makeUnique(ref op.result.dataType);
		}
		instructions.Add(op);
	}
	
	static void basicOperator(NodeOperator op)
	{
		getTypeFromName(ref op.a);
		getTypeFromName(ref op.b);
		if(op.a.dataType != op.b.dataType) {
			throw Jolly.addError(op.location, "Types not the same");
		}
		op.result.dataType = op.a.dataType;
		instructions.Add(op);
	}
	
	static bool getTypeFromName(ref Node node, bool load = true)
	{
		if(node.nodeType == NT.NAME)
		{
			NodeSymbol name = (NodeSymbol)node;
			Debug.Assert(name.dataType == null);
			var item = name.definitionScope.searchItem(name.name);
			
			if(item == null) {
				throw Jolly.addError(name.location, "The name \"{0}\" does not exist in the current context".fill(name.name));
			}
			Debug.Assert(item.dataType != null);
			
			if((item.dataType is DataTypeReference) && load) {
				node = new NodeResult(name.location) { dataType = item.dataType };
				instructions.Add(new NodeOperator(name.location, OT.READ, item.node, null, result: node));
			} else {
				node.dataType = item.dataType;
			}
			return true;
		}
		return false;
	}
}
}