using System.Collections.Generic;
using System.Diagnostics;
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
		new public void Push(Enclosure e) { base.Push(enclosure = e); }
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
		new public void Push(Context c) { base.Push(context = c); }
		
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
	
	static bool isStatic(ValueKind kind) => (kind & (ValueKind.STATIC_TYPE | ValueKind.STATIC_FUNCTION)) != 0;
	
	public static IRList analyse(List<AST_Node> program, SymbolTable globalScope)
	{
		Analyser.program = program;
		instructions = new IRList();
		contextStack = new ContextStack(16);
		enclosureStack = new EnclosureStack(16);	
		
		contextStack.Push(new Context(int.MaxValue, Context.Kind.STATEMENT)); // Just put something on the stack
		enclosureStack.Push(new Enclosure(NT.GLOBAL, null, globalScope, int.MaxValue));
		
		program.forEach((n, i) => Console.WriteLine("{0}: {1}".fill(i, n)));
		Console.WriteLine();
		
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
				AST_Tuple      tuple = (AST_Tuple)ended.node;
				DataType_Tuple tupleType;
				ValueKind      tupleKind;
				getTuple_Type_Kind(tuple, out tupleType, out tupleKind);
				tuple.result = new IR{ irType = NT.TUPLE, dType = tupleType, dKind = tupleKind };
				
				// TODO: only allow members
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
	
	static void getTuple_Type_Kind(AST_Tuple tuple, out DataType_Tuple tupleType, out ValueKind tupleKind)
	{
		ValueKind      _tupleKind = 0;
		DataType_Tuple _tupleType = new DataType_Tuple(tuple.values.Count);
		
		tuple.values.forEach((val, i) => {
			_tupleKind |= val.result.dKind;
			_tupleType.members[i] = val.result.dType;
		});
		
		if((_tupleKind & (ValueKind.STATIC_TYPE | ValueKind.STATIC_FUNCTION)) != 0) {
			if((_tupleKind & ~(ValueKind.STATIC_TYPE | ValueKind.STATIC_FUNCTION)) != 0) {
				throw Jolly.addError(tuple.location, "Tuple mixes values and types");
			}
		} else if((_tupleKind & (ValueKind.ADDRES | ValueKind.STATIC_VALUE | ValueKind.VALUE)) == 0) {
			throw Jolly.addError(tuple.location, "Invalid tuple type");
		}
		tupleKind = _tupleKind;
		tupleType = (DataType_Tuple)DataType.makeUnique(_tupleType);
	}
	
	static IR packTuple(AST_Tuple tuple, DataType_Tuple tupleType)
	{
		IR_Allocate alloc = new IR_Allocate{ dType = tupleType };
		tuple.values.forEach((val, i) => {
			IR member = instructions.Add(IR.getMember(alloc, tupleType.members[i], i));
			instructions.Add(IR.operation<IR_Assign>(member, val.result, null));
		});
		return alloc;
	}
	
	static void contextEnd(Context ended)
	{
		switch(ended.kind)
		{
			case Context.Kind.TUPLE: {
				AST_Tuple      tuple = (AST_Tuple)ended.target;
				DataType_Tuple tupleType;
				ValueKind      tupleKind;
				getTuple_Type_Kind(tuple, out tupleType, out tupleKind);
				
				// If the tuple doesn't contain names pack it
				if((tupleKind & (ValueKind.ADDRES | ValueKind.STATIC_TYPE)) == 0) {
					tuple.result = instructions.Add(new IR_Read{ target = packTuple(tuple, tupleType), dType = tupleType });
				} else {
					tuple.result = new IR{ irType = NT.TUPLE, dType = tupleType, dKind = tupleKind };
				}
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
				
				if(alloc.dType == Lookup.AUTO || !alloc.initialized) {
					throw Jolly.addError(declaration.location, "Auto variables must be initialized.");
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
			{ NT.TEMPLATE_NAME, node => {
				var template = (AST_Template)node;
				template.result = new IR { dType = Lookup.TEMPLATE, dKind = ValueKind.STATIC_TYPE };
				if(template.item == null)
				{
					template.item = enclosure.scope.getTemplate(template.name);
				}
				else
				{
					var type = template.item?.constantValue?.result;
					if(type != null)
					{
						// TODO: check type instantiable
						if(type.dKind != ValueKind.STATIC_TYPE) {
							throw Jolly.addError(template.location, "Expected static type");
						}
						template.result.dKind = ValueKind.STATIC_VALUE;
						template.result.dType = type.dType;
					}
				}
			} },
			{ NT.DECLARATION, declare },
			{ NT.FUNCTION, node => {
				var function = (AST_Function)node;
				var table = (SymbolTable)function.symbol;
				if(table.template.Count > 0) { // TODO: Maybe a better wat to check if generic
					cursor += function.memberCount;
					return;
				}
				enclosureStack.Push(new Enclosure(NT.FUNCTION, function, table, function.memberCount + cursor));
				contextStack.Push(new Context(function.definitionCount + cursor, Context.Kind.FUNCTION_DECLARATION));
			} },
			{ NT.STRUCT, node => {
				var structNode = (AST_Scope)node;
				var table = (SymbolTable)structNode.symbol;
				if(table.template.Count > 0) { // TODO: Maybe a better wat to check if generic
					cursor += structNode.memberCount;
					return;
				}
				enclosureStack.Push(new Enclosure(NT.STRUCT, structNode, table, structNode.memberCount + cursor));
			} },
			{ NT.GET_MEMBER, node => {
				var op = (AST_Operation)node;
				op.result =  operatorGetMember(ref op.a, op.b);
			} },
			{ NT.NAME, getTypeFromName },
			{ NT.MEMBER_NAME, node => { } },
			{ NT.TUPLE, tupleContext },
			{ NT.MODIFY_TYPE, modifyType },
			{ NT.ENUM,   skipSymbol },
		},
		analysers = new Dictionary<NT, Action<AST_Node>>() {
			{ NT.DECLARATION, declare },
			{ NT.MODIFY_TYPE, modifyType },
			{ NT.STRUCT, skipSymbol },
			{ NT.ENUM,   skipSymbol },
			{ NT.OBJECT, node => {
				var _object = (AST_Object)node;
				_object.result = instructions.Add(new IR_Allocate{ dType = Lookup.AUTO });
				_object.infer = inferObject;
				_object.startIndex = cursor + 1;
				cursor += _object.memberCount;
			} },
			{ NT.INITIALIZER, assign },
			{ NT.FUNCTION, node => {
				var function = (AST_Function)node;
				var functionIR = (IR_Function)function.result;
				var table = (SymbolTable)function.symbol;
				if(table.template.Count > 0) { // TODO: Maybe a better wat to check if generic
					cursor += function.memberCount;
					return;
				}
				instructions.Add(functionIR);
				functionIR.block = instructions;
				instructions = new IRList();
				
				enclosureStack.Push(new Enclosure(NT.FUNCTION, function, table, function.memberCount + cursor));
				cursor += function.definitionCount;
			} },
			{ NT.FUNCTION_CALL, node => {
				var call = (AST_FunctionCall)node;
				var function = call.function;
				
				var args = call.arguments;
				AST_Symbol name = function as AST_Symbol;
				if(name?.symbol.isGeneric ?? false) {
					var template = ((SymbolTable)name.symbol).template;
					var tArgs = name.templateArguments;
					var types = new DataType[template.Count];
					
					// tArgs.forEach(temp => {
					// 	int i = temp.item.defineIndex;
					// 	if(types[i] == null) types[i] = temp;
					// });
					
					
					
					Debug.Fail("Not implemented");
				}
				
				var functionType = function.result.dType as DataType_Function;
				if(functionType == null) {
					throw Jolly.addError(node.location, "Cannot call this");
				}
				var FTArgs = functionType.arguments;
				var values = new IR[args.Length];
				
				for(int i = 0; i < values.Length; i += 1)
				{
					var arg = args[i];
					var argT = FTArgs[i];
					
					load(arg);
					implicitCast(ref arg.result, argT);
					
					values[i] = arg.result;
				}
				node.result = instructions.Add(new IR_Call(){ target = function.result, arguments = values, dType = functionType.returns });
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
				node.result = operatorGetMember(ref enclosure.node, node);
			} },
			{ NT.RETURN, node => {
				var returnNode = (AST_Return)node;
				AST_Function function = null;
				foreach(var closure in enclosureStack) {
					function = closure.node as AST_Function;
					if(function != null) break;
				}
				Debug.Assert(function != null);
				
				var returns = ((DataType_Function)function.result.dType).returns;
				implicitCast(ref returnNode.value.result, returns);
				node.result = instructions.Add(new IR_Return{ value = returnNode.value.result, dType = returns });
			} },
			{ NT.SUBTRACT, node => basicOperator<IR_Subtract>(node, null) },
			{ NT.ADD,      node => basicOperator<IR_Add>     (node, null) },
			{ NT.MULTIPLY, node => basicOperator<IR_Multiply>(node, null) },
			{ NT.DIVIDE,   node => basicOperator<IR_Divide>  (node, null) },
			{ NT.BIT_OR,   node => basicOperator<IR_BitOr>   (node, null) },
			{ NT.BIT_AND,  node => basicOperator<IR_BitAnd>  (node, null) },
			{ NT.BIT_NOT,  node => {
				// Instr xor;
				// var op = (AST_Operation)node;
				// if(!Lookup.xors.TryGetValue(op.a.result.dType, out xor)) {
				// 	throw Jolly.addError(op.location, "Cannot use operator '~' on"+node.result.dType);
				// }
				// op.result = xor(op.a.result, new Value{ type = op.a.result.dType, kind = Value.Kind.STATIC_VALUE, data = -1 });
			} },
			{ NT.BIT_XOR,     node => basicOperator<IR_Divide>(node, null) },
			{ NT.MODULO,      node => basicOperator<IR_Modulo>(node, null) },
			{ NT.SHIFT_LEFT,  node => basicOperator<IR_LShift>(node, null) },
			{ NT.SHIFT_RIGHT, node => basicOperator<IR_RShift>(node, null) },
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
				op.result =  operatorGetMember(ref op.a, op.b);
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
			case AST_ModifyType.TO_SLICE:
			case AST_ModifyType.TO_ARRAY:
				mod.result = new IR{ irType = NT.BASETYPE, dType = new DataType_Array_Data(mod.target.result.dType), dKind = ValueKind.STATIC_TYPE };
				break;
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
		var declaration    = (AST_Declaration)node;
		DataType allocType = declaration.typeFrom.result.dType;
		
		// Has to be instantiable 
		if((allocType.flags & DataType.Flags.INSTANTIABLE) == 0) {
			throw Jolly.addError(node.location, "The type {0} is not instantiable.".fill(allocType));
		}
		
		switch(enclosure.type)
		{
			case NT.FUNCTION:
			case NT.GLOBAL: {
				var alloc = new IR_Allocate{ dType = allocType };
				declaration.symbol.declaration = alloc;
				declaration.result = instructions.Add(alloc);
				
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
				((DataType_Struct)enclosureNode.result.dType).finishDefinition(declaration.text, allocType);
			} break;
			default: throw Jolly.addError(declaration.location, "Cannot define a variable here");
		}
		if(allocType == Lookup.AUTO) {
			declaration.infer = inferAutoVariable;
			contextStack.Push(new Context(declaration.memberCount + cursor, Context.Kind.DECLARATION){ target = declaration });
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
		
		//TODO: Assign to tuple containing names: someStruct.(a, b) = (0, 1);
		
		if(op.a.result.irType == NT.ALLOCATE) {
			((IR_Allocate)op.a.result).initialized = true;
		}
		op.result = instructions.Add(IR.operation<IR_Assign>(op.a.result, op.b.result, null));
	}
		
	static IR operatorGetMember(ref AST_Node a, AST_Node b)
	{
		bool   isName = false;
		string name   = null;
		int    index  = 0;
		
		switch(b.nodeType) {
			case NT.NAME: {
				isName = true;
				name = ((AST_Symbol)b).text;
			} break;
			case NT.LITERAL: {
				if(b.result.dType != Lookup.I32) goto default;
				index = (int)(long)((IR_Literal)b.result).data;
			} break;
			default: throw Jolly.addError(a.location, "The right-hand operant of member access can only be a symbol or index");
		}
		
		if(a.result.dKind == ValueKind.ADDRES)
		{
			var iterator = a.result;
			var definition = isName ?
				iterator.dType.getMember(iterator, name,  instructions) :
				iterator.dType.getMember(iterator, index, instructions);
			
			while(definition == null) {
				dereference(a);
				iterator = a.result;
				definition = isName ?
					iterator.dType.getMember(iterator, name,  instructions) :
					iterator.dType.getMember(iterator, index, instructions);
			}
			
			if(definition == null) {
				throw Jolly.addError(b.location, "Type does not contain a member {0}".fill(name));
			}
			return definition;
		}
		else if(a.result.dKind == ValueKind.STATIC_TYPE)
		{
			if(!isName) {
				throw new ParseException();
			}
			
			// Get static member
			var definition = ((AST_Symbol)a).symbol.getChildSymbol(name);
			if(definition == null) {
				throw Jolly.addError(b.location, "The type does not contain a member \"{0}\"".fill(name));
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
		
		if((aIR.dKind & bIR.dKind & ValueKind.STATIC_VALUE) != 0 && staticExec != null) {
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
		if((node.result.dKind & ValueKind.ADDRES) == 0) {
			return;
		}
		
		if(node.nodeType == NT.TUPLE) {
			node.result = packTuple((AST_Tuple)node, ((DataType_Tuple)node.result.dType));
		}
		
		AST_Symbol name = node as AST_Symbol;
		if(name.symbol.isGeneric) {
			Debug.Fail("Not implemented");
		}
		
		node.result = instructions.Add(new IR_Read{ target = node.result, dType = node.result.dType });
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
		if(node.result?.dType != null &&
		   (node.result.dType.flags & DataType.Flags.UNFINISHED) == 0) {
			return;
		}
		
		if(enclosure.type == NT.MEMBER_TUPLE) {
			node.result = operatorGetMember(ref ((AST_Tuple)enclosure.node).membersFrom, node);
			return;
		}
		
		AST_Symbol name = (AST_Symbol)node;
		name.symbol = enclosure.scope.searchSymbol(name.text);
		
		if(name.symbol == null) {
			throw Jolly.addError(name.location, "The name \"{0}\" does not exist in the current context".fill(name.text));
		}
		
		name.result = name.symbol.declaration;
	}
	
	/*###########
	    Hooks
	###########*/
	
	static bool inferObject(AST_Node i, AST_Node other, IRList instructions)
	{
		var _object = (AST_Object)i;
		var inferFrom = _object.inferFrom?.result ?? other.result;
		var errLoc = _object.inferFrom?.location ?? other.location;
		
		if((inferFrom.dType.flags & DataType.Flags.INSTANTIABLE) == 0) {
			throw Jolly.addError(errLoc, "Cannot instantiate auto");
		}
		// TODO: Add further type checks
		
		int end = _object.startIndex + _object.memberCount;
		_object.result.dType = inferFrom.dType;
		enclosureStack.Push(new Enclosure(NT.OBJECT, _object, enclosure.scope, end));
		for(int j = _object.startIndex; j < end; incrementCursor(ref j)) {
			analyseNode(program[j]);
		}
		return true;
	}
	
	static bool inferAutoVariable(AST_Node i, AST_Node other, IRList instructions)
	{
		DataType type = other.result.dType;
		var declaration = (AST_Declaration)i;
		
		// Has to be instantiable and not unfinished
		if((type.flags & DataType.Flags.INSTANTIABLE & DataType.Flags.UNFINISHED) != DataType.Flags.INSTANTIABLE) {
			throw Jolly.addError(declaration.location, "The inferred type {0} is not instantiable.".fill(type));
		}
		declaration.symbol.declaration.dType = type;
		return true;
	}
}
}
