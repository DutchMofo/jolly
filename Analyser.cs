using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace Jolly
{
using System.Linq;
using NT = AST_Node.Type;

static class Analyser
{
	class EnclosureStack : Stack<Enclosure>
	{
		public EnclosureStack() { }
		public EnclosureStack(int size) : base(size) { }
		new public void Push(Enclosure e) => base.Push(enclosure = e);
		new public Enclosure Pop()
		{
			var popped = base.Pop();
			enclosure = base.Peek();
			return popped;
		}
	}
	
	class ContextStack : Stack<Context>
	{
		public ContextStack(int size) : base(size) { }
		new public void Push(Context c) => base.Push(context = c);
		new public Context Pop()
		{
			var popped = base.Pop();
			context = base.Peek();
			return popped;
		}
	}
	
	static ContextStack contextStack;
	static Context context;
	static EnclosureStack enclosureStack;
	static Enclosure enclosure;
	static List<IR> instructions;
	static int cursor;
	
	struct Enclosure
	{
		public Enclosure(NT t, AST_Node n, SymbolTable s, int e)
		{
			scope = s;
			type = t;
			node = n;
			end = e;
		}
		public SymbolTable scope;
		public AST_Node node; // Node may be null
		public int end;
		public NT type;
	}
	
	static void incrementCursor()
	{
		cursor += 1;
		
		while(context.index < cursor) {
			contextEnd(context);
		}
		while(enclosure.end < cursor) {
			enclosureEnd(enclosureStack.Pop());
		}
	}
	
	static int tempID = 0;
	public static Value newResult(Value _value)
		=> new Value{ type = _value.type, kind = _value.kind, tempID = tempID++ };
		
	public static bool valueIsStatic(Value val)
		=> val.kind == Value.Kind.STATIC_TYPE | val.kind == Value.Kind.STATIC_FUNCTION;
	
	public static List<IR> analyse(List<AST_Node> program, SymbolTable globalScope)
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
	
	static void enclosureEnd(Enclosure ended)
	{
		switch(ended.type)
		{
			case NT.MEMBER_TUPLE: break;
			case NT.FUNCTION: break;
			case NT.STRUCT: break;
			case NT.TUPLE: break;
			default: throw Jolly.addError(ended.node.location, "Internal compiler error: illigal node used as enclosure");
		}
	}
	
	static void contextEnd(Context ended)
	{
		switch(ended.kind)
		{
			case Context.Kind.DECLARATION: {
				// type inference
				var declaration = (AST_Declaration)ended.target;
				
				if(declaration.result.type == Lookup.AUTO) {
					throw Jolly.addError(declaration.location, "Implicitly-typed variables must be initialized.");
				}
				
			} break;
			case Context.Kind.FUNCTION_DECLARATION: {
				// The definition ends after the return values and arguments are parsed.
				var function = (AST_Function)enclosure.node;
				var functionType = (DataType_Function)function.result.type;
				var tuple = function.returns as AST_Tuple;
				if(tuple != null) {
					tuple.values.forEach((v, i) => functionType.returns[i] = v.result.type);
				} else {
					functionType.returns[0] = function.returns.result.type;
				}
				
				DataType.makeUnique(ref function.result.type);
				cursor = enclosure.end; // Skip to end of function enclosure
			} break;
			case Context.Kind.OBJECT: {
				cursor = ((AST_Object)ended.target).resetIndex;
			} break;
		}
	}
	
	static readonly Dictionary<NT, Action<AST_Node>>
		// Used for the first pass to define all the struct members
		typeDefinitionAnalysers = new Dictionary<NT, Action<AST_Node>>() {
			{ NT.DEFINITION, node => declare(node) },
			{ NT.FUNCTION, node => {
				var function = (AST_Function)node;
				var table = (SymbolTable)function.symbol;
				tempID = 1;
				// Allocate id's
				foreach(var allocation in table.allocations) {
					allocation.result.tempID = tempID++;
				}
				enclosureStack.Push(new Enclosure(NT.FUNCTION, function, table, function.memberCount + cursor));
				contextStack.Push(new Context(function.definitionCount + cursor, Context.Kind.FUNCTION_DECLARATION));
			} },
			{ NT.STRUCT, node => {
				var structNode = (AST_Scope)node;
				var table = (SymbolTable)structNode.symbol;
				instructions.Add(new IR_Struct{ structType = (DataType_Struct)structNode.result.type });
				enclosureStack.Push(new Enclosure(NT.STRUCT, structNode, table, structNode.memberCount + cursor));
			} },
			{ NT.GET_MEMBER, node => {
				var op = (AST_Operation)node;
				op.result =  operatorGetMember(ref op.a, op.b as AST_Symbol);
			} },
			{ NT.NAME, getTypeFromName },
			{ NT.MEMBER_NAME, node => { } },
			{ NT.TUPLE, node => {
				// var tuple = (AST_Tuple)node;
				// enclosureStack.Push(new Enclosure(tuple.nodeType, tuple, enclosure.scope, tuple.memberCount + cursor));
			} },
			{ NT.MODIFY_TYPE, modifyType },
		},
		analysers = new Dictionary<NT, Action<AST_Node>>() {
			{ NT.DEFINITION, node => declare(node) },
			{ NT.MODIFY_TYPE, modifyType },
			{ NT.STRUCT, skipSymbol },
			{ NT.OBJECT, node => {
				var _object = (AST_Object)node;
				_object.onStored = storeObject;
				_object.startIndex = cursor;
				cursor += _object.memberCount;
			} },
			{ NT.INITIALIZER, node => {
				var init = (AST_Operation)node;
				assign(init.a, init.b);
			} },
			{ NT.FUNCTION, node => {
				var function = (AST_Function)node;
				var table = (SymbolTable)function.symbol;
				instructions.Add(new IR_Function((DataType_Function)function.result.type));
				
				// TODO: Is this necessary?
				// foreach(var returns in ((DataType_Function)function.result.type).returns) {
				// 	instructions.Add(new IR_Allocate{ type = returns, result = new Value{ tempID = tempID++, type = returns } });
				// }
				foreach(var allocation in table.allocations) {
					instructions.Add(allocation);
				}
				tempID = 1 + table.allocations.Count;
				
				enclosureStack.Push(new Enclosure(NT.FUNCTION, function, table, function.memberCount + cursor));
				cursor += function.definitionCount;
			} },
			{ NT.FUNCTION_CALL, node => {
				var functionCall = (AST_FunctionCall)node;
				// TODO: validate function call
				
				instructions.Add(new IR_Call(){ function = functionCall.function.result, arguments = functionCall.arguments.Select(a=>a.result).ToArray() });
			} },
			{ NT.TUPLE, node => {
				// var tuple = (AST_Tuple)node;
				// enclosureStack.Push(new Enclosure(tuple.nodeType, tuple, enclosure.scope, tuple.memberCount + cursor));
			} },
			{ NT.MEMBER_TUPLE, node => {
				var tuple = (AST_Tuple)node;
				enclosureStack.Push(new Enclosure(tuple.nodeType, tuple, enclosure.scope, tuple.memberCount + cursor));
			} },
			{ NT.MEMBER_NAME, node => { } },
			{ NT.NAME, getTypeFromName },
			{ NT.OBJECT_MEMBER_NAME, node => {
				Debug.Assert(enclosure.type == NT.OBJECT);
				node.result = operatorGetMember(ref enclosure.node, node as AST_Symbol);
			} },
			{ NT.RETURN, node => {
				var returns = (AST_Return)node;
				Value[] values = (returns.values != null) ?
					(returns.values as AST_Tuple)?.values.Select(v=>v.result).ToArray() ?? new Value[] { returns.values.result } : new Value[0];
				
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
			{ NT.MINUS,		 basicOperator },
			{ NT.PLUS,		 basicOperator },
			{ NT.MULTIPLY,	 basicOperator },
			{ NT.DIVIDE,	 basicOperator },
			{ NT.SLICE,		 basicOperator },
			{ NT.GET_MEMBER, node => {
				var op = (AST_Operation)node;
				op.result =  operatorGetMember(ref op.a, op.b as AST_Symbol);
			} },
			{ NT.ASSIGN, node => {
				var op = (AST_Operation)node;
				assign(op.a, op.b);
				op.result = op.b.result;
			} },
			{ NT.REFERENCE, node => {
				var op = (AST_Operation)node;
				if(op.a.result.kind != Value.Kind.VALUE | !(op.a.result.type is DataType_Reference)) {
					throw Jolly.addError(op.location, "Cannot get a reference to this");
				}
				op.result.type = op.a.result.type;
				op.result.kind = Value.Kind.ADDRES;
			} },
			{ NT.DEREFERENCE, node => {
				var op = (AST_Operation)node;
				if(op.a.result.kind == Value.Kind.ADDRES) {
					op.a.result.kind = Value.Kind.VALUE;
				} else {
					load(op.a);
				}
				op.result.kind = op.a.result.kind;
				op.result.type = op.a.result.type;
			} },
			{ NT.CAST, node => {
				var op = (AST_Operation)node;
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
			case AST_ModifyType.TO_NULLABLE: // TODO: Make regular pointer non nullable
			case AST_ModifyType.TO_ARRAY: /*Debug.Assert(false); break;*/
			case AST_ModifyType.TO_SLICE: /*Debug.Assert(false); break;*/
			case AST_ModifyType.TO_POINTER: mod.result.type = new DataType_Reference(mod.target.result.type); break;
		}
		
		DataType.makeUnique(ref mod.result.type);
		mod.result.kind = mod.target.result.kind;
	}
	
	static void skipSymbol(AST_Node node)
		=> cursor += (node as AST_Scope).memberCount;
	
	static void declare(AST_Node node)
	{
		if(node.result.type != null) {
			skipSymbol(node);
			return;
		}
		
		var enclosureNode  = (AST_Scope)enclosure.node;
		var definition     = (AST_Declaration)node;
		DataType allocType = definition.typeFrom.result.type;
		
		switch(enclosure.type)
		{
			case NT.FUNCTION:
			case NT.GLOBAL: {
				Value resultValue = new Value {
					tempID = definition.allocation.result.tempID,
					type   = new DataType_Reference(allocType),
					kind   = Value.Kind.VALUE,
				};
				
				DataType.makeUnique(ref resultValue.type);
				
				definition.allocation.type   = allocType;
				definition.allocation.result = resultValue;
				definition.symbol.type       = resultValue;
				definition.result            = resultValue;
				
				if(context.kind == Context.Kind.FUNCTION_DECLARATION)
				{
					var function     = (AST_Function)enclosureStack.ElementAt(1).node;
					var functionType = (DataType_Function)function.result.type;
					functionType.arguments[function.finishedArguments] = allocType;
					function.finishedArguments += 1;
					cursor = enclosure.end;
				}
			} break;
			case NT.STRUCT: {
				((DataType_Struct)enclosureNode.result.type).finishDefinition(definition.text, allocType);
			} break;
			default: throw Jolly.addError(definition.location, "Cannot define a variable here");
		}
		if(allocType == Lookup.AUTO) {
			contextStack.Push(new)
			enclosureStack.Push(new Enclosure(NT.DEFINITION, definition, enclosure.scope, definition.memberCount + cursor));
		}
	}
	
	static bool implicitCast(AST_Node inValue, AST_Node toValue)
	{
		DataType inType = inValue.result.type,
				 toType = toValue.result.type;
		
		if(inType == toType) {
			return true;
		}
		
		if((inType.flags & toType.flags & DataType.Flags.BASE_TYPE) == 0) {
			
			//TODO: implicitly cast to inherited type (TODO: implement inheretance)
			return false;
		}
		
		if(inType is DataType_Reference | toType is DataType_Reference) {
			return false;
		}
		
		if(inType.size > toType.size) {
			return false;
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

	static void assign(AST_Node a, AST_Node b)
	{
		bool aIsTuple = a.nodeType == NT.TUPLE | a.nodeType == NT.MEMBER_TUPLE;
		bool bIsTuple = b.nodeType == NT.TUPLE | b.nodeType == NT.MEMBER_TUPLE;
		if(!aIsTuple & !bIsTuple)
		{
			load(b);
			
			var target = a.result.type as DataType_Reference;
			if(target == null) {
				throw Jolly.addError(a.location, "Cannot assign to this");
			}
			
			if(b.onStored?.Invoke(a, b, instructions) ?? false) {
				return;
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
			aVals.forEach((aVal, i) => assign(aVal, bVals[i]));
		}
		else if(aIsTuple & !bIsTuple) {
			((AST_Tuple)a).values.forEach(v => assign(v, b));
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
			var definition = ((AST_Symbol)a).symbol.getChildSymbol(b.text);
			if(definition == null) {
				throw Jolly.addError(b.location, "The type does not contain a member \"{0}\"".fill(b.text));
			}
			return definition.type;
		}
		else
		{
			throw Jolly.unexpected(a);
		}
		return result;
	}
	
	static void basicOperator(AST_Node node)
	{
		var op = (AST_Operation)node;
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
			node.result = operatorGetMember(ref ((AST_Tuple)enclosure.node).membersFrom, node as AST_Symbol);
			return;
		}
		AST_Symbol name = (AST_Symbol)node;
		var definition = enclosure.scope.searchSymbol(name.text);
		
		if(definition == null) {
			throw Jolly.addError(name.location, "The name \"{0}\" does not exist in the current context".fill(name.text));
		}
		Debug.Assert(definition.type.type != null);
		Debug.Assert(definition.type.kind != Value.Kind.UNDEFINED);
		
		name.symbol = definition;
		name.result = definition.type;
	}
	
	/*###########
	    Hooks
	###########*/
	
	static bool storeObject(AST_Node location, AST_Node obj, List<IR> instructions)
	{
		var _object = (AST_Object)obj;
		
		if(_object.inferFrom != null)
		{
			var inferFrom = _object.inferFrom.result;
			if(!valueIsStatic(inferFrom)) {
				 throw Jolly.addError(_object.inferFrom.location, "Not a type");
			}
			_object.result.kind = Value.Kind.VALUE;
			_object.result.type = new DataType_Reference(inferFrom.type);
			DataType.makeUnique(ref _object.result.type);
		}
		else
		{
			if(location.result.type == Lookup.AUTO) {
				throw Jolly.addError(_object.location, "Cannot derive type.");
			}
			_object.result = location.result;
		}
		
		_object.resetIndex = cursor + 1;
		cursor = _object.startIndex;
		enclosureStack.Push(new Enclosure(NT.OBJECT, obj, enclosure.scope, _object.startIndex + _object.memberCount));
		
		return true;
	}
}
}