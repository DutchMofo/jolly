using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace Jolly
{
using System.Linq;
using NT = AST_Node.Type;
using OT = OperatorType;
using IT = IR.Type;

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
	static List<IR> instructions;
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
	
	static int tempID = 0;
	static Value newResult(Value _value)
		=> new Value{ type = _value.type, kind = _value.kind, tempID = tempID++ };
	static Value newResult()
		=> new Value{ tempID = tempID++ };
	
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
				((DataType_Struct)closure.result.type).finishDefinition(symbol.text, symbol.typeFrom.result.type);
			} break;
			case NT.ARGUMENTS: {
				var function = (AST_Function)enclosureStack.ElementAt(1).node;
				var functionType = (DataType_Function)function.result.type;
				functionType.arguments[function.finishedArguments] = symbol.typeFrom.result.type;
				function.finishedArguments += 1;
			} goto case NT.FUNCTION; // Define the actual variable
			case NT.FUNCTION:
			case NT.GLOBAL: {
				if((symbol.typeFrom.result.type.flags & DataType.Flags.INSTANTIABLE) == 0) {
					throw Jolly.addError(symbol.typeFrom.location, "The type {0} is not instantiable.".fill(symbol.typeFrom.result.type));
				}
				symbol.result.type = new DataType_Reference(symbol.typeFrom.result.type);
				DataType.makeUnique(ref symbol.result.type);
				closure.scope.finishDefinition(symbol.text, symbol.result.type);
				instructions.Add(new IR_Allocate(symbol.typeFrom.result.type));
			} break;
			default: throw Jolly.addError(symbol.location, "Cannot define a variable here");
			}
		} break;
		case NT.RETURN_VALUES: {
			var function = (AST_Function)enclosure.node;
			var functionType = (DataType_Function)function.result.type;
			var tuple = function.returns as AST_Tuple;
			if(tuple != null) {
				for(int i = 0; i < tuple.values.Count; i += 1) {
					functionType.returns[i] = tuple.values[i].result.type;
				}
			} else {
				functionType.returns[0] = function.returns.result.type;
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
			DataType.makeUnique(ref enclosure.node.result.type);
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
	
	public static List<IR> analyse(List<AST_Node> program, Scope globalScope)
	{
		global.scope = globalScope;
		// instructions = new List<Node>(program.Count);
		instructions = new List<IR>();
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
				op.result = result;
			} },
			{ NT.BASETYPE, node => { } },
			{ NT.MEMBER_NAME, node => { } },
			{ NT.NAME, getTypeFromName },
			{ NT.TUPLE, node => {
				enclosureStack.Push(new Enclosure(node, ((AST_Tuple)node).memberCount + cursor));
			} },
			{ NT.MODIFY_TYPE, node => {
				AST_ModifyType tToRef = (AST_ModifyType)node;
				if((tToRef.target.result.type.flags & DataType.Flags.INSTANTIABLE) == 0) {
					throw Jolly.addError(tToRef.target.location, "The type {0} is not instantiable.".fill(tToRef.target.result.type));
				}
				tToRef.result.type = new DataType_Reference(tToRef.target.result.type);
				DataType.makeUnique(ref tToRef.result.type);
				tToRef.result.kind = tToRef.target.result.kind;
			} },
		},
		analysers = new Dictionary<NT, Action<AST_Node>>() {
			{ NT.VARIABLE_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.STRUCT, skipSymbol },
			{ NT.FUNCTION, node => {
				AST_Function function = (AST_Function)node;
				enclosureStack.Push(new Enclosure(function, function.memberCount + cursor));
				instructions.Add(new IR_Function((DataType_Function)function.result.type));
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
				
				instructions.Add(new IR_Call(){ arguments = functionCall.arguments.Select(a=>a.result).ToArray() });
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
				instructions.Add(new IR_Return());
			} },
		};
	
	static void skipSymbol(AST_Node node)
		=> cursor += (node as AST_Symbol).memberCount;
	
	static void defineMemberOrVariable(AST_Node node)
	{
		if(node.result.type != null) {
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
				op.result =  operatorGetMember(ref op.a, op.b as AST_Symbol);
			} },
			{ OT.ASSIGN, op => {
				assign(op.a, op.b);
				op.result.type = op.b.result.type;
			} },
			{ OT.REFERENCE, op => {
				if(op.a.result.kind != Value.Kind.VALUE | !(op.a.result.type is DataType_Reference)) {
					throw Jolly.addError(op.location, "Cannot get a reference to this");
				}
				op.result.type = op.a.result.type;
				op.result.kind = Value.Kind.ADDRES;
			} },
			{ OT.DEREFERENCE, op => {
				if(op.a.result.kind == Value.Kind.ADDRES) {
					op.a.result.kind = Value.Kind.VALUE;
				} else {
					load(op.a);
				}
				op.result.kind = op.a.result.kind;
				op.result.type = op.a.result.type;
			} },
			{ OT.CAST, op => {
				load(op.b);
				if(op.a.result.kind != Value.Kind.STATIC_TYPE) {
					throw Jolly.addError(op.a.location, "Cannot cast to this");
				}
				op.result.kind = op.b.result.kind;
				op.result.type = op.a.result.type;
				Value toValue = new Value{ kind = Value.Kind.STATIC_TYPE, type = op.a.result.type };
				instructions.Add(new IR_Cast{ _value = op.b.result, type = toValue, result = toValue });
			} },
			
		};
	
	static void assign(AST_Node a, AST_Node b)
	{
		bool aIsTup = a.nodeType == NT.TUPLE | a.nodeType == NT.MEMBER_TUPLE;
		bool bIsTup = b.nodeType == NT.TUPLE | b.nodeType == NT.MEMBER_TUPLE;
		if(!aIsTup & !bIsTup)
		{
			load(b);
		
			var target = a.result.type as DataType_Reference;
			if(target == null) {
				throw Jolly.addError(a.location, "Cannot assign to this");
			}
			if(target.referenced != b.result.type ) {
				throw Jolly.addError(a.location, "Cannot assign this value type");
			}
			
			instructions.Add(new IR_STORE{ location = a.result, _value = b.result, result = newResult(b.result) });
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
	
	static Value operatorGetMember(ref AST_Node a, AST_Symbol b)
	{
		if(b == null) {
			throw Jolly.addError(b.location, "The right-hand side of the period operator must be a name");
		}
		
		Value result = newResult();
		if(a.result.kind != Value.Kind.STATIC_TYPE)
		{
			var varType = ((DataType_Reference)a.result.type).referenced;
			var definition = varType.getDefinition(b.text);
			
			var refType = varType as DataType_Reference;
			if(definition == null && refType != null)
			{
				load(a);
				instructions.Add(new IR_Operator() {
					instruction = IT.LOAD,
					aType = a.result.type,
					resultType = refType
				});
				definition = refType.referenced.getDefinition(b.text);
			}
			
			if(definition == null) {
				throw Jolly.addError(b.location, "Type does not contain a member {0}".fill(b.text));
			}
			
			result.kind = definition.Value.kind;
			result.type = new DataType_Reference(definition.Value.type);
			DataType.makeUnique(ref result.type);
			
			instructions.Add(new IR_Operator {
				instruction = IT.GET_MEMBER,
				aType = a.result.type,
				bType = result.type,
				resultType = resultType,
			});
		}
		else
		{
			// Get static member
			var definition = ((DataType_Struct)a.result.type).structScope.getDefinition(b.text);
			if(definition == null) {
				throw Jolly.addError(b.location, "The type does not contain a member \"{0}\"".fill(b.text));
			}
			result = definition.Value;
		}
		return newResult(result);
	}
	
	static void basicOperator(AST_Operator op)
	{
		load(op.a);
		load(op.b);
		if(op.a.result.type != op.b.result.type ) {
			throw Jolly.addError(op.location, "Types not the same");
		}
		op.result.type = op.a.result.type;
		instructions.Add(new IR_Operator(op));
	}
	
	static void load(AST_Node node)
	{
		var refTo = node.result.type as DataType_Reference;
		if(refTo != null)
		{
			if(node.result.kind == Value.Kind.STATIC_TYPE) {
				throw Jolly.addError(node.location, "Cannot be used as value");
			}
			
			if(((refTo.referenced.flags & DataType.Flags.BASE_TYPE) == 0)  | node.result.kind == Value.Kind.ADDRES) {
				return;
			}
			node.result.type = refTo.referenced;
			instructions.Add(new IR_Operator() {
				instruction = IT.LOAD,
				aType = refTo,
				resultType = refTo.referenced
			});
		}
	}
	
	static void getTypeFromName(AST_Node node)
	{
		// TODO: remove name part from function call
		if(node.nodeType == NT.NAME & node.result.type == null)
		{
			var closure = (AST_Scope)enclosure.node;
			if(closure.nodeType == NT.MEMBER_TUPLE) {
				var tup = (AST_Tuple)closure;
				var hacky = tup.membersFrom; // Prevent ref from messing things up
				node.result = operatorGetMember(ref hacky, node as AST_Symbol);
				return;
			}
			AST_Symbol name = (AST_Symbol)node;
			var definition = closure.getDefinition(name.text);
			
			if(definition == null) {
				throw Jolly.addError(name.location, "The name \"{0}\" does not exist in the current context".fill(name.text));
			}
			Debug.Assert(definition.Value.type != null);
			Debug.Assert(definition.Value.kind != Value.Kind.UNDEFINED);
			
			node.result = definition.Value;
		}
	}
}
}