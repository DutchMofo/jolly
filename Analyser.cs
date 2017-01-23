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
	[ThreadStatic] static Stack<NodeSymbol> scopeStack;
	[ThreadStatic] static List<Node> instructions;
	[ThreadStatic] static List<Node> program;
	// Used to store the intermediary node nessasary for looking up the correct datatype
	// of the variable about to be defined
	[ThreadStatic] static Node definitionInstruction;
	[ThreadStatic] static int cursor;
		
	public static List<Node> analyse(List<Node> program)
	{
		Analyser.program = program;
		scopeStack = new Stack<NodeSymbol>(16);
		instructions = new List<Node>(program.Count);
		// instructions = new List<Instruction>(program.Count);
				
		cursor = 0;
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
			int debug = cursor;
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
		// if(scopeHeader.nodeType != NT.STRUCT) {
		// 	DataType.makeUnique(ref scopeHeader.dataType);
		// }
	}
	
	static readonly Dictionary<NT, Action<Node>>
		// Used for the first pass to define all the struct members
		typeDefinitionAnalysers = new Dictionary<NT, Action<Node>>() {
			{ NT.MEMBER_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.VARIABLE_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.SCOPE_END, node => scopeEnd(scopeStack.Pop()) },
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
					((NodeTupple)definitionInstruction).values.Select(n => n.dataType ).ToArray() :
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
				tToRef.target.dataType = new DataTypeReference(tToRef.target.dataType);
				DataType.makeUnique(ref tToRef.target.dataType);
				tToRef.typeKind = tToRef.target.typeKind;
				definitionInstruction = tToRef.target;
			} },
		},
		analysers = new Dictionary<NT, Action<Node>>() {
			{ NT.SCOPE_END, node => scopeEnd(scopeStack.Pop()) },
			{ NT.VARIABLE_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.STRUCT, skipSymbol },
			{ NT.FUNCTION, node => {
				NodeFunction function = (NodeFunction)node;
				scopeStack.Push(function);
				cursor += function.returnDefinitionCount + function.argumentDefinitionCount;
				instructions.Add(function);
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
		=> cursor += (node as NodeSymbol).memberCount;
	
	static DataType defineMemberOrVariable(Node node)
	{
		if(node.dataType != null)
			return null;
		NodeSymbol symbol = (NodeSymbol)node;
		
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
		
		if(symbol.typeKind != TypeKind.STATIC) {
			DataType refToData = new DataTypeReference(symbol.dataType);
			DataType.makeUnique(ref refToData);
			symbol.definitionScope.children[symbol.text].dataType = refToData;
		} else {
			var structType = (DataTypeStruct)symbol.definitionScope.dataType;
			structType.members[structType.memberMap[symbol.text]] = symbol.dataType;
		}
				
		instructions.Add(symbol);
		return symbol.dataType;
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
				getTypeFromName(ref op.b);
				
				var target = op.a.dataType as DataTypeReference;
				if(target == null) {
					throw Jolly.addError(op.a.location, "Cannot assign to this");
				}
				if(target.referenced != op.b.dataType ) {
					throw Jolly.addError(op.a.location, "Cannot assign this value type");
				}
				op.result.dataType = op.b.dataType;
				instructions.Add(op);
				// instructions.Add(new InstructionOperator(op));
			} },
			{ OT.REFERENCE, op => {
				getTypeFromName(ref op.a);
				var reference = (DataType)new DataTypeReference(op.a.dataType);
				DataType.makeUnique(ref reference);
				op.result.dataType = reference;
				instructions.Add(op);
				// instructions.Add(new InstructionOperator(op));
			} },
			{ OT.CAST, op => {
				getTypeFromName(ref op.a);
				getTypeFromName(ref op.b);
				op.result.dataType = op.b.dataType;
				instructions.Add(op);
				// instructions.Add(new InstructionOperator(op));
			} },
		};
	
	static void operatorGetMember(NodeOperator op)
	{
		Node a = op.a;
		NodeSymbol b = op.b as NodeSymbol;
		
		getTypeFromName(ref a, false);
		if(a.dataType == null) {
			throw Jolly.addError(a.location, "Can't load type");
		}
		if(b == null) {
			throw Jolly.addError(op.b.location, "The right-hand side of the period operator must be a name");
		}
		
		Debug.Assert(a.typeKind != TypeKind.VALUE);
		Debug.Assert(a.typeKind != TypeKind.UNDEFINED);
		
		if(a.typeKind != TypeKind.STATIC)
		{
			var varType = ((DataTypeReference)a.dataType).referenced;
			DataType result = varType.getMember(b.text);
			
			var refType = varType as DataTypeReference;
			if(result == null && refType != null) {
				var resultNode = new NodeResult(new SourceLocation()) { dataType = refType };
				instructions.Add(new NodeOperator(new SourceLocation(), OT.DEREFERENCE, op.a, null, resultNode));
				result = refType.referenced.getMember(b.text);
				op.a = resultNode;
			}
			
			if(result == null) {
				throw Jolly.addError(b.location, "Type does not contain a member {0}".fill(b.text));
			}
			
			op.result.dataType = new DataTypeReference(result);
			DataType.makeUnique(ref op.result.dataType);
			instructions.Add(op);
		}
		else
		{
			/* Get static member */
			op.result.dataType = op.a.dataType.getChild(b.text);
			if(op.result.dataType == null) {
				throw Jolly.addError(b.location, "The type does not contain a member \"{0}\"".fill(b.text));
			}
		}
	}
	
	static void basicOperator(NodeOperator op)
	{
		getTypeFromName(ref op.a);
		getTypeFromName(ref op.b);
		if(op.a.dataType != op.b.dataType ) {
			throw Jolly.addError(op.location, "Types not the same");
		}
		if(op.a.typeKind == TypeKind.STATIC | op.b.typeKind == TypeKind.STATIC) {
			throw Jolly.addError(op.location, "ERR STATIC TYPE AND STUFF");
		}
		op.result.dataType = op.a.dataType;
		instructions.Add(op);
	}
	
	static bool getTypeFromName(ref Node node, bool load = true)
	{
		if(node.nodeType == NT.NAME & node.dataType == null)
		{
			Debug.Assert(node.dataType == null);
			NodeSymbol name = (NodeSymbol)node;
			var item = name.definitionScope.searchItem(name.text);
			
			if(item == null) {
				throw Jolly.addError(name.location, "The name \"{0}\" does not exist in the current context".fill(name.text));
			}
			Debug.Assert(item.dataType != null);
			Debug.Assert(item.typeKind != TypeKind.UNDEFINED);
			
			if(item.typeKind != TypeKind.STATIC)
			{
				DataTypeReference refTo = item.dataType as DataTypeReference;
				if(refTo != null & load) {
					node = new NodeResult(name.location) { dataType = refTo.referenced };
					node.typeKind = TypeKind.VALUE;
					instructions.Add(new NodeOperator(name.location, OT.READ, item.node, null, result: node));
				} else {
					node.dataType = item.dataType;
				}
			} else {
				node.dataType = item.dataType;
			}
			node.typeKind = item.typeKind;
			return true;
		}
		return false;
	}
}
}