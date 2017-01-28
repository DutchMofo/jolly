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
	static Stack<Enclosure> enclosureStack;
	static Enclosure enclosure, statementEnclosure;
	static List<Instruction> instructions;
	static int cursor;
	
	struct Enclosure
	{
		public Enclosure(Node n, int e) { node = n; end = e; }
		public Node node;
		public int end;
	}
	
	static void incrementCursor()
	{
		cursor += 1;
		while(enclosure.end < cursor) {
			var popped = enclosureStack.Pop();
			enclosure = enclosureStack.Peek();
			enclosureEnd(popped);
		}
	}
	
	static void pushEnclosure(Enclosure enclosure)
	{
		Analyser.enclosure = enclosure;
		enclosureStack.Push(enclosure);
	}
	
	static void enclosureEnd(Enclosure poppedEnclosure)
	{
		switch(poppedEnclosure.node.nodeType)
		{
		case NT.VARIABLE_DEFINITION: {
			var closure = (NodeScope)enclosure.node;
			var symbol = (NodeVariableDefinition)poppedEnclosure.node;
			
			switch(enclosure.node.nodeType)
			{
			case NT.STRUCT: {
				((DataTypeStruct)closure.dataType).finishDefinition(symbol.text, symbol.typeFrom.dataType);
			} break;
			case NT.ARGUMENTS: {
				var function = (NodeFunction)enclosureStack.ElementAt(1).node;
				var functionType = (DataTypeFunction)function.dataType;
				functionType.arguments[function.finishedArguments] = symbol.typeFrom.dataType;
				function.finishedArguments += 1;
			} goto case NT.FUNCTION; // Define the actual variable
			case NT.FUNCTION:
			case NT.GLOBAL: {
				symbol.dataType = new DataTypeReference(symbol.typeFrom.dataType);
				DataType.makeUnique(ref symbol.dataType);
				closure.scope.finishDefinition(symbol.text, symbol.dataType);
				instructions.Add(new InstructionAllocate(symbol.typeFrom.dataType));
			} break;
			default: throw Jolly.addError(symbol.location, "Cannot define a variable here");
			}
		} break;
		case NT.RETURN_VALUES: {
			var function = (NodeFunction)enclosure.node;
			var functionType = (DataTypeFunction)function.dataType;
			var tuple = function.returns as NodeTuple;
			if(tuple != null) {
				for(int i = 0; i < tuple.values.Count; i += 1) {
					functionType.returns[i] = tuple.values[i].dataType;
				}
			} else {
				functionType.returns[0] = function.returns.dataType;
			}
			
			if(function.argumentDefinitionCount > 0) {
				arguments.scope = function.scope;
				pushEnclosure(new Enclosure(arguments, function.argumentDefinitionCount + cursor));	
			} else {
				goto case NT.ARGUMENTS;
			}
		} break;
		case NT.ARGUMENTS: {
			// Make function type unique
			DataType.makeUnique(ref enclosure.node.dataType);
			// Skip to end of function enclosure
			cursor = enclosure.end;
		} break;
		case NT.MEMBER_TUPLE: break;
		case NT.FUNCTION: break;
		case NT.STRUCT: break;
		case NT.TUPLE: break;
		default: throw Jolly.addError(poppedEnclosure.node.location, "Internal compiler error: illigal node used as enclosure");
		}
	}
	
	static readonly NodeScope
		global = new NodeScope(new SourceLocation(), NT.GLOBAL, null, null),
		return_values = new NodeScope(new SourceLocation(), NT.RETURN_VALUES, null, null),
		arguments = new NodeScope(new SourceLocation(), NT.ARGUMENTS, null, null);
	
	public static List<Instruction> analyse(List<Node> program, Scope globalScope)
	{
		global.scope = globalScope;
		// instructions = new List<Node>(program.Count);
		instructions = new List<Instruction>();
		enclosureStack = new Stack<Enclosure>(16);
		
		pushEnclosure(new Enclosure(global, int.MaxValue));
		
		cursor = 0;
		for(Node node = program[cursor];
			cursor < program.Count;
			incrementCursor())
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
			incrementCursor())
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
		
	static readonly Dictionary<NT, Action<Node>>
		// Used for the first pass to define all the struct members
		typeDefinitionAnalysers = new Dictionary<NT, Action<Node>>() {
			{ NT.MEMBER_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.VARIABLE_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.FUNCTION, node => {
				NodeFunction function = (NodeFunction)node;
				return_values.scope = function.scope;
				pushEnclosure(new Enclosure(function, function.memberCount + cursor));
				pushEnclosure(new Enclosure(return_values, function.returnDefinitionCount + cursor));
			} },
			{ NT.STRUCT, node => {
				pushEnclosure(new Enclosure(node, ((NodeSymbol)node).memberCount + cursor));
			} },
			{ NT.OPERATOR, node => {
				NodeOperator op = (NodeOperator)node;
				Debug.Assert(op.operation == OT.GET_MEMBER);
				var result = operatorGetMember(ref op.a, op.b as NodeSymbol);
				op.dataType = result.Item1;
				op.typeKind = result.Item2;
			} },
			{ NT.BASETYPE, node => { } },
			{ NT.MEMBER_NAME, node => { } },
			{ NT.NAME, getTypeFromName },
			{ NT.TUPLE, node => {
				enclosureStack.Push(new Enclosure(node, ((NodeTuple)node).memberCount + cursor));
			} },
			{ NT.MODIFY_TYPE, node => {
				NodeModifyType tToRef = (NodeModifyType)node;
				tToRef.dataType = new DataTypeReference(tToRef.target.dataType);
				DataType.makeUnique(ref tToRef.dataType);
				tToRef.typeKind = tToRef.target.typeKind;
			} },
		},
		analysers = new Dictionary<NT, Action<Node>>() {
			{ NT.VARIABLE_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.STRUCT, skipSymbol },
			{ NT.FUNCTION, node => {
				NodeFunction function = (NodeFunction)node;
				pushEnclosure(new Enclosure(function, function.memberCount + cursor));
				instructions.Add(new InstructionFunction((DataTypeFunction)function.dataType));
				// Skip return type and argument definitions
				cursor += function.returnDefinitionCount + function.argumentDefinitionCount;
			} },
			{ NT.OPERATOR, node => {
				NodeOperator o = (NodeOperator)node;
				operatorAnalysers[o.operation](o);
			} },
			{ NT.FUNCTION_CALL, node => {
				var functionCall = (NodeFunctionCall)node;
				// getTypeFromName(functionCall);
				
				instructions.Add(new InstructionCall(){ arguments = functionCall.arguments.Select(a=>a.dataType).ToArray(), name = functionCall.text });
			} },
			{ NT.TUPLE, node => {
				pushEnclosure(new Enclosure(node, ((NodeTuple)node).memberCount + cursor));
			} },
			{ NT.MEMBER_TUPLE, node => {
				pushEnclosure(new Enclosure(node, ((NodeTuple)node).memberCount + cursor));
			} },
			{ NT.MEMBER_NAME, node => { } },
			{ NT.BASETYPE, node => { } },
			{ NT.LITERAL, node => { } },
			{ NT.NAME, getTypeFromName },
			{ NT.RETURN, node => {
				// TODO: Validate datatype's
				instructions.Add(new InstructionReturn());
			} },
		};
	
	static void skipSymbol(Node node)
		=> cursor += (node as NodeSymbol).memberCount;
	
	static void defineMemberOrVariable(Node node)
	{
		if(node.dataType != null) {
			skipSymbol(node);
			return;
		}
		pushEnclosure(new Enclosure(node, ((NodeSymbol)node).memberCount + cursor));
	}
	
	static readonly Dictionary<OT, Action<NodeOperator>>
		operatorAnalysers = new Dictionary<OT, Action<NodeOperator>>() {
			{ OT.MINUS,		 basicOperator },
			{ OT.PLUS,		 basicOperator },
			{ OT.MULTIPLY,	 basicOperator },
			{ OT.DIVIDE,	 basicOperator },
			{ OT.GET_MEMBER, op => {
				var result = operatorGetMember(ref op.a, op.b as NodeSymbol);
				op.dataType = result.Item1;
				op.typeKind = result.Item2;
			} },
			{ OT.ASSIGN, op => {
				load(ref op.b);
				
				var target = op.a.dataType as DataTypeReference;
				if(target == null) {
					throw Jolly.addError(op.a.location, "Cannot assign to this");
				}
				if(target.referenced != op.b.dataType ) {
					throw Jolly.addError(op.a.location, "Cannot assign this value type");
				}
				op.dataType = op.b.dataType;
				instructions.Add(new InstructionOperator(op));
			} },
			{ OT.REFERENCE, op => {
				if(op.a.typeKind != TypeKind.VALUE | !(op.a.dataType is DataTypeReference)) {
					throw Jolly.addError(op.location, "Cannot get a reference to this");
				}
				op.dataType = op.a.dataType;
				op.typeKind = TypeKind.ADDRES;
			} },
			{ OT.CAST, op => {
				load(ref op.b);
				if(op.a.typeKind != TypeKind.STATIC) {
					throw Jolly.addError(op.a.location, "Cannot cast to this");
				}
				op.typeKind = op.b.typeKind;
				op.dataType = op.a.dataType;
				instructions.Add(new InstructionOperator(op));
			} },
			{ OT.DEREFERENCE, op => {
				load(ref op.a);
				op.dataType = op.a.dataType;
			} },
			
		};
	
	static Tuple<DataType,TypeKind> operatorGetMember(ref Node a, NodeSymbol b)
	{
		if(b == null) {
			throw Jolly.addError(b.location, "The right-hand side of the period operator must be a name");
		}
		
		DataType resultType;
		TypeKind resultTypeKind;
		if(a.typeKind != TypeKind.STATIC)
		{
			var varType = ((DataTypeReference)a.dataType).referenced;
			var definition = varType.getDefinition(b.text);
			
			var refType = varType as DataTypeReference;
			if(definition == null && refType != null)
			{
				var resultNode = new NodeOperator(new SourceLocation(), OT.READ, a, null) { dataType = refType };
				
				instructions.Add(new InstructionOperator() {
					instruction = IT.LOAD,
					aType = a.dataType,
					resultType = refType
				});
				definition = refType.referenced.getDefinition(b.text);
				a = resultNode;
			}
			
			if(definition == null) {
				throw Jolly.addError(b.location, "Type does not contain a member {0}".fill(b.text));
			}
			
			resultTypeKind = definition.Value.typeKind;
			resultType = new DataTypeReference(definition.Value.dataType);
			DataType.makeUnique(ref resultType);
			// instructions.Add(new InstructionOperator(op));
		}
		else
		{
			// Get static member
			var definition = ((DataTypeStruct)a.dataType).structScope.getDefinition(b.text);
			if(definition == null) {
				throw Jolly.addError(b.location, "The type does not contain a member \"{0}\"".fill(b.text));
			}
			resultType = definition.Value.dataType;
			resultTypeKind = definition.Value.typeKind;
		}
		return new Tuple<DataType, TypeKind>(resultType, resultTypeKind);
	}
	
	static void basicOperator(NodeOperator op)
	{
		load(ref op.a);
		load(ref op.b);
		if(op.a.dataType != op.b.dataType ) {
			throw Jolly.addError(op.location, "Types not the same");
		}
		op.dataType = op.a.dataType;
		instructions.Add(new InstructionOperator(op));
	}
	
	static void load(ref Node node)
	{
		var refTo = node.dataType as DataTypeReference;
		if(refTo != null)
		{
			if(node.typeKind == TypeKind.STATIC) {
				throw Jolly.addError(node.location, "Cannot be used as value");
			}
			
			if(!refTo.referenced.isBaseType | node.typeKind == TypeKind.ADDRES) {
				return;
			}
			node.dataType = refTo.referenced;
			instructions.Add(new InstructionOperator() {
				instruction = IT.LOAD,
				aType = refTo,
				resultType = refTo.referenced
			});
		}
	}
	
	static void getTypeFromName(Node node)
	{
		// TODO: remove name part from function call
		if((node.nodeType == NT.NAME | node.nodeType == NT.FUNCTION_CALL) & node.dataType == null)
		{
			var closure = (NodeScope)enclosure.node;
			if(closure.nodeType == NT.MEMBER_TUPLE) {
				var tup = (NodeTuple)closure;
				// TODO: the ref will fuck shit up down the line
				var result = operatorGetMember(ref tup.scopeFrom, node as NodeSymbol);
				node.dataType = result.Item1;
				node.typeKind = result.Item2;
				return;
			}
			NodeSymbol name = (NodeSymbol)node;
			var definition = closure.getDefinition(name.text);
			
			if(definition == null) {
				throw Jolly.addError(name.location, "The name \"{0}\" does not exist in the current context".fill(name.text));
			}
			Debug.Assert(definition.Value.dataType != null);
			Debug.Assert(definition.Value.typeKind != TypeKind.UNDEFINED);
			
			node.dataType = definition.Value.dataType;
			node.typeKind = definition.Value.typeKind;
		}
	}
}
}