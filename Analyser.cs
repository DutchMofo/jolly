using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace Jolly
{
using System.Linq;
using NT = Node.NodeType;
using OT = OperatorType;
using IT = Instruction.Type;


static class Analyser
{
	[ThreadStatic] static Stack<NodeSymbol> scopeStack = new Stack<NodeSymbol>(16);
	[ThreadStatic] static List<Instruction> instructions;
	[ThreadStatic] static List<Node> program;
	// Used to store the intermediary node nessasary for looking up the correct datatype
	// of the variable about to be defined
	[ThreadStatic] static Node definitionInstruction;
	[ThreadStatic] static int cursor = 0;
		
	public static List<Instruction> analyse(List<Node> program)
	{
		Analyser.program = program;
		var debug = instructions = new List<Instruction>(program.Count);
				
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
		DataType.makeUnique(ref scopeHeader.dataType);
	}
	
	static readonly Dictionary<NT, Action<Node>>
		// Used for the first pass to define all the struct members
		typeDefinitionAnalysers = new Dictionary<NT, Action<Node>>() {
			{ NT.SCOPE_END, node => scopeEnd(scopeStack.Pop()) },
			{ NT.VARIABLE_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.FUNCTION, node => {
				int startCursor = cursor;
				NodeFunction function = (NodeFunction)node;
				for(int i = 1; i <= function.returnDefinitionCount; i += 1)
				{
					node = program[cursor + i];
					Action<Node> action;
					if(variableDefinitionAnalysers.TryGetValue(node.nodeType, out action)) {
						action(node);
					} else {
						throw Jolly.unexpected(node);
					}
				}
				cursor += function.returnDefinitionCount + 1;
				var returns = (definitionInstruction.nodeType == NT.TUPPLE) ?
					((NodeTupple)definitionInstruction).values.Select(n => n.dataType).ToArray() :
					new DataType[] { definitionInstruction.dataType };
				definitionInstruction = null;
				
				int end = cursor + function.argumentDefinitionCount;
				List<DataType> fuckit = new List<DataType>();
				for(; cursor < end; cursor += 1)
				{
					var definition = program[cursor] as NodeSymbol;
					if(definition == null) {
						throw Jolly.unexpected(node);
					}
					fuckit.Add(defineMemberOrVariable(definition));
				}
				
				var arguments = fuckit.ToArray();
				
				DataType functionType = new DataTypeFunction(returns, arguments);
				cursor = startCursor + function.memberCount;			
			} },
			{ NT.STRUCT, node => {
				// instructions.Add(node);asdasd
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
				getTypeFromName(ref node, false);
				definitionInstruction = node;
			} },
			{ NT.TUPPLE, node => {
				NodeTupple tupple = (NodeTupple)node;
				for(int i = 0; i < tupple.values.Count; i += 1) {
					// getTypeFromName(ref tupple.values[i], false); // Fuck you c#
					var _node = tupple.values[i];
					getTypeFromName(ref _node, false);
					tupple.values[i] = _node;
				}
				definitionInstruction = tupple;
			} },
			{ NT.MODIFY_TYPE, node => {
				NodeModifyType tToRef = (NodeModifyType)node;
				getTypeFromName(ref tToRef.target, false);
				definitionInstruction = tToRef.target;
			} },
		},
		analysers = new Dictionary<NT, Action<Node>>() {
			{ NT.SCOPE_END, node => scopeEnd(scopeStack.Pop()) },
			{ NT.VARIABLE_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.STRUCT, skipSymbol },
			{ NT.FUNCTION, node => {
				NodeFunction function = (NodeFunction)node;
				cursor += function.returnDefinitionCount + function.argumentDefinitionCount;
				// instructions.Add(function);asdasd
			} },
			{ NT.OPERATOR, node => {
				NodeOperator o = (NodeOperator)node;
				operatorAnalysers[o.operation](o);
			} },
			{ NT.FUNCTION_CALL, node => {
				// TODO: Validate datatype's
				// instructions.Add(node);asdasd
			} },
			{ NT.RETURN, node => {
				// TODO: Validate datatype's
				// instructions.Add(node);asdasd
			} },
		};
	
	static void skipSymbol(Node node)
		=> cursor += (node as NodeSymbol).memberCount;
	
	static DataType defineMemberOrVariable(Node node)
	{
		NodeSymbol symbol = (NodeSymbol)node;
		if(symbol.dataType != null) {
			cursor += symbol.memberCount;
			return null;
		}
		
		for(int i = 1; i <= symbol.memberCount; i += 1)
		{
			node = program[cursor + i];
			Action<Node> action;
			if(variableDefinitionAnalysers.TryGetValue(node.nodeType, out action)) {
				action(node);
			} else {
				throw Jolly.unexpected(node);
			}
		}
		cursor += symbol.memberCount;
		
		Debug.Assert(definitionInstruction.dataType != null);
		symbol.dataType = definitionInstruction.dataType;
		
		if((symbol.definitionScope.flags & NameFlags.IS_TYPE) == 0) {
			DataType refToData = new DataTypeReference(symbol.dataType);
			DataType.makeUnique(ref refToData);
			symbol.definitionScope.children[symbol.name].dataType = refToData;
		} else {
			var structType = symbol.dataType as DataTypeStruct;
			if(structType != null) {
				structType.members[structType.memberMap[symbol.name]] = symbol.dataType;
			}
		}
				
		return symbol.dataType;
		// instructions.Add(symbol);asdasd
		// definitionInstruction = null; // Just to be sure
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
				instructions.Add(new InstructionOperator(op));
			} },
			{ OT.REFERENCE, op => {
				getTypeFromName(ref op.a);
				op.result.dataType = new DataTypeReference(op.a.dataType);
				DataType.makeUnique(ref op.result.dataType);
				instructions.Add(new InstructionOperator(op));
			} },
			{ OT.CAST, op => {
				getTypeFromName(ref op.a);
				getTypeFromName(ref op.b);
				op.result.dataType = op.b.dataType;
				instructions.Add(new InstructionOperator(op));
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
			refType.getMember(bName.name, false) ?? refType.referenced.getMember(bName.name, false) :
			op.a.dataType.getMember(bName.name, true);
		
		if(op.result.dataType == null) {
			throw Jolly.addError(bName.location, "The type does not contain a member \"{0}\"".fill(bName.name));
		}
		if(refType != null) {
			op.result.dataType = new DataTypeReference(op.result.dataType) as DataType; 
			DataType.makeUnique(ref op.result.dataType);
			// instructions.Add(op);asdasd
		}
	}
	
	static void basicOperator(NodeOperator op)
	{
		getTypeFromName(ref op.a);
		getTypeFromName(ref op.b);
		if(op.a.dataType != op.b.dataType) {
			throw Jolly.addError(op.location, "Types not the same");
		}
		op.result.dataType = op.a.dataType;
		// instructions.Add(op);asdasd
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
			
			DataTypeReference refTo = item.dataType as DataTypeReference;
			if(refTo != null && load) {
				node = new NodeResult(name.location) { dataType = refTo.referenced };
				// instructions.Add(new NodeOperator(name.location, OT.READ, item.node, null, result: node));asdasd
			} else {
				node.dataType = item.dataType;
			}
			return true;
		}
		return false;
	}
}
}