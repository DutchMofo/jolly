using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace Jolly
{
using System.Linq;
using NT = AST_Node.Type;
using OT = OperatorType;

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
		public Enclosure(NT t, AST_Scope n, Scope s, int e)
		{
			scope = s;
			type = t;
			node = n;
			end = e;
		}
		public AST_Scope node; // Node may be null
		public Scope scope;
		public int end;
		public NT type;
	}
	
	static void incrementCursor()
	{
		cursor += 1;
		while(enclosure.end < cursor) {
			enclosureEnd(enclosureStack.Pop());
		}
	}
	
	static int tempID = 0;
	public static Value newResult(Value _value)
		=> new Value{ type = _value.type, kind = _value.kind, tempID = tempID++ };
		
	public static bool valueIsStatic(Value val)
		=> val.kind == Value.Kind.STATIC_TYPE | val.kind == Value.Kind.STATIC_FUNCTION;
	
	static void enclosureEnd(Enclosure poppedEnclosure)
	{
		switch(poppedEnclosure.type)
		{
			case NT.VARIABLE_DEFINITION: {
				var closure = (AST_Scope)enclosure.node;
				var symbol = (AST_VariableDefinition)poppedEnclosure.node;
				
				switch(enclosure.type)
				{
					case NT.ARGUMENTS:
					case NT.FUNCTION:
					case NT.GLOBAL: {
						var variableValue = symbol.typeFrom.result;
						DataType resultRef = new DataType_Reference(variableValue.type);
						DataType.makeUnique(ref resultRef);
						symbol.result.type = resultRef;
						enclosure.scope.children[symbol.text] = symbol.result;
						instructions.Add(new IR_Allocate(){ type = variableValue.type, result = symbol.result });
						
						if(enclosure.type == NT.ARGUMENTS) {
							var function = (AST_Function)enclosureStack.ElementAt(1).node;
							var functionType = (DataType_Function)function.result.type;
							functionType.arguments[function.finishedArguments] = variableValue.type;
							function.finishedArguments += 1;
						}
					} break;
					case NT.STRUCT: {
						((DataType_Struct)closure.result.type).finishDefinition(symbol.text, symbol.typeFrom.result.type);
					} break;
					default: throw Jolly.addError(symbol.location, "Cannot define a variable here");
				}
			} break;
			case NT.RETURN_VALUES: {
				var function = (AST_Function)enclosureStack.ElementAt(1).node;
				var functionType = (DataType_Function)function.result.type;
				var tuple = function.returns as AST_Tuple;
				if(tuple != null) {
					for(int i = 0; i < tuple.values.Count; i += 1) {
						functionType.returns[i] = tuple.values[i].result.type;
					}
				} else {
					functionType.returns[0] = function.returns.result.type;
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
	
	public static List<IR> analyse(List<AST_Node> program, Scope globalScope)
	{
		instructions = new List<IR>();
		enclosureStack = new EnclosureStack(16);	
		enclosureStack.Push(new Enclosure(NT.GLOBAL, null, globalScope, int.MaxValue));
		
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
				enclosureStack.Push(new Enclosure(NT.FUNCTION,      function, function.scope, function.memberCount + cursor));
				enclosureStack.Push(new Enclosure(NT.ARGUMENTS,     null,     function.scope, function.returnCount + function.argumentCount + cursor));
				enclosureStack.Push(new Enclosure(NT.RETURN_VALUES, null,     function.scope, function.returnCount + cursor));
			} },
			{ NT.STRUCT, node => {
				var structNode = ((AST_Scope)node);
				enclosureStack.Push(new Enclosure(NT.STRUCT, structNode, structNode.scope, structNode.memberCount + cursor));
			} },
			{ NT.OPERATOR, node => {
				AST_Operator op = (AST_Operator)node;
				Debug.Assert(op.operation == OT.GET_MEMBER);
				op.result = operatorGetMember(ref op.a, op.b as AST_Symbol);
			} },
			{ NT.NAME, getTypeFromName },
			{ NT.MEMBER_NAME, node => { } },
			{ NT.TUPLE, node => {
				var tuple = (AST_Tuple)node;
				enclosureStack.Push(new Enclosure(tuple.nodeType, tuple, tuple.scope, ((AST_Tuple)node).memberCount + cursor));
			} },
			{ NT.MODIFY_TYPE, modifyType },
		},
		analysers = new Dictionary<NT, Action<AST_Node>>() {
			{ NT.VARIABLE_DEFINITION, node => defineMemberOrVariable(node) },
			{ NT.MODIFY_TYPE, modifyType },
			{ NT.STRUCT, skipSymbol },
			{ NT.FUNCTION, node => {
				AST_Function function = (AST_Function)node;
				tempID = ((DataType_Function)function.result.type).returns.Length + 1;
				instructions.Add(new IR_Function((DataType_Function)function.result.type));
				enclosureStack.Push(new Enclosure(NT.FUNCTION, function, function.scope, function.memberCount + cursor));
				cursor += function.returnCount;
			} },
			{ NT.OPERATOR, node => {
				AST_Operator o = (AST_Operator)node;
				operatorAnalysers[o.operation](o);
			} },
			{ NT.FUNCTION_CALL, node => {
				var functionCall = (AST_FunctionCall)node;
				// TODO: validate function call
				
				instructions.Add(new IR_Call(){ function = functionCall.function.result, arguments = functionCall.arguments.Select(a=>a.result).ToArray() });
			} },
			{ NT.TUPLE, node => {
				var tuple = (AST_Tuple)node;
				enclosureStack.Push(new Enclosure(tuple.nodeType, tuple, tuple.scope, tuple.memberCount + cursor));
			} },
			{ NT.MEMBER_TUPLE, node => {
				var tuple = (AST_Tuple)node;
				enclosureStack.Push(new Enclosure(tuple.nodeType, tuple, tuple.scope, tuple.memberCount + cursor));
			} },
			{ NT.MEMBER_NAME, node => { } },
			{ NT.NAME, getTypeFromName },
			{ NT.RETURN, node => {
				var returns = (AST_Return)node;
				Value[] values = (returns.values != null) ?
					(returns.values as AST_Tuple)?.values.Select(v=>v.result).ToArray() ?? new Value[] { returns.values.result } :
					new Value[0];
				
				AST_Function function = null;
				foreach(var closure in enclosureStack) {
					function = closure.node as AST_Function;
					if(function != null) break;
				}
				Debug.Assert(function != null);
				
				// if(function.returns)
				
				
				// TODO: Validate return values
				instructions.Add(new IR_Return{ values = values });
			} },
		};
	
	static void modifyType(AST_Node node)
	{
		AST_ModifyType mod = (AST_ModifyType)node;
		if((mod.target.result.type.flags & DataType.Flags.INSTANTIABLE) == 0) {
			throw Jolly.addError(mod.target.location, "The type {0} is not instantiable.".fill(mod.target.result.type));
		}
		if(mod.target.result.kind != Value.Kind.STATIC_TYPE) {
			throw Jolly.addError(mod.target.location, "Not a type");
		}
		
		switch(mod.toType) {
			case AST_ModifyType.TO_POINTER: mod.result.type = new DataType_Reference(mod.target.result.type); break;
			case AST_ModifyType.TO_ARRAY: Debug.Assert(false); break;
			case AST_ModifyType.TO_SLICE: Debug.Assert(false); break;
		}
		
		DataType.makeUnique(ref mod.result.type);
		mod.result.kind = mod.target.result.kind;
	}
	
	static void skipSymbol(AST_Node node)
		=> cursor += (node as AST_Symbol).memberCount;
	
	static void defineMemberOrVariable(AST_Node node)
	{
		if(node.result.type != null) {
			skipSymbol(node);
			return;
		}
		var symbol = (AST_VariableDefinition)node;
		enclosureStack.Push(new Enclosure(NT.VARIABLE_DEFINITION, symbol, symbol.scope, symbol.memberCount + cursor));
	}
	
	static bool implicitCast(AST_Node a, AST_Node b)
	{
		DataType aType = a.result.type,
				 bType = b.result.type;
		
		if(aType == bType) {
			return true;
		}
		
		if((aType.flags & bType.flags & DataType.Flags.BASE_TYPE) == 0) {
			
			//TODO: implicitly cast to inherited type (TODO: implement inheretance)
			return false;
		}
		
		if(aType is DataType_Reference | bType is DataType_Reference) {
			return false;
		}
		
		// Make a the biggest size
		if(bType.size > aType.size)
		{
			var swap1 = a;
			var swap2 = aType;
			a = b;
			aType = bType;
			b = swap1;
			bType = swap2;
		}
		
		//TODO: Finish
		
		return false;
	}
	
	static bool implicitCast(AST_Node a, DataType toType)
	{
		DataType aType = a.result.type;
		if(aType == toType) return true;
		
		
		return false;
	}
	
	static readonly Dictionary<OT, Action<AST_Operator>>
		operatorAnalysers = new Dictionary<OT, Action<AST_Operator>>() {
			{ OT.MINUS,		 basicOperator },
			{ OT.PLUS,		 basicOperator },
			{ OT.MULTIPLY,	 basicOperator },
			{ OT.DIVIDE,	 basicOperator },
			{ OT.SLICE,		 basicOperator },
			/*{ OT.PLUS,		 op => {
				load(op.a);
				load(op.b);
				if(!implicitCast(op.a, op.b)) {
					throw Jolly.addError(op.location, "Types not the same");
				}
				
				
			} },*/
			{ OT.GET_MEMBER, op => {
				op.result =  operatorGetMember(ref op.a, op.b as AST_Symbol);
			} },
			{ OT.ASSIGN, op => {
				assign(op.a, op.b);
				op.result = op.b.result;
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
		bool aIsTuple = a.nodeType == NT.TUPLE | a.nodeType == NT.MEMBER_TUPLE;
		bool bIsTuple = b.nodeType == NT.TUPLE | b.nodeType == NT.MEMBER_TUPLE;
		if(!aIsTuple & !bIsTuple)
		{
			load(b);
		
			if((a.triggers & AST_Node.Trigger.STORE) != 0) {
				// a.onStore(b.result);
			}
			
			var target = a.result.type as DataType_Reference;
			if(target == null) {
				throw Jolly.addError(a.location, "Cannot assign to this");
			}
			if(target.referenced != b.result.type ) {
				throw Jolly.addError(a.location, "Cannot assign this value type");
			}
			
			instructions.Add(new IR_Store{ location = a.result, _value = b.result, result = b.result });
		}
		else if(aIsTuple & bIsTuple)
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
		else if(aIsTuple & !bIsTuple) {
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
		
		Value result = new Value();
		if(!valueIsStatic(a.result))
		{
			int index;
			var varType = ((DataType_Reference)a.result.type).referenced;
			var definition = varType.getMember(b.text, out index);
			
			var refType = varType as DataType_Reference;
			if(definition == null && refType != null) {
				load(a);
				definition = refType.referenced.getMember(b.text, out index);
			}
			
			if(definition == null) {
				throw Jolly.addError(b.location, "Type does not contain a member {0}".fill(b.text));
			}
			
			result.kind = definition.Value.kind;
			result.type = new DataType_Reference(definition.Value.type);
			DataType.makeUnique(ref result.type);
			
			instructions.Add(new IR_GetMember{ _struct = a.result, index = index, result = result = newResult(result) });
		}
		else if(a.result.kind == Value.Kind.STATIC_TYPE)
		{
			// Get static member
			var definition = ((DataType_Struct)a.result.type).structScope.getDefinition(b.text);
			if(definition == null) {
				throw Jolly.addError(b.location, "The type does not contain a member \"{0}\"".fill(b.text));
			}
			return definition.Value;
		}
		else
		{
			throw Jolly.unexpected(a);
		}
		return result;
	}
	
	static void basicOperator(AST_Operator op)
	{
		load(op.a);
		load(op.b);
		if(op.a.result.type != op.b.result.type ) {
			throw Jolly.addError(op.location, "Types not the same");
		}
		op.result.type = op.a.result.type;
		// instructions.Add(new IR_Operator(op));
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
			Value result = newResult(node.result);
			result.type = refTo.referenced;
			instructions.Add(new IR_Load{ location = node.result, result = result });
			node.result = result;
		}
	}
	
	static void getTypeFromName(AST_Node node)
	{
		Debug.Assert(node.result.type == null);
		
		if(enclosure.type == NT.MEMBER_TUPLE) {
			var tup = (AST_Tuple)enclosure.node;
			var hacky = tup.membersFrom; // Prevent ref from messing things up
			node.result = operatorGetMember(ref hacky, node as AST_Symbol);
			return;
		}
		AST_Symbol name = (AST_Symbol)node;
		var definition = enclosure.scope.searchItem(name.text);
		
		if(definition == null) {
			throw Jolly.addError(name.location, "The name \"{0}\" does not exist in the current context".fill(name.text));
		}
		Debug.Assert(definition.Value.type != null);
		Debug.Assert(definition.Value.kind != Value.Kind.UNDEFINED);
		
		node.result = definition.Value;
	}
}
}