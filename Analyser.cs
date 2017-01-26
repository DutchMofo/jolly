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
	[ThreadStatic] static List<Instruction> instructions;
	[ThreadStatic] static List<Node> program;
	// Used to store the intermediary node nessasary for looking up the correct datatype
	// of the variable about to be defined
	[ThreadStatic] static Node definitionInstruction;
	[ThreadStatic] static int cursor;
	
	struct Enclosure
	{
		public Enclosure(Node n, int e) { node = n; end = e; }
		public Node node;
		public int end;
	}
	
	[ThreadStatic] static Stack<Enclosure> enclosureStack;
	
	static void incrementCursor()
	{
		cursor += 1;
		while(enclosureStack.Count > 0 && enclosureStack.Peek().end < cursor)
			enclosureEnd(enclosureStack.Pop());
	}
	
	static void enclosureEnd(Enclosure enclosure)
	{
		"".ToString();
	}
	
	public static List<Instruction> analyse(List<Node> program)
	{
		Analyser.program = program;
		// instructions = new List<Node>(program.Count);
		instructions = new List<Instruction>();
		enclosureStack = new Stack<Enclosure>(16);
		
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
		if(scopeHeader.nodeType == NT.STRUCT) {
			var structType = (DataTypeStruct)scopeHeader.dataType;
			instructions.Add(new InstructionStruct() { structType = structType });
		}
	}
	
	static readonly Dictionary<NT, Action<Node>>
		// Used for the first pass to define all the struct members
		typeDefinitionAnalysers = new Dictionary<NT, Action<Node>>() {
			{ NT.MEMBER_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.VARIABLE_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.FUNCTION, node => {
				NodeFunction function = (NodeFunction)node;
				int startCursor = cursor, end = (cursor += 1) + function.returnDefinitionCount;
				for(; cursor < end; incrementCursor())
				{
					node = program[cursor];
					Action<Node> action;
					if(variableDefinitionAnalysers.TryGetValue(node.nodeType, out action)) {
						action(node);
					} else {
						throw Jolly.unexpected(node);
					}
				}
				var returns = (definitionInstruction.nodeType == NT.TUPPLE) ?
					((NodeTupple)definitionInstruction).values.Select(n => n.dataType ).ToArray() :
					new DataType[] { definitionInstruction.dataType };
				definitionInstruction = null;
				
				end = cursor + function.argumentDefinitionCount - 1;
				List<DataType> fuckit = new List<DataType>();
				for(; cursor < end; incrementCursor())
				{
					var definition = program[cursor] as NodeSymbol;
					if(definition == null) {
						throw Jolly.unexpected(node);
					}
					fuckit.Add(defineMemberOrVariable(definition));
				}
				cursor -= 1;
				var arguments = fuckit.ToArray();
				
				function.dataType = new DataTypeFunction(returns, arguments) { name = function.text };
				DataType.makeUnique(ref function.dataType);
				function.scope.finishDefinition(function.text, function.dataType);
				cursor = startCursor + function.memberCount;			
			} },
			{ NT.STRUCT, node => {
				enclosureStack.Push(new Enclosure(node, ((NodeSymbol)node).memberCount + cursor));
			} },
		},
		// Used to load the type before defining a variable
		variableDefinitionAnalysers = new Dictionary<NT, Action<Node>>() {
			{ NT.OPERATOR, node => {
				NodeOperator o = (NodeOperator)node;
				Debug.Assert(o.operation == OT.GET_MEMBER);
				operatorGetMember(o);
				definitionInstruction = o;
			} },
			{ NT.BASETYPE, node => {
				definitionInstruction = node;
			} },
			{ NT.MEMBER_NAME, node => { } },
			{ NT.NAME, node => {
				getTypeFromName(node);
				definitionInstruction = node;
			} },
			{ NT.TUPPLE, node => {
				// enclosureStack.Push(new Enclosure(node, ));
				definitionInstruction = node;
			} },
			{ NT.MODIFY_TYPE, node => {
				NodeModifyType tToRef = (NodeModifyType)node;
				tToRef.dataType = new DataTypeReference(tToRef.target.dataType);
				DataType.makeUnique(ref tToRef.dataType);
				tToRef.typeKind = tToRef.target.typeKind;
				definitionInstruction = tToRef;
			} },
		},
		analysers = new Dictionary<NT, Action<Node>>() {
			{ NT.VARIABLE_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.STRUCT, skipSymbol },
			{ NT.FUNCTION, node => {
				NodeFunction function = (NodeFunction)node;
				enclosureStack.Push(new Enclosure(function, function.memberCount + cursor));
				
				cursor += function.returnDefinitionCount + function.argumentDefinitionCount;
				instructions.Add(new InstructionFunction((DataTypeFunction)function.dataType));
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
	
	static DataType defineMemberOrVariable(Node node)
	{
		if(node.dataType != null)
			return null;
		NodeSymbol symbol = (NodeSymbol)node;
		
		int end = symbol.memberCount + cursor;
		for(cursor += 1; cursor <= end; incrementCursor())
		{
			node = program[cursor];
			Action<Node> action;
			if(variableDefinitionAnalysers.TryGetValue(node.nodeType, out action)) {
				action(node);
			} else {
				throw Jolly.unexpected(node);
			}
		}
		cursor -= 1;
		
		Debug.Assert(definitionInstruction.dataType != null);
		symbol.dataType = definitionInstruction.dataType;
		
		if(symbol.typeKind != TypeKind.STATIC) {
			DataType refToData = new DataTypeReference(symbol.dataType);
			DataType.makeUnique(ref refToData);
			symbol.scope.finishDefinition(symbol.text, refToData);
			instructions.Add(new InstructionAllocate(symbol.dataType));
		} else {
			symbol.scope.dataType.finishDefinition(symbol.text, symbol.dataType);
		}
		return symbol.dataType;
	}
	
	static readonly Dictionary<OT, Action<NodeOperator>>
		operatorAnalysers = new Dictionary<OT, Action<NodeOperator>>() {
			{ OT.GET_MEMBER, operatorGetMember },
			{ OT.MINUS,		 basicOperator },
			{ OT.PLUS,		 basicOperator },
			{ OT.MULTIPLY,	 basicOperator },
			{ OT.DIVIDE,	 basicOperator },
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
		};
	
	static void operatorGetMember(NodeOperator op)
	{
		Node a = op.a;
		NodeSymbol b = op.b as NodeSymbol;
		
		if(b == null) {
			throw Jolly.addError(op.b.location, "The right-hand side of the period operator must be a name");
		}
		
		Debug.Assert(a.typeKind != TypeKind.UNDEFINED);
		
		if(a.typeKind != TypeKind.STATIC)
		{
			var varType = ((DataTypeReference)a.dataType).referenced;
			var definition = varType.getDefinition(b.text);
			
			var refType = varType as DataTypeReference;
			if(definition == null && refType != null)
			{
				var resultNode = new NodeOperator(new SourceLocation(), OT.READ, op.a, null) { dataType = refType };
				
				instructions.Add(new InstructionOperator() {
					instruction = IT.LOAD,
					aType = op.a.dataType,
					resultType = refType
				});
				// instructions.Add(new NodeOperator(new SourceLocation(), OT.DEREFERENCE, op.a, null, resultNode));
				definition = refType.referenced.getDefinition(b.text);
				op.a = resultNode;
			}
			
			if(definition == null) {
				throw Jolly.addError(b.location, "Type does not contain a member {0}".fill(b.text));
			}
			
			op.dataType = new DataTypeReference(definition.Value.dataType);
			DataType.makeUnique(ref op.dataType);
			instructions.Add(new InstructionOperator(op));
		}
		else
		{
			// Get static member
			var definition = ((DataTypeStruct)op.a.dataType).structScope.getDefinition(b.text);
			if(definition == null) {
				throw Jolly.addError(b.location, "The type does not contain a member \"{0}\"".fill(b.text));
			}
			op.typeKind = definition.Value.typeKind;
			op.dataType = definition.Value.dataType;
		}
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
		if((node.nodeType == NT.NAME | node.nodeType == NT.FUNCTION_CALL) & node.dataType == null)
		{
			Debug.Assert(node.dataType == null);
			NodeSymbol name = (NodeSymbol)node;
			var definition = name.scope.searchItem(name.text);
			
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