using System.Collections.Generic;
using System.Diagnostics;
using System;

namespace Jolly
{
using NT = AST_Node.Type;

using Cast = Func<Value,DataType,Value>;
using Instr = Func<Value,Value,Value>;

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
	
	public static List<IR> instructions;
	static EnclosureStack enclosureStack;
	static ContextStack contextStack;
	static List<AST_Node> program;
	static Enclosure enclosure;
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
	
	static int tempID = 0;
	public static Value newResult(Value _value)
		=> new Value{ type = _value.type, kind = _value.kind, tempID = (tempID += 1) };
		
	public static bool valueIsStatic(Value val)
		=> val.kind == Value.Kind.STATIC_TYPE | val.kind == Value.Kind.STATIC_FUNCTION;
	
	public static List<IR> analyse(List<AST_Node> program, SymbolTable globalScope)
	{
		Analyser.program = program;
		instructions = new List<IR>();
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
		action(node);
	}
	
	static void enclosureEnd(Enclosure ended)
	{
		switch(ended.type)
		{
			case NT.IF: break;
			case NT.MEMBER_TUPLE: break;
			case NT.FUNCTION: break;
			case NT.STRUCT: {
				var structNode = (AST_Struct)ended.node;
				var structType = (DataType_Struct)structNode.result.type;
				
				DataType[] members;
				if(structNode.inherits != null) {
					members = new DataType[structType.members.Length + 1];
					members[0] = structNode.inherits.result.type;
					structType.members.CopyTo(members, 1);
				} else {
					members = new DataType[structType.members.Length];
					structType.members.CopyTo(members, 0);
				}
				instructions.Add(new IR_Struct{ _struct = structType, members = members });
				
				for(int i = 0; i < structType.members.Length; i += 1) {
					structType.members[i] = new DataType_Reference(structType.members[i]);
					DataType.makeUnique(ref structType.members[i]);
				}
				
				if(structNode.inherits != null) {
					if(!valueIsStatic(structNode.inherits.result) || !(structNode.inherits.result.type is DataType_Struct)) {
						throw Jolly.addError(structNode.inherits.location, "Can only inherit from other structs");
					}
					structType.inherits = (DataType_Struct)structNode.inherits.result.type;
				}
			} break;
			case NT.TUPLE: break;
			case NT.OBJECT: break;
			default: throw Jolly.addError(ended.node.location, "Internal compiler error: illigal node used as enclosure");
		}
	}
	
	static void contextEnd(Context ended)
	{
		switch(ended.kind)
		{
			case Context.Kind.IF_CONDITION: {
				var ifNode = (AST_If)ended.target;
				ifNode.trueLabelId = (tempID += 1);
				ifNode.falseLabelId = (tempID += 1);
				instructions.Add(new IR_Br{ condition = ifNode.condition.result, trueLabelId = ifNode.trueLabelId, falseLabelId = ifNode.falseLabelId });
				instructions.Add(new IR_Label{ id = ifNode.trueLabelId });
				contextStack.Push(new Context(cursor + ifNode.ifCount, Context.Kind.IF_TRUE){ target = ifNode });
			} break;
			case Context.Kind.IF_TRUE: {
				var ifNode = (AST_If)ended.target;
				if(ifNode.elseCount > 0) {
					ifNode.endLabelId = (tempID += 1);
					instructions.Add(new IR_Goto{ labelId = ifNode.endLabelId });
					instructions.Add(new IR_Label{ id = ifNode.falseLabelId });
					contextStack.Push(new Context(cursor + ifNode.elseCount, Context.Kind.IF_FALSE) { target = ifNode });
				} else {
					instructions.Add(new IR_Goto{ labelId = ifNode.endLabelId });
					instructions.Add(new IR_Label{ id = ifNode.falseLabelId });
				}
			} break;
			case Context.Kind.IF_FALSE: {
				var ifNode = (AST_If)ended.target;
				instructions.Add(new IR_Goto{ labelId = ifNode.endLabelId });
				instructions.Add(new IR_Label{ id = ifNode.endLabelId });
			} break;
			case Context.Kind.LOGIC_OR: {
				var lor = (AST_Logic)ended.target;
				
				if(lor.a.result.type != Lookup.I1)
				{
					Cast cast;
					if(!Lookup.implicitCasts.getCast(lor.a.result.type, Lookup.I1, out cast)) {
						throw Jolly.addError(lor.a.location, "Cannot use '||' on this type");
					}
					lor.a.result = cast(lor.a.result, Lookup.I1);
				}
				instructions.Add(new IR_Goto{ labelId = lor.trueLabelId });
				instructions.Add(new IR_Label{ id = lor.trueLabelId });
				instructions.Add(new IR_Phi(new PhiBranch[] {
					new PhiBranch(lor.trueLabelId, new Value{ type = Lookup.I1, kind = Value.Kind.STATIC_VALUE, data = true }), // TODO: WRONG
					new PhiBranch(lor.falseLabelId, lor.a.result)
				}));
				lor.result = new Value{ tempID = (tempID += 1), kind = Value.Kind.VALUE, type = Lookup.I1 };
			} break;
			case Context.Kind.LOGIC_AND: {
				var land = (AST_Logic)ended.target;
				
				if(land.a.result.type != Lookup.I1)
				{
					Cast cast;
					if(!Lookup.implicitCasts.getCast(land.a.result.type, Lookup.I1, out cast)) {
						throw Jolly.addError(land.a.location, "Cannot use '&&' on this type");
					}
					land.a.result = cast(land.a.result, Lookup.I1);
				}
				instructions.Add(new IR_Goto{ labelId = land.falseLabelId });
				instructions.Add(new IR_Label{ id = land.falseLabelId });
				instructions.Add(new IR_Phi(new PhiBranch[] {
					new PhiBranch(land.falseLabelId, new Value{ type = Lookup.I1, kind = Value.Kind.STATIC_VALUE, data = false }), // TODO: WRONG
					new PhiBranch(land.trueLabelId, land.a.result)
				}));
				land.result = new Value{ tempID = (tempID += 1), kind = Value.Kind.VALUE, type = Lookup.I1 };
			} break;
			case Context.Kind.DECLARATION: {
				// type inference
				var declaration = (AST_Declaration)ended.target;
				
				if(declaration.result.type == Lookup.AUTO) {
					throw Jolly.addError(declaration.location, "Implicitly-typed variables must be initialized.");
				}
			} break;
			case Context.Kind.FUNCTION_DECLARATION: {
				// The declaration ends after the return values and arguments are parsed.
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
		}
	}
	
	static readonly Dictionary<NT, Action<AST_Node>>
		// Used for the first pass to define all the struct members
		typeDefinitionAnalysers = new Dictionary<NT, Action<AST_Node>>() {
			{ NT.DEFINITION, declare },
			{ NT.FUNCTION, node => {
				var function = (AST_Function)node;
				var table = (SymbolTable)function.symbol;
				tempID = 0;
				// Allocate id's
				foreach(var allocation in table.allocations) {
					allocation.result.tempID = (tempID += 1);
				}
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
			{ NT.TUPLE, node => { } },
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
				_object.onUsed = storeObject;
				_object.startIndex = cursor + 1;
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
				tempID = table.allocations.Count;
				
				enclosureStack.Push(new Enclosure(NT.FUNCTION, function, table, function.memberCount + cursor));
				cursor += function.definitionCount;
			} },
			{ NT.FUNCTION_CALL, node => {
				var functionCall = (AST_FunctionCall)node;
				var functionType = functionCall.function.result.type as DataType_Function;
				if(functionType == null) {
					throw Jolly.addError(node.location, "Can not call this");
				}
				var arguments = functionType.arguments;
				var values = new Value[functionCall.arguments.Length];
				
				for(int i = 0; i < values.Length; i += 1)
				{
					Cast cast = null;
					var arg = functionCall.arguments[i];
					var argT = arguments[i];
					
					load(arg);
					if(!arg.result.type.Equals(argT) && !Lookup.implicitCasts.getCast(arg.result.type, argT, out cast)) {
						throw Jolly.addError(arg.location, "Wrong argument");
					}
					values[i] = cast?.Invoke(functionCall.arguments[i].result, arguments[i]) ?? functionCall.arguments[i].result;
				}
				instructions.Add(new IR_Call(){ function = functionCall.function.result, arguments = values });
			} },
			{ NT.TUPLE, node => { } },
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
				var returnNode = (AST_Return)node;
				AST_Function function = null;
				foreach(var closure in enclosureStack) {
					function = closure.node as AST_Function;
					if(function != null) break;
				}
				Debug.Assert(function != null);
				
				AST_Node[] valueNodes = (returnNode.values as AST_Tuple)?.values.ToArray() ?? new AST_Node[] { returnNode.values };
				DataType[] returns = ((DataType_Function)function.result.type).returns;
				Value[] values = new Value[valueNodes.Length];
				
				for(int i = 0; i < valueNodes.Length; i += 1)
				{
					load(valueNodes[i]);
					Cast cast = null;
					DataType aR = returns[i];
					Value bR = valueNodes[i].result;
					if(aR != bR.type && !Lookup.implicitCasts.getCast(bR.type, aR, out cast)) {
						throw Jolly.addError(valueNodes[i].location, "Invalid return value");
					}
					values[i] = cast?.Invoke(bR, aR) ?? bR;
				}
				instructions.Add(new IR_Return{ values = values });
			} },
			{ NT.MINUS,       node => basicOperator(node, Lookup.subs)    },
			{ NT.PLUS,        node => basicOperator(node, Lookup.adds)    },
			{ NT.MULTIPLY,    node => basicOperator(node, Lookup.muls)    },
			{ NT.DIVIDE,      node => basicOperator(node, Lookup.divs)    },
			{ NT.BIT_OR,      node => basicOperator(node, Lookup.ors)     },
			{ NT.BIT_AND,     node => basicOperator(node, Lookup.ands)    },
			{ NT.BIT_NOT,     node => {
				Instr xor;
				var op = (AST_Operation)node;
				if(!Lookup.xors.TryGetValue(op.a.result.type, out xor)) {
					throw Jolly.addError(op.location, "Cannot use operator '~' on"+node.result.type);
				}
				op.result = xor(op.a.result, new Value{ type = op.a.result.type, kind = Value.Kind.STATIC_VALUE, data = -1 });
			} },
			{ NT.BIT_XOR,     node => basicOperator(node, Lookup.xors)    },
			{ NT.MODULO,      node => basicOperator(node, Lookup.mods)    },
			{ NT.SHIFT_LEFT,  node => basicOperator(node, Lookup.slefts)  },
			{ NT.SHIFT_RIGHT, node => basicOperator(node, Lookup.srights) },
			{ NT.LOGIC_AND,    node => {
				// 'or' and 'and' ended up swapped
				var lor = (AST_Logic)node;
				
				if(lor.condition.result.type != Lookup.I1)
				{
					Cast cast;
					if(!Lookup.implicitCasts.getCast(lor.condition.result.type, Lookup.I1, out cast)) {
						throw Jolly.addError(lor.condition.location, "Cannot use '||' on this type");
					}
					lor.condition.result = cast(lor.condition.result, Lookup.I1);
				}
				
				lor.trueLabelId = (tempID += 1);
				lor.falseLabelId = (tempID += 1);
				instructions.Add(new IR_Br{ trueLabelId = lor.trueLabelId, falseLabelId = lor.falseLabelId, condition = lor.condition.result });
				instructions.Add(new IR_Label{ id = lor.falseLabelId });
				contextStack.Push(new Context(cursor + lor.memberCount, Context.Kind.LOGIC_OR){ target = lor });
			} },
			{ NT.LOGIC_OR,   node => {
				var land = (AST_Logic)node;
				
				if(land.condition.result.type != Lookup.I1)
				{
					Cast cast;
					if(!Lookup.implicitCasts.getCast(land.condition.result.type, Lookup.I1, out cast)) {
						throw Jolly.addError(land.condition.location, "Cannot use '&&' on this type");
					}
					land.condition.result = cast(land.condition.result, Lookup.I1);
				}
				
				land.trueLabelId = (tempID += 1);
				land.falseLabelId = (tempID += 1);
				instructions.Add(new IR_Br{ trueLabelId = land.trueLabelId, falseLabelId = land.falseLabelId, condition = land.condition.result });
				instructions.Add(new IR_Label{ id = land.trueLabelId });
				contextStack.Push(new Context(cursor + land.memberCount, Context.Kind.LOGIC_AND){ target = land });
			} },
			{ NT.BITCAST, node => {
				var op = (AST_Operation)node;
				op.result = Lookup.doCast<IR_Bitcast>(op.a.result, op.b.result.type);
			} },
			{ NT.LOGIC_NOT,   node => {
				Cast cast;
				var op = (AST_Operation)node;
				if(!Lookup.casts.getCast(op.a.result.type, Lookup.I1, out cast)) {
					throw Jolly.addError(op.location, "Cannot use operator '!' on"+node.result.type);
				}
				Instr xor = Lookup.xors[Lookup.I1];
				op.result = xor(cast(op.a.result, Lookup.I1), new Value{ type = Lookup.I1, kind = Value.Kind.STATIC_VALUE, data = true });
			} },
			// { NT.SLICE,		 node => basicOperator(node, Lookup.) },
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
				if(op.a.result.type == op.b.result.type) return;
				
				Cast cast;
				if(!Lookup.casts.getCast(op.b.result.type, op.a.result.type, out cast)) {
					throw Jolly.addError(op.location, "Cannot cast {1} to {0}".fill(op.a.result.type, op.b.result.type));
				}
				op.result = cast(op.b.result, op.a.result.type);
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
			case AST_ModifyType.TO_ARRAY:   mod.result.type = new DataType_Array(mod.target.result.type);     break;
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
				DataType.makeUnique(ref allocType);
				
				definition.allocation.type   = allocType;
				definition.allocation.result = resultValue;
				definition.symbol.type       = resultValue;
				definition.result            = resultValue;
				
				if(context.kind == Context.Kind.FUNCTION_DECLARATION)
				{
					var function     = (AST_Function)enclosure.node;
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
			contextStack.Push(new Context(definition.memberCount + cursor, Context.Kind.DECLARATION));
		}
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
						
			if(b.onUsed?.Invoke(a, b, instructions) ?? false) return;
			
			if(target.referenced != b.result.type)
			{
				Cast cast;
				if(!Lookup.implicitCasts.getCast(b.result.type, target.referenced, out cast)) {
					throw Jolly.addError(a.location, "Cannot assign this value type");
				}
				b.result = cast(b.result, target.referenced);
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
			var varType = ((DataType_Reference)a.result.type).referenced;
			var definition = varType.getMember(a.result, b.text, instructions);
			
			var refType = varType as DataType_Reference;
			if(definition == null && refType != null) {
				load(a);
				definition = refType.referenced.getMember(a.result, b.text, instructions);
			}
			
			if(definition == null) {
				throw Jolly.addError(b.location, "Type does not contain a member {0}".fill(b.text));
			}
			result = definition.Value;
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
	
	static void basicOperator(AST_Node node, Dictionary<DataType, Instr> instrs)
	{
		var op = (AST_Operation)node;
		load(op.a);
		load(op.b);
		if(op.a.result.type != op.b.result.type)
		{
			Cast cast;
			if(Lookup.implicitCasts.getCast(op.a.result.type, op.b.result.type, out cast)) {
				op.a.result = cast(op.a.result, op.b.result.type);
			} else if(Lookup.implicitCasts.getCast(op.b.result.type, op.a.result.type, out cast)) {
				op.b.result = cast(op.b.result, op.a.result.type);
			} else {
				throw Jolly.addError(op.location, "Types not the same");
			}
		}
		
		Instr instr;
		if(!instrs.TryGetValue(op.a.result.type, out instr)) {
			throw Jolly.addError(op.location, "Operator cannot be used on the type "+op.a.result.type);
		}
		
		op.result = instr(op.a.result, op.b.result);
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
		if(node.result.type != null) {
			// Jolly.addNote(node.location, "Name got looked up twice.".fill(node));
			return;
		}
		
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
		// TODO: Zero out struct.
		
		var _object = (AST_Object)obj;
		_object.result = location.result;
		
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
		else if(location.result.type == Lookup.AUTO) {
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