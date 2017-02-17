using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;

namespace Jolly
{
using NT = AST_Node.Type;
using Cast = Func<IR, DataType, IR>;

class IRList : List<IR>
{
	new public IR Add(IR item) { base.Add(item); return item; }
}

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
		
	public static IRList instructions;
	static EnclosureStack enclosureStack;
	static ContextStack contextStack;
	static List<AST_Node> program;
	static Enclosure enclosure;
	static bool isDefineFase = true;
	static Context context;
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
	
	static void incrementCursor(ref int cursor)
	{
		while(context.index <= cursor) {
			contextEnd(contextStack.Pop());
		}
		while(enclosure.end <= cursor) {
			enclosureEnd(enclosureStack.Pop());
		}
		cursor += 1;
	}
	
	static void swap(ref IRList a, ref IRList b)
	{
		var _swp = a;
		a = b;
		b = _swp;
	}
	
	static void implicitCast(ref IR ir, DataType to)
	{
		if(ir.dType != to)
		{
			Cast cast;
			if(!Lookup.implicitCast.get(ir.dType, to, out cast)) {
				throw Jolly.addError(new SourceLocation(), "Cannot implicitly cast {0} to {1}".fill(ir.dType, to));
			}
			ir = cast(ir, to);
		}
	}
	
	static bool isStatic(ValueKind kind) => kind == ValueKind.STATIC_TYPE || kind == ValueKind.STATIC_FUNCTION;
	
	
	public static IRList analyse(List<AST_Node> program, SymbolTable globalScope)
	{
		Analyser.program = program;
		instructions = new IRList();
		contextStack = new ContextStack(16);
		enclosureStack = new EnclosureStack(16);	
		
		contextStack.Push(new Context(int.MaxValue, Context.Kind.STATEMENT)); // Just put something on the stack
		enclosureStack.Push(new Enclosure(NT.GLOBAL, null, globalScope, int.MaxValue));
		
		for(cursor = 0; cursor < program.Count; incrementCursor(ref cursor))
		{
			Action<AST_Node> action;
			AST_Node node = program[cursor];
			if(typeDefinitionAnalysers.TryGetValue(node.nodeType, out action)) {
				action(node);
			}
		}
		
		isDefineFase = false;
		
		for(cursor = 0; cursor < program.Count; incrementCursor(ref cursor)) {
			analyseNode(program[cursor]);
		}
		
		return instructions;
	}
	
	static void analyseNode(AST_Node node)
	{
		Action<AST_Node> action;
		if(!analysers.TryGetValue(node.nodeType, out action)) {
			throw Jolly.unexpected(node);
		}
		// TODO: Find a better way to do this
		AST_Operation op = node as AST_Operation;
		if(op != null) inferOperands(op);
		action(node);
	}
	
	static void enclosureEnd(Enclosure ended)
	{
		switch(ended.type)
		{
			case NT.IF: break;
			case NT.MEMBER_TUPLE: {
				var tuple = (AST_Tuple)ended.node;
				var tupleType = new DataType_Tuple(tuple.values.Count);
				tuple.values.forEach((v, i)=> {
					if((v.result.dKind & ValueKind.ADDRES) == 0) {
						throw Jolly.addError(v.location, "This tuple can only contain members of {0}".fill(tuple.membersFrom.result.dType));
					}
					tupleType.members[i] = v.result.dType;
				});
				ended.node.result.dType = tupleType;
				DataType.makeUnique(ref ended.node.result.dType);
			} break;
			case NT.FUNCTION: if(!isDefineFase) swap(ref instructions, ref ((IR_Function)ended.node.result).block); break;
			case NT.STRUCT: {
				var structNode = (AST_Struct)ended.node;
				var structType = (DataType_Struct)structNode.result.dType;
				
				DataType[] members;
				if(structNode.inherits != null) {
					members = new DataType[structType.members.Length + 1];
					members[0] = structNode.inherits.result.dType;
					structType.members.CopyTo(members, 1);
				} else {
					members = new DataType[structType.members.Length];
					structType.members.CopyTo(members, 0);
				}
				structNode.result = instructions.Add(new IR{ irType = NT.STRUCT, dType = structType, dKind = ValueKind.STATIC_TYPE });
				
				if(structNode.inherits != null) {
					if(structNode.inherits.result.dKind != ValueKind.STATIC_TYPE || !(structNode.inherits.result.dType is DataType_Struct)) {
						throw Jolly.addError(structNode.inherits.location, "Can only inherit from other structs");
					}
					structType.inherits = (DataType_Struct)structNode.inherits.result.dType;
				}
			} break;
			case NT.OBJECT: break;
			default: throw Jolly.addError(ended.node.location, "Internal compiler error: illigal node used as enclosure");
		}
	}
	
	static ValueKind validateTupleKind(List<AST_Node> values)
	{
		ValueKind kind = values.map(a => a.result.dKind).reduce((a, b) => a | b);
		
		SourceLocation removeThis = values.FirstOrDefault()?.location ?? new SourceLocation();
		if((kind & (ValueKind.STATIC_TYPE | ValueKind.STATIC_FUNCTION)) != 0) {
			if((kind & ~(ValueKind.STATIC_TYPE | ValueKind.STATIC_FUNCTION)) != 0) {
				throw Jolly.addError(removeThis, "Tuple mixes values and types");
			}
		} else if((kind & (ValueKind.ADDRES | ValueKind.STATIC_VALUE | ValueKind.VALUE)) == 0) {
			throw Jolly.addError(removeThis, "Unknown tuple type");
		}
		return kind;
	}
	
	static void contextEnd(Context ended)
	{
		switch(ended.kind)
		{
			case Context.Kind.TUPLE: {
				var tuple = (AST_Tuple)ended.target;
				var tupleType = new DataType_Tuple(tuple.values.Count);
				var kind = validateTupleKind(tuple.values);
				tupleType.members = tuple.values.Select(v => v.result.dType).ToArray();
				tuple.result = new IR_Tuple{ irType = NT.TUPLE, dType = tupleType, dKind = kind };
				DataType.makeUnique(ref tuple.result.dType);
			} break;
			case Context.Kind.IF_CONDITION: {
				var ifNode = (AST_If)ended.target;
				implicitCast(ref ifNode.condition.result, Lookup.I1);
				ifNode.result = instructions.Add(new IR_If{ condition = ifNode.condition.result, ifBlock = instructions });
				contextStack.Push(new Context(cursor + ifNode.ifCount, Context.Kind.IF_TRUE){ target = ifNode });
				instructions = new IRList();
			} break;
			case Context.Kind.IF_TRUE: {
				var ifNode = (AST_If)ended.target;
				var ifIR = (IR_If)ifNode.result;
				swap(ref instructions, ref ifIR.ifBlock);
				
				if(ifNode.elseCount > 0) {
					ifIR.elseBlock = instructions;
					instructions = new IRList();
					contextStack.Push(new Context(cursor + ifNode.elseCount, Context.Kind.IF_FALSE) { target = ifNode });
				}
			} break;
			case Context.Kind.IF_FALSE: {
				var ifNode = (AST_If)ended.target;
				var ifIR = (IR_If)ifNode.result;
				swap(ref instructions, ref ifIR.elseBlock);
			} break;
			case Context.Kind.LOGIC_OR: {
				var lor = (AST_Logic)ended.target;
				var lorIR = (IR_Logic)lor.result;
				implicitCast(ref lor.a.result, Lookup.I1);
				swap(ref instructions, ref lorIR.block);
				lorIR.a = lor.a.result;
			} break;
			case Context.Kind.LOGIC_AND: {
				var land = (AST_Logic)ended.target;
				var landIR = (IR_Logic)land.result;
				implicitCast(ref land.a.result, Lookup.I1);
				swap(ref instructions, ref landIR.block);
				landIR.a = land.a.result;
			} break;
			case Context.Kind.DECLARATION: {
				// type inference
				var declaration = (AST_Declaration)ended.target;
				var alloc = (IR_Allocate)declaration.result;
				// declaration.result.packed = true; // TODO: Maybe remove later
				
				if(alloc.dType == Lookup.AUTO || !alloc.initialized) {
					throw Jolly.addError(declaration.location, "Implicitly-typed variables must be initialized.");
				}
			} break;
			case Context.Kind.FUNCTION_DECLARATION: {
				// The declaration ends after the return values and arguments are parsed.
				var function = (AST_Function)enclosure.node;
				var functionType = (DataType_Function)function.result.dType;
				functionType.returns = function.returns.result.dType;
				DataType.makeUnique(ref function.result.dType);
				cursor = enclosure.end; // Skip to end of function enclosure
			} break;
		}
	}
	
	static readonly Dictionary<NT, Action<AST_Node>>
		// Used for the first pass to define all the struct members
		typeDefinitionAnalysers = new Dictionary<NT, Action<AST_Node>>() {
			{ NT.DEFINITION, declare },
			{ NT.FUNCTION, node => {
				var function = (AST_Function)node;
				var table = (SymbolTable)function.symbol;
				enclosureStack.Push(new Enclosure(NT.FUNCTION, function, table, function.memberCount + cursor));
				contextStack.Push(new Context(function.definitionCount + cursor, Context.Kind.FUNCTION_DECLARATION));
			} },
			{ NT.STRUCT, node => {
				var structNode = (AST_Scope)node;
				var table = (SymbolTable)structNode.symbol;
				enclosureStack.Push(new Enclosure(NT.STRUCT, structNode, table, structNode.memberCount + cursor));
			} },
			{ NT.GET_MEMBER, node => {
				var op = (AST_Operation)node;
				op.result =  operatorGetMember(ref op.a, op.b as AST_Symbol);
			} },
			{ NT.NAME, getTypeFromName },
			{ NT.MEMBER_NAME, node => { } },
			{ NT.TUPLE, tupleContext },
			{ NT.MODIFY_TYPE, modifyType },
			{ NT.ENUM,   skipSymbol },
		},
		analysers = new Dictionary<NT, Action<AST_Node>>() {
			{ NT.DEFINITION, declare },
			{ NT.MODIFY_TYPE, modifyType },
			{ NT.STRUCT, skipSymbol },
			{ NT.ENUM,   skipSymbol },
			{ NT.OBJECT, node => {
				var _object = (AST_Object)node;
				_object.infer = inferObject;
				_object.onUsed = storeObject;
				_object.startIndex = cursor + 1;
				cursor += _object.memberCount;
			} },
			{ NT.INITIALIZER, assign },
			{ NT.FUNCTION, node => {
				var function = (AST_Function)node;
				var functionIR = (IR_Function)function.result;
				instructions.Add(functionIR);
				functionIR.block = instructions;
				instructions = new IRList();
				
				enclosureStack.Push(new Enclosure(NT.FUNCTION, function, (SymbolTable)function.symbol, function.memberCount + cursor));
				cursor += function.definitionCount;
			} },
			{ NT.FUNCTION_CALL, node => {
				var functionCall = (AST_FunctionCall)node;
				var functionType = functionCall.function.result.dType as DataType_Function;
				if(functionType == null) {
					throw Jolly.addError(node.location, "Can not call this");
				}
				var arguments = functionType.arguments;
				var values = new IR[functionCall.arguments.Length];
				
				for(int i = 0; i < values.Length; i += 1)
				{
					var arg = functionCall.arguments[i];
					var argT = arguments[i];
					
					load(arg);
					implicitCast(ref arg.result, argT);
					
					values[i] = arg.result;
				}
				node.result = instructions.Add(new IR_Call(){ target = functionCall.function.result, arguments = values, dType = functionType.returns });
			} },
			{ NT.TUPLE, tupleContext },
			{ NT.MEMBER_TUPLE, node => {
				var tuple = (AST_Tuple)node;
				enclosureStack.Push(new Enclosure(tuple.nodeType, tuple, enclosure.scope, tuple.memberCount + cursor));
			} },
			{ NT.IF, node => {
				var ifNode = (AST_If)node;
				contextStack.Push(new Context(cursor + ifNode.conditionCount, Context.Kind.IF_CONDITION) { target = ifNode });
				enclosureStack.Push(new Enclosure(NT.IF, ifNode, ifNode.ifScope, cursor + ifNode.conditionCount + ifNode.ifCount + ifNode.elseCount));
			} },
			{ NT.MEMBER_NAME, node => { } },
			{ NT.NAME, getTypeFromName },
			{ NT.OBJECT_MEMBER_NAME, node => {
				Debug.Assert(enclosure.type == NT.OBJECT);
				node.result = operatorGetMember(ref enclosure.node, node as AST_Symbol);
			} },
			{ NT.RETURN, node => {
				// var returnNode = (AST_Return)node;
				// AST_Function function = null;
				// foreach(var closure in enclosureStack) {
				// 	function = closure.node as AST_Function;
				// 	if(function != null) break;
				// }
				// Debug.Assert(function != null);
				
				// AST_Node[] valueNodes = (returnNode.values as AST_Tuple)?.values.ToArray() ?? new AST_Node[] { returnNode.values };
				// DataType[] returns = ((DataType_Function)function.result.dType).returns;
				// Value[] values = new Value[valueNodes.Length];
				
				// for(int i = 0; i < valueNodes.Length; i += 1)
				// {
				// 	load(valueNodes[i]);
				// 	Cast cast = null;
				// 	DataType aR = returns[i];
				// 	Value bR = valueNodes[i].result;
				// 	if(aR != bR.type && !Lookup.implicitCasts.getCast(bR.type, aR, out cast)) {
				// 		throw Jolly.addError(valueNodes[i].location, "Invalid return value");
				// 	}
				// 	values[i] = cast?.Invoke(bR, aR) ?? bR;
				// }
				// instructions.Add(new IR_Return{ values = values });
			} },
			{ NT.SUBTRACT, node => basicOperator<IR_Subtract>(node, (a, b) => (a is long ? (long)a - (long)b : (double)a - (double)b)) },
			{ NT.ADD,      node => basicOperator<IR_Add>     (node, (a, b) => (a is long ? (long)a + (long)b : (double)a + (double)b)) },
			{ NT.MULTIPLY, node => basicOperator<IR_Multiply>(node, (a, b) => (a is long ? (long)a * (long)b : (double)a * (double)b)) },
			{ NT.DIVIDE,   node => basicOperator<IR_Divide>  (node, (a, b) => (a is long ? (long)a / (long)b : (double)a / (double)b)) }, // TODO: Check null
			// { NT.BIT_OR,   basicOperator<IR_>    },
			// { NT.BIT_AND,  basicOperator<IR_>    },
			{ NT.BIT_NOT,  node => {
				// Instr xor;
				// var op = (AST_Operation)node;
				// if(!Lookup.xors.TryGetValue(op.a.result.dType, out xor)) {
				// 	throw Jolly.addError(op.location, "Cannot use operator '~' on"+node.result.dType);
				// }
				// op.result = xor(op.a.result, new Value{ type = op.a.result.dType, kind = Value.Kind.STATIC_VALUE, data = -1 });
			} },
			{ NT.BIT_XOR,     node => basicOperator<IR_Divide>(node, (a, b) => (long)a ^ (long)b) },
			// { NT.MODULO,      basicOperator<>    },
			// { NT.SHIFT_LEFT,  basicOperator<>  },
			// { NT.SHIFT_RIGHT, basicOperator<> },
			{ NT.LOGIC_AND,    node => {
				var land = (AST_Logic)node;
				land.result = instructions.Add(new IR_Logic{ irType = NT.LOGIC_AND, dType = Lookup.I1, dKind = ValueKind.VALUE, block = instructions });
				instructions = new IRList();
				implicitCast(ref land.condition.result, Lookup.I1);
				contextStack.Push(new Context(cursor + land.memberCount, Context.Kind.LOGIC_OR){ target = land });
			} },
			{ NT.LOGIC_OR,   node => {
				var lor = (AST_Logic)node;
				lor.result   = instructions.Add(new IR_Logic{ irType = NT.LOGIC_OR, dType = Lookup.I1, dKind = ValueKind.VALUE, block = instructions });
				instructions = new IRList();				
				implicitCast(ref lor.condition.result, Lookup.I1);
				contextStack.Push(new Context(cursor + lor.memberCount, Context.Kind.LOGIC_AND){ target = lor });
			} },
			{ NT.REINTERPRET, node => {
				var op = (AST_Operation)node;
				op.result = instructions.Add(IR.cast<IR_Reinterpret>(op.a.result, op.b.result.dType, null));
			} },
			{ NT.LOGIC_NOT,   node => {
				// Cast cast;
				var op = (AST_Operation)node;
				implicitCast(ref op.a.result, Lookup.I1);
				// var result = IR.operation<IR_Xor>(op.a.result, op.b.result, (a,b) => (bool)a ^ (bool)b);
				
				
				// op.result = xor(cast(op.a.result, Lookup.I1), new Value{ type = Lookup.I1, kind = Value.Kind.STATIC_VALUE, data = true });
			} },
			// { NT.SLICE,		 node => basicOperator(node, Lookup.) },
			{ NT.GET_MEMBER, node => {
				var op = (AST_Operation)node;
				op.result =  operatorGetMember(ref op.a, op.b as AST_Symbol);
			} },
			{ NT.ASSIGN, assign },
			{ NT.REFERENCE, node => {
				var op = (AST_Operation)node;
				if(op.a.result.dKind != ValueKind.ADDRES) {
					throw Jolly.addError(op.location, "Cannot get a reference to this");
				}
				DataType reference = new DataType_Reference(op.a.result.dType);
				DataType.makeUnique(ref reference);
				op.result = new IR_Reference{ target = op.a.result, dType = reference, dKind = ValueKind.VALUE };
			} },
			{ NT.DEREFERENCE, dereference },
			{ NT.CAST, node => {
				var op = (AST_Operation)node;
				load(op.b);
				
				if(op.a.result.dKind != ValueKind.STATIC_TYPE) {
					throw Jolly.addError(op.a.location, "Cannot cast to this");
				}
				if(op.a.result.dType == op.b.result.dType) return;
				
				Cast cast;
				if(!Lookup.casts.get(op.b.result.dType, op.a.result.dType, out cast)) {
					throw Jolly.addError(op.location, "Cannot cast {1} to {0}".fill(op.a.result.dType, op.b.result.dType));
				}
				op.result = cast(op.b.result, op.a.result.dType);
			} },
		};
	
	static void modifyType(AST_Node node)
	{
		AST_ModifyType mod = (AST_ModifyType)node;
		if(mod.target.result.dKind != ValueKind.STATIC_TYPE) {
			throw Jolly.addError(mod.target.location, "Not a type");
		}
		if((mod.target.result.dType.flags & DataType.Flags.INSTANTIABLE) == 0) {
			throw Jolly.addError(mod.target.location, "The type {0} is not instantiable.".fill(mod.target.result.dType));
		}
		
		// TODO: Fix
		switch(mod.toType) {
			case AST_ModifyType.TO_ARRAY:
				mod.result = new IR{ irType = NT.BASETYPE, dType = new DataType_Array(mod.target.result.dType), dKind = ValueKind.STATIC_TYPE };
				break;
			case AST_ModifyType.TO_SLICE: /*Debug.Assert(false); break;*/
			case AST_ModifyType.TO_POINTER:
			case AST_ModifyType.TO_NULLABLE: // TODO: Make regular pointer non nullable
				mod.result = new IR{ irType = NT.BASETYPE, dType = new DataType_Reference(mod.target.result.dType), dKind = ValueKind.STATIC_TYPE };
				break;
		}
		
		DataType.makeUnique(ref mod.result.dType);
		mod.result.dKind = mod.target.result.dKind;
	}
	
	static void skipSymbol(AST_Node node)
		=> cursor += (node as AST_Scope).memberCount;
	
	static void tupleContext(AST_Node node)
		=> contextStack.Push(new Context(cursor + ((AST_Tuple)node).memberCount, Context.Kind.TUPLE) { target = node });
	
	static void declare(AST_Node node)
	{
		if(node.result?.dType != null) {
			skipSymbol(node);
			return;
		}
		
		var enclosureNode  = (AST_Scope)enclosure.node;
		var definition     = (AST_Declaration)node;
		DataType allocType = definition.typeFrom.result.dType;
				
		switch(enclosure.type)
		{
			case NT.FUNCTION:
			case NT.GLOBAL: {
				var alloc = new IR_Allocate{ dType = allocType };
				definition.symbol.declaration = alloc;
				definition.result = instructions.Add(alloc);
				
				if(context.kind == Context.Kind.FUNCTION_DECLARATION)
				{
					var function     = (AST_Function)enclosure.node;
					var functionType = (DataType_Function)function.result.dType;
					functionType.arguments[function.finishedArguments] = allocType;
					function.finishedArguments += 1;
					cursor = enclosure.end;
				}
			} break;
			case NT.STRUCT: {
				((DataType_Struct)enclosureNode.result.dType).finishDefinition(definition.text, allocType);
			} break;
			default: throw Jolly.addError(definition.location, "Cannot define a variable here");
		}
		if(allocType == Lookup.AUTO) {
			contextStack.Push(new Context(definition.memberCount + cursor, Context.Kind.DECLARATION));
		}
	}
	
	static void assign(AST_Node node)
	{
		var op = (AST_Operation)node;
		
		if((op.a.result.dKind & ~ValueKind.ADDRES) != 0) {
			throw Jolly.addError(op.location, "Cannot assign to this");
		}
		load(op.b);
		implicitCast(ref op.b.result, op.a.result.dType);
		
		Action<AST_Node, IR> assignTup = null; assignTup = (c, d) => {
			var aTupType = (DataType_Tuple)c.result.dType;
			int i = 0;
			foreach(var val in ((AST_Tuple)c).values)
			{
				var member = instructions.Add(IR.getMember(d, aTupType.members[i], i++));
				if(val.nodeType == NT.TUPLE) {
					assignTup(val, member);
					continue;
				}
				instructions.Add(IR.operation<IR_Assign>(c.result, d, null));
			}
		};
		
		if(op.a.nodeType == NT.TUPLE) {
			assignTup(op.a, op.b.result);
			return;
		}
		op.result = instructions.Add(IR.operation<IR_Assign>(op.a.result, op.b.result, null));
	}
		
	static IR operatorGetMember(ref AST_Node a, AST_Symbol b)
	{
		if(b == null) {
			throw Jolly.addError(b.location, "The right-hand side of the period operator must be a name");
		}
		
		if(a.result.dKind == ValueKind.ADDRES)
		{
			var iterator = a.result;
			var definition = iterator.dType.getMember(iterator, b.text, instructions);
			
			// while(definition == null) {
			// 	iterator = dereference(a);
			// 	definition = iterator.dType.getMember(iterator, b.text, instructions);
			// }
			
			if(definition == null) {
				throw Jolly.addError(b.location, "Type does not contain a member {0}".fill(b.text));
			}
			return definition;
		}
		else if(a.result.dKind == ValueKind.STATIC_TYPE)
		{
			// Get static member
			var definition = ((AST_Symbol)a).symbol.getChildSymbol(b.text);
			if(definition == null) {
				throw Jolly.addError(b.location, "The type does not contain a member \"{0}\"".fill(b.text));
			}
			return definition.declaration;
		}
		else
		{
			throw Jolly.unexpected(a);
		}
	}
	
	static void basicOperator<T>(AST_Node node, Func<object, object, object> staticExec) where T : IR_Operation, new()
	{
		var op = (AST_Operation)node;
		
		load(op.a); load(op.b);
		 
		IR aIR = op.a.result, bIR = op.b.result;
		if(aIR.dType != bIR.dType)
		{
			// TODO: This always tries to cast's the left type to the right type then right to left,
			// maybe this should be decided by the operator's left to right'ness.
			Cast cast;
			if(Lookup.implicitCast.get(aIR.dType, bIR.dType, out cast)) {
				aIR = cast(aIR, bIR.dType);
			} else if(Lookup.implicitCast.get(aIR.dType, bIR.dType, out cast)) {
				bIR = cast(bIR, aIR.dType);
			} else {
				throw Jolly.addError(op.location, "Cannot use operator on {0} and {1}".fill(aIR.dType, bIR.dType));
			}
		}
		
		if((aIR.dKind & bIR.dKind & ValueKind.STATIC_VALUE) != 0) {
			op.result = new IR_Literal{ dType = aIR.dType, data = staticExec(((IR_Literal)aIR).data, ((IR_Literal)bIR).data) };
			return;
		}
		op.result = instructions.Add(new T{ a = aIR, b = bIR, dType = aIR.dType });
	}
	
	static void dereference(AST_Node node)
	{
		var op = (AST_Operation)node;
		var reference = op.a.result.dType as DataType_Reference;
		if(isStatic(op.a.result.dKind) || reference == null) {
			throw Jolly.addError(op.a.location, "Cannot dereference this");
		}
		op.result = instructions.Add(new IR_Dereference{ target = op.a.result, dType = reference.referenced, dKind = ValueKind.ADDRES });
	}
	
	static void load(AST_Node node)
	{
		if(node.nodeType == NT.TUPLE)
		{
			var tupType = (DataType_Tuple)node.result.dType;
			node.result = instructions.Add(new IR_Allocate{ dType = node.result.dType, initialized = true });
			
			int i = 0;
			foreach(var val in ((AST_Tuple)node).values) {
				var member = instructions.Add(IR.getMember(node.result, tupType.members[i], i++));
				load(val);
				implicitCast(ref val.result, member.dType);
				instructions.Add(IR.operation<IR_Assign>(member, val.result, null));
			}
		}
		
		if((node.result.dKind & ValueKind.ADDRES) == 0) {
			return;
		}
		
		node.result = instructions.Add(new IR_Read{ target = node.result, dType = node.result.dType, dKind = ValueKind.VALUE });
	}
	
	static void inferOperands(AST_Operation op)
	{
		if(op.leftToRight) {
			op.a.infer?.Invoke(op.a, op.b, instructions);
			op.b?.infer?.Invoke(op.b, op.a, instructions);
		} else {
			op.b?.infer?.Invoke(op.b, op.a, instructions);
			op.a.infer?.Invoke(op.a, op.b, instructions);
		}
	}
	
	static void getTypeFromName(AST_Node node)
	{
		if(node.result?.dType != null) {
			return;
		}
		
		if(enclosure.type == NT.MEMBER_TUPLE) {
			node.result = operatorGetMember(ref ((AST_Tuple)enclosure.node).membersFrom, node as AST_Symbol);
			return;
		}
		AST_Symbol name = (AST_Symbol)node;
		var definition = enclosure.scope.searchSymbol(name.text);
		// ((IR_Allocate)definition.declaration).references += 1;
		
		if(definition == null) {
			throw Jolly.addError(name.location, "The name \"{0}\" does not exist in the current context".fill(name.text));
		}
		Debug.Assert(definition.declaration.dType != null);
		Debug.Assert(definition.declaration.dKind != ValueKind.UNDEFINED);
		
		name.result = definition.declaration;
	}
	
	/*###########
	    Hooks
	###########*/
	
	static bool inferObject(AST_Node i, AST_Node other, IRList instructions)
	{
		return false;
	}
	
	static bool storeObject(AST_Node location, AST_Node obj, IRList instructions)
	{
		// TODO: Zero out struct.
		
		var _object = (AST_Object)obj;
		_object.result = location.result;
		
		if(_object.inferFrom != null)
		{
			var inferFrom = _object.inferFrom.result;
			if(!isStatic(inferFrom.dKind)) {
				 throw Jolly.addError(_object.inferFrom.location, "Not a type");
			}
			// _object.result.dKind = Value.Kind.VALUE;
			// _object.result.dType = inferFrom.dType;
		}
		else if(location.result.dType == Lookup.AUTO) {
			throw Jolly.addError(_object.location, "Cannot derive type.");
		}
		
		int end = _object.startIndex + _object.memberCount;
		enclosureStack.Push(new Enclosure(NT.OBJECT, obj, enclosure.scope, end));
		for(int i = _object.startIndex; i < end; incrementCursor(ref i)) {
			analyseNode(program[i]);
		}
		return true;
	}
}
}