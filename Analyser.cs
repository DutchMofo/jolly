using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace Jolly
{
using System.Linq;
using NT = AST_Node.Type;
using OT = OperatorType;
using IT = Instruction.Type;

static class Analyser
{
	class EnclosureStack : Stack<Enclosure>
	{
		public EnclosureStack() { }
		public EnclosureStack(int size) : base(size) { }
		new public void Push(Enclosure e) => base.Push(Analyser.enclosure = e);
		new public Enclosure Pop()
		{
			var popped = base.Pop();
			Analyser.enclosure = base.Peek();
			return popped;
		}
	}
	
	static EnclosureStack enclosureStack;
	static List<Instruction> instructions;
	static Enclosure enclosure;
	static int cursor;
	
	struct Enclosure
	{
		public Enclosure(AST_Node n, int e) { node = n; end = e; }
		public AST_Node node;
		public int end;
	}
	
	static void incrementCursor()
	{
		cursor += 1;
		while(enclosure.end < cursor) {
			enclosureEnd(enclosureStack.Pop());
		}
	}
	
	static void enclosureEnd(Enclosure poppedEnclosure)
	{
		switch(poppedEnclosure.node.nodeType)
		{
		case NT.VARIABLE_DEFINITION: {
			var closure = (AST_Scope)enclosure.node;
			var symbol = (AST_VariableDefinition)poppedEnclosure.node;
			
			switch(enclosure.node.nodeType)
			{
			case NT.STRUCT: {
				((DataType_Struct)closure.dataType).finishDefinition(symbol.text, symbol.typeFrom.dataType);
			} break;
			case NT.ARGUMENTS: {
				var function = (AST_Function)enclosureStack.ElementAt(1).node;
				var functionType = (DataType_Function)function.dataType;
				functionType.arguments[function.finishedArguments] = symbol.typeFrom.dataType;
				function.finishedArguments += 1;
			} goto case NT.FUNCTION; // Define the actual variable
			case NT.FUNCTION:
			case NT.GLOBAL: {
				if((symbol.typeFrom.dataType.flags & DataType.Flags.INSTANTIABLE) == 0) {
					throw Jolly.addError(symbol.typeFrom.location, "The type {0} is not instantiable.".fill(symbol.typeFrom.dataType));
				}
				symbol.dataType = new DataType_Reference(symbol.typeFrom.dataType);
				DataType.makeUnique(ref symbol.dataType);
				closure.scope.finishDefinition(symbol.text, symbol.dataType);
				instructions.Add(new InstructionAllocate(symbol.typeFrom.dataType));
			} break;
			default: throw Jolly.addError(symbol.location, "Cannot define a variable here");
			}
		} break;
		case NT.RETURN_VALUES: {
			var function = (AST_Function)enclosure.node;
			var functionType = (DataType_Function)function.dataType;
			var tuple = function.returns as AST_Tuple;
			if(tuple != null) {
				for(int i = 0; i < tuple.values.Count; i += 1) {
					functionType.returns[i] = tuple.values[i].dataType;
				}
			} else {
				functionType.returns[0] = function.returns.dataType;
			}
			
			if(function.argumentDefinitionCount > 0) {
				arguments.scope = function.scope;
				enclosureStack.Push(new Enclosure(arguments, function.argumentDefinitionCount + cursor));	
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
	
	static readonly AST_Scope
		global = new AST_Scope(new SourceLocation(), NT.GLOBAL, null, null),
		return_values = new AST_Scope(new SourceLocation(), NT.RETURN_VALUES, null, null),
		arguments = new AST_Scope(new SourceLocation(), NT.ARGUMENTS, null, null);
	
	public static List<Instruction> analyse(List<AST_Node> program, Scope globalScope)
	{
		global.scope = globalScope;
		// instructions = new List<Node>(program.Count);
		instructions = new List<Instruction>();
		enclosureStack = new EnclosureStack(16);
		
		enclosureStack.Push(new Enclosure(global, int.MaxValue));
		
		cursor = 0;
		for(AST_Node node = program[cursor];
			cursor < program.Count;
			incrementCursor())
		{
			node = program[cursor];
			Action<AST_Node> action;
			if(typeDefinitionAnalysers.TryGetValue(node.nodeType, out action)) {
				action(node);
			}
		}
		
		cursor = 0;
		for(AST_Node node = program[cursor];
			cursor < program.Count;
			incrementCursor())
		{
			node = program[cursor];
			Action<AST_Node> action;
			if(!analysers.TryGetValue(node.nodeType, out action)) {
				throw Jolly.unexpected(node);
			}
			action(node);
		}
		
		return instructions;
	}
		
	static readonly Dictionary<NT, Action<AST_Node>>
		// Used for the first pass to define all the struct members
		typeDefinitionAnalysers = new Dictionary<NT, Action<AST_Node>>() {
			{ NT.MEMBER_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.VARIABLE_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.FUNCTION, node => {
				AST_Function function = (AST_Function)node;
				return_values.scope = function.scope;
				enclosureStack.Push(new Enclosure(function, function.memberCount + cursor));
				enclosureStack.Push(new Enclosure(return_values, function.returnDefinitionCount + cursor));
			} },
			{ NT.STRUCT, node => {
				enclosureStack.Push(new Enclosure(node, ((AST_Symbol)node).memberCount + cursor));
			} },
			{ NT.OPERATOR, node => {
				AST_Operator op = (AST_Operator)node;
				Debug.Assert(op.operation == OT.GET_MEMBER);
				var result = operatorGetMember(ref op.a, op.b as AST_Symbol);
				op.dataType = result.Item1;
				op.typeKind = result.Item2;
			} },
			{ NT.BASETYPE, node => { } },
			{ NT.MEMBER_NAME, node => { } },
			{ NT.NAME, getTypeFromName },
			{ NT.TUPLE, node => {
				enclosureStack.Push(new Enclosure(node, ((AST_Tuple)node).memberCount + cursor));
			} },
			{ NT.MODIFY_TYPE, node => {
				AST_ModifyType tToRef = (AST_ModifyType)node;
				if((tToRef.target.dataType.flags & DataType.Flags.INSTANTIABLE) == 0) {
					throw Jolly.addError(tToRef.target.location, "The type {0} is not instantiable.".fill(tToRef.target.dataType));
				}
				tToRef.dataType = new DataType_Reference(tToRef.target.dataType);
				DataType.makeUnique(ref tToRef.dataType);
				tToRef.typeKind = tToRef.target.typeKind;
			} },
		},
		analysers = new Dictionary<NT, Action<AST_Node>>() {
			{ NT.VARIABLE_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.STRUCT, skipSymbol },
			{ NT.FUNCTION, node => {
				AST_Function function = (AST_Function)node;
				enclosureStack.Push(new Enclosure(function, function.memberCount + cursor));
				instructions.Add(new InstructionFunction((DataType_Function)function.dataType));
				// Skip return type and argument definitions
				cursor += function.returnDefinitionCount + function.argumentDefinitionCount;
			} },
			{ NT.OPERATOR, node => {
				AST_Operator o = (AST_Operator)node;
				operatorAnalysers[o.operation](o);
			} },
			{ NT.FUNCTION_CALL, node => {
				var functionCall = (AST_FunctionCall)node;
				// TODO: validate function call
				// getTypeFromName(functionCall);
				
				instructions.Add(new InstructionCall(){ arguments = functionCall.arguments.Select(a=>a.dataType).ToArray() });
			} },
			{ NT.TUPLE, node => {
				enclosureStack.Push(new Enclosure(node, ((AST_Tuple)node).memberCount + cursor));
			} },
			{ NT.MEMBER_TUPLE, node => {
				enclosureStack.Push(new Enclosure(node, ((AST_Tuple)node).memberCount + cursor));
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
	
	static void skipSymbol(AST_Node node)
		=> cursor += (node as AST_Symbol).memberCount;
	
	static void defineMemberOrVariable(AST_Node node)
	{
		if(node.dataType != null) {
			skipSymbol(node);
			return;
		}
		enclosureStack.Push(new Enclosure(node, ((AST_Symbol)node).memberCount + cursor));
	}
	
	static readonly Dictionary<OT, Action<AST_Operator>>
		operatorAnalysers = new Dictionary<OT, Action<AST_Operator>>() {
			{ OT.MINUS,		 basicOperator },
			{ OT.PLUS,		 basicOperator },
			{ OT.MULTIPLY,	 basicOperator },
			{ OT.DIVIDE,	 basicOperator },
			{ OT.GET_MEMBER, op => {
				var result = operatorGetMember(ref op.a, op.b as AST_Symbol);
				op.dataType = result.Item1;
				op.typeKind = result.Item2;
			} },
			{ OT.ASSIGN, op => {
				assign(op.a, op.b);
				op.dataType = op.b.dataType;
			} },
			{ OT.REFERENCE, op => {
				if(op.a.typeKind != TypeKind.VALUE | !(op.a.dataType is DataType_Reference)) {
					throw Jolly.addError(op.location, "Cannot get a reference to this");
				}
				op.dataType = op.a.dataType;
				op.typeKind = TypeKind.ADDRES;
			} },
			{ OT.DEREFERENCE, op => {
				if(op.a.typeKind == TypeKind.ADDRES) {
					op.a.typeKind = TypeKind.VALUE;
				} else {
					load(op.a);
				}
				op.typeKind = op.a.typeKind;
				op.dataType = op.a.dataType;
			} },
			{ OT.CAST, op => {
				load(op.b);
				if(op.a.typeKind != TypeKind.STATIC) {
					throw Jolly.addError(op.a.location, "Cannot cast to this");
				}
				op.typeKind = op.b.typeKind;
				op.dataType = op.a.dataType;
				instructions.Add(new InstructionOperator(op));
			} },
			
		};
	
	static void assign(AST_Node a, AST_Node b)
	{
		bool aIsTup = a.nodeType == NT.TUPLE | a.nodeType == NT.MEMBER_TUPLE;
		bool bIsTup = b.nodeType == NT.TUPLE | b.nodeType == NT.MEMBER_TUPLE;
		if(!aIsTup & !bIsTup)
		{
			load(b);
		
			var target = a.dataType as DataType_Reference;
			if(target == null) {
				throw Jolly.addError(a.location, "Cannot assign to this");
			}
			if(target.referenced != b.dataType ) {
				throw Jolly.addError(a.location, "Cannot assign this value type");
			}
			
			instructions.Add(new InstructionOperator {
				instruction = IT.STORE,
				aType = a.dataType,
				bType = b.dataType,
				resultType = b.dataType,
			});
		}
		else if(aIsTup & bIsTup)
		{
			var aVals = ((AST_Tuple)a).values;
			var bVals = ((AST_Tuple)b).values;
			if(aVals.Count != bVals.Count) {
				throw Jolly.addError(a.location, "Tuple's not the same size");
			}
			for(int i = 0; i < aVals.Count; i += 1) {
				assign(aVals[i], bVals[i]);
			}
		}
		else if(aIsTup & !bIsTup) {
			foreach(AST_Node node in ((AST_Tuple)a).values) {
				assign(node, b);
			}
		} else {
			throw Jolly.addError(a.location, "Cannot assign tuple to variable");
		}
	}
	
	static Tuple<DataType,TypeKind> operatorGetMember(ref AST_Node a, AST_Symbol b)
	{
		if(b == null) {
			throw Jolly.addError(b.location, "The right-hand side of the period operator must be a name");
		}
		
		DataType resultType;
		TypeKind resultTypeKind;
		if(a.typeKind != TypeKind.STATIC)
		{
			var varType = ((DataType_Reference)a.dataType).referenced;
			var definition = varType.getDefinition(b.text);
			
			var refType = varType as DataType_Reference;
			if(definition == null && refType != null)
			{
				load(a);
				instructions.Add(new InstructionOperator() {
					instruction = IT.LOAD,
					aType = a.dataType,
					resultType = refType
				});
				definition = refType.referenced.getDefinition(b.text);
			}
			
			if(definition == null) {
				throw Jolly.addError(b.location, "Type does not contain a member {0}".fill(b.text));
			}
			
			resultTypeKind = definition.Value.typeKind;
			resultType = new DataType_Reference(definition.Value.dataType);
			DataType.makeUnique(ref resultType);
			
			instructions.Add(new InstructionOperator {
				instruction = IT.GET_MEMBER,
				aType = a.dataType,
				bType = resultType,
				resultType = resultType,
			});
		}
		else
		{
			// Get static member
			var definition = ((DataType_Struct)a.dataType).structScope.getDefinition(b.text);
			if(definition == null) {
				throw Jolly.addError(b.location, "The type does not contain a member \"{0}\"".fill(b.text));
			}
			resultType = definition.Value.dataType;
			resultTypeKind = definition.Value.typeKind;
		}
		return new Tuple<DataType, TypeKind>(resultType, resultTypeKind);
	}
	
	static void basicOperator(AST_Operator op)
	{
		load(op.a);
		load(op.b);
		if(op.a.dataType != op.b.dataType ) {
			throw Jolly.addError(op.location, "Types not the same");
		}
		op.dataType = op.a.dataType;
		instructions.Add(new InstructionOperator(op));
	}
	
	static void load(AST_Node node)
	{
		var refTo = node.dataType as DataType_Reference;
		if(refTo != null)
		{
			if(node.typeKind == TypeKind.STATIC) {
				throw Jolly.addError(node.location, "Cannot be used as value");
			}
			
			if(((refTo.referenced.flags & DataType.Flags.BASE_TYPE) == 0)  | node.typeKind == TypeKind.ADDRES) {
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
	
	static void getTypeFromName(AST_Node node)
	{
		// TODO: remove name part from function call
		if(node.nodeType == NT.NAME & node.dataType == null)
		{
			var closure = (AST_Scope)enclosure.node;
			if(closure.nodeType == NT.MEMBER_TUPLE) {
				var tup = (AST_Tuple)closure;
				var hacky = tup.membersFrom; // Prevent ref from messing things up
				var result = operatorGetMember(ref hacky, node as AST_Symbol);
				node.dataType = result.Item1;
				node.typeKind = result.Item2;
				return;
			}
			AST_Symbol name = (AST_Symbol)node;
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