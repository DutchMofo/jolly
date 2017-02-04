using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Jolly
{
using TT = Token.Type;
using NT = AST_Node.Type;

class SharedParseData
{
	public int cursor;
	public Token[] tokens;
	public List<AST_Node> ast;
}

// Parses an parseData.ast using a modified Shunting-yard algorithm.
// Throws a ParseException when it's not a valid parseData.ast.
class ExpressionParser
{
	public struct Context
	{
		public enum Kind : byte
		{
			STATEMENT,
			
			GROUP,      // (A group are the values between parenthesis)
			OBJECT,     // An object are the initializers between braces: SomeStruct t = { a: 0, b: 1, };
			TERNARY,
			SUBSCRIPT,
			// The definition context is the moment a variable is defined
			// till the end of the statement: auto begin = 10; // <- end
			// the type of an auto variable must be inferred bofere the end.
			DEFINITION,
			TEMPLATE_LIST,
		}
		
		public Context(int startIndex, Kind kind)
		{
			this.startIndex = startIndex;
			this.isFunctionCall = false;
			this.hasColon = false;
			this.target = null;
			this.kind = kind;
		}
		
		public int startIndex;
		public bool isFunctionCall, hasColon;
		public AST_Node target;
		public Kind kind;
		
		public override string ToString()
			=> "kind: {0}, start: {1}, target: {2}, ".fill(kind, startIndex, target);
	}
	
	public struct Operator
	{
		public Operator(byte precedence, byte valCount, bool leftToRight, NT operation, bool isSpecial = false, SourceLocation location = new SourceLocation())
		{
			this.leftToRight = leftToRight;
			this.precedence = precedence;
			this.operation = operation;
			this.isSpecial = isSpecial;
			this.location = location;
			this.valCount = valCount;
			this.operatorIndex = 0;
		}
		public byte precedence, valCount;
		public SourceLocation location;
		public bool leftToRight, isSpecial;
		public int operatorIndex;
		public NT operation;
		
		public override string ToString()
			=> "op: {0}, p: {1}, {2}".fill(operation, precedence, leftToRight ? "->" : "<-");
	}

	enum TokenKind : byte
	{
		NONE      = 0,
		VALUE     = 1,
		OPERATOR  = 2,
		SEPARATOR = 3,
	}
	
	public enum DefineMode : byte
	{
		STATEMENT,  // Can define function or variable
		MEMBER,     // Can only define variables, no expression
		ARGUMENT,   // Can only define variables, no expression
		EXPRESSION, // Only a expression, can't define anything
	}
	
	public ExpressionParser(SharedParseData parseData, Token.Type terminator, SymbolTable scope, DefineMode defineMode, int end)
	{
		contextStack.Push(new Context(parseData.ast.Count, Context.Kind.STATEMENT));
		this.terminator = terminator;
		this.parseData = parseData;
		this.defineMode = defineMode;
		this.canDefine = true;
		this.scope = scope;
		this.end = end;
	}
	
	// Was the previouly parsed token a value (literal, identifier, object),
	// operator or a separator (comma)
	TokenKind prevTokenKind    = TokenKind.OPERATOR, // Was the previous parsed token an operator or a value
	          currentTokenKind = TokenKind.VALUE;     // Is the current token being parsed an operator or a value
	
	// The first defined variable
	AST_Declaration firstDefined;
	SharedParseData parseData;
	DefineMode defineMode;
	SymbolTable scope;
	bool canDefine;
	TT terminator;
	Token token;
	int end;

	Stack<Context> contextStack = new Stack<Context>();
	Stack<AST_Node> values = new Stack<AST_Node>();
	Stack<Operator> operators = new Stack<Operator>();
	
	static Value   BOOL(bool   data) => new Value{ type = Lookup.I1,     kind = Value.Kind.STATIC_VALUE, data = data };
	static Value    INT(ulong  data) => new Value{ type = Lookup.I32,    kind = Value.Kind.STATIC_VALUE, data = data };
	static Value  FLOAT(double data) => new Value{ type = Lookup.F32,    kind = Value.Kind.STATIC_VALUE, data = data };
	static Value STRING(string data) => new Value{ type = Lookup.STRING, kind = Value.Kind.STATIC_VALUE, data = data };
	
	static Value TUPLE() => new Value { type = Lookup.TUPLE, kind = Value.Kind.STATIC_TYPE };
	
	public bool isDefinition() => firstDefined != null;
	
	public AST_Node getValue() => values.PeekOrDefault();
	public void addValue(AST_Node _value)
	{
		prevTokenKind = TokenKind.VALUE;
		values.Push(_value);
	}
	
	public ExpressionParser parse(bool allowEarlyExit)
	{
		AST_Node _value = null;
		Debug.Assert(contextStack.Count > 0); // Context must be set
		
		for(token = parseData.tokens[parseData.cursor];
			token.type != terminator & parseData.cursor < end;
			token = parseData.tokens[parseData.cursor += 1])
		{
			switch(token.type)
			{
				case TT.IDENTIFIER:        parseIdentifier();       break;
				case TT.COMMA:             parseComma();            break;
				case TT.PARENTHESIS_OPEN:  parseParenthesisOpen();  break;
				case TT.PARENTHESIS_CLOSE: parseParenthesisClose(); break;
				case TT.BRACKET_OPEN:      parseBracketOpen();      break;
				case TT.BRACKET_CLOSE:     parseBracketClose();		break;
				case TT.BRACE_OPEN:        parseBraceOpen();        break;
				case TT.BRACE_CLOSE:       parseBraceClose();		break;
				case TT.INTEGER_LITERAL:   _value = new AST_Node(token.location, NT.LITERAL) { result =    INT(token._integer) }; goto case 0;
				case TT.STRING_LITERAL:    _value = new AST_Node(token.location, NT.LITERAL) { result = STRING(token._string)  }; goto case 0;
				case TT.FLOAT_LITERAL:     _value = new AST_Node(token.location, NT.LITERAL) { result =  FLOAT(token._float)   }; goto case 0;
				case TT.TRUE:              _value = new AST_Node(token.location, NT.LITERAL) { result =   BOOL(true)           }; goto case 0;
				case TT.FALSE:             _value = new AST_Node(token.location, NT.LITERAL) { result =   BOOL(false)          }; goto case 0;
				case TT.SEMICOLON:         if(allowEarlyExit) goto breakLoop; else throw Jolly.unexpected(token);
				default:
					if(token.type >= TT.I1 & token.type <= TT.AUTO) {
						_value = new AST_Node(token.location, NT.BASETYPE) 
							{ result = new Value{ type = Lookup.getBaseType(token.type), kind = Value.Kind.STATIC_TYPE } };
						goto case 0;
					}
					if(parseOperator())
						break;
					// Failed to parse token
					throw Jolly.unexpected(token);
				case 0: // Hacky
					if(prevTokenKind == TokenKind.VALUE) {
						throw Jolly.unexpected(token);
					}
					currentTokenKind = TokenKind.VALUE;
					values.Push(_value);
					break;
			}
			prevTokenKind = currentTokenKind;
		}
		
		goto breakLoop;
		breakLoop:
		
		// An early exit is when you exit on a semicolon and not the terminator
		if(!allowEarlyExit && token.type != terminator) {
			throw Jolly.unexpected(token);
		}
		
		while(operators.Count > 0) {
			pushOperator(operators.Pop());
		}
		
		while(contextStack.Count > 1) {
			contextEnd(contextStack.Pop());
		}
		
		return this; // Make calls chainable: new ExpressionParser(...).parse();
	} // parseExpression()
	
	void parseIdentifier()
	{
		currentTokenKind = TokenKind.VALUE;
		string name = token.text;
		AST_Node target = null;
		
		switch(prevTokenKind)
		{
			case TokenKind.VALUE:
				// Pop eventual period and comma operators
				while(operators.Count > 0) {
					pushOperator(operators.Pop());
				}
				target = values.Pop();
				break;
			case TokenKind.SEPARATOR:
				if(defineMode != DefineMode.ARGUMENT && firstDefined != null) {
					target = firstDefined.typeFrom;
					break;
				}
				goto default;
			default:
				var symbol = new AST_Symbol(token.location, null, name);
				parseData.ast.Add(symbol);
				values.Push(symbol);
				return;
		}
		
		Token nextToken = parseData.tokens[parseData.cursor + 1];
		int startNodeCount = contextStack.Peek().startIndex;
		
		if(!canDefine) {
			throw Jolly.addError(token.location, "Can't define the {0} \"{1}\" here.".fill(
				(nextToken.type == TT.PARENTHESIS_OPEN) ? "function" : "variable",
				token.text));
		}
		
		// Define
		if(nextToken.type == TT.PARENTHESIS_OPEN)
		{ // Function
			if(defineMode !=DefineMode.STATEMENT) {
				throw Jolly.addError(token.location, "Can't define the function \"{0}\" here".fill(name));
			}
			
			DataType_Function functionType  = new DataType_Function();
			AST_Function      functionNode  = new AST_Function(token.location);
			SymbolTable       functionTable = new SymbolTable(scope);
			
			functionNode.symbol = functionTable;
			functionNode.text   = functionType.name  = name;
			functionNode.result = functionTable.type = new Value { kind = Value.Kind.STATIC_FUNCTION, type = functionType };
			
			if(!scope.Add(name, functionTable)) {
				// TODO: add overloads
				Jolly.addError(token.location, "Trying to redefine function");
			}
			
			parseData.ast.Insert(startNodeCount, functionNode);
			functionNode.returnCount = parseData.ast.Count - (startNodeCount += 1); // Skip the function node itself
			
			parseData.cursor += 2;
			functionTable.canAllocate = true;
			new ExpressionParser(parseData, TT.PARENTHESIS_CLOSE, functionTable, DefineMode.ARGUMENT, nextToken.partnerIndex)
				.parse(false);
			
			functionNode.returns         = target;
			functionType.arguments       = new DataType[functionTable.allocations.Count];
			functionType.returns         = new DataType[(target as AST_Tuple)?.values.Count ?? 1];
			functionNode.definitionCount = parseData.ast.Count - startNodeCount;
			
			Token brace = parseData.tokens[parseData.cursor + 1];
			if(brace.type != TT.BRACE_OPEN) {
				throw Jolly.unexpected(brace);
			}
			
			parseData.cursor += 2; // Skip parenthesis close and brace open
			new ScopeParser(parseData, brace.partnerIndex, functionTable).parseBlockScope();
			functionNode.memberCount = parseData.ast.Count - startNodeCount;
			
			parseData.cursor = brace.partnerIndex - 1;
			terminator = TT.BRACE_CLOSE; // HACK: stop parsing 
		}
		else
		{ // Variable
			var variableNode = declareVariable(name, target);
			
			firstDefined = variableNode;
			parseData.ast.Add(variableNode);
			values.Push(variableNode);
			contextStack.Push(new Context(parseData.ast.Count, Context.Kind.DEFINITION) { target = variableNode });
		}
	} // parseIdentifier()
	
	AST_Declaration declareVariable(string name, AST_Node target)
	{
		AST_Declaration variableNode;
			
		if(defineMode == DefineMode.MEMBER)
		{
			var structType = (DataType_Struct)scope.type.type;
			if(structType.memberMap.ContainsKey(name)) {
				throw Jolly.addError(token.location, "Type {0} already contains a member named {1}".fill(structType.name, name));
			}
			structType.memberMap.Add(name, structType.memberMap.Count);
			
			variableNode = new AST_Declaration(token.location, target, scope, name);
			variableNode.result.kind = Value.Kind.VALUE;
		}
		else if(defineMode == DefineMode.STATEMENT ||
				defineMode == DefineMode.ARGUMENT)
		{
			Symbol variableSymbol = new Symbol(scope);
			       variableNode   = new AST_Declaration(token.location, target);
			
			variableNode.symbol     = variableSymbol;
			variableNode.text       = name;
			variableNode.result     = variableSymbol.type = new Value { kind = Value.Kind.VALUE };
			variableNode.allocation = scope.allocateVariable();
			
			if(!scope.Add(name, variableSymbol)) {
				throw Jolly.addError(token.location, "Trying to redefine variable");
			}
		} else {
			throw Jolly.addError(token.location, "Can't define the variable \"{0}\" here.".fill(name));
		}
		return variableNode;
	}
	
	void modifyType(byte toType)
	{
		AST_Node target = values.PopOrDefault();
		if(target == null) {
			throw Jolly.unexpected(token);
		}
		
		currentTokenKind = TokenKind.VALUE;
		var mod = new AST_ModifyType(token.location, target, toType);
		parseData.ast.Add(mod);
		values.Push(mod);
	}
	
	bool prevIsTypeModifier()
	{
		switch(operators.PeekOrDefault().operation) {
			case NT.MULTIPLY: modifyType(AST_ModifyType.TO_POINTER ); break;
			case NT.TERNARY:  modifyType(AST_ModifyType.TO_NULLABLE); break;
			default: return false;
		}
		operators.Pop(); // Remove multiply / ternary
		return true;
	}
	
	void parseComma()
	{		
		if(prevTokenKind != TokenKind.VALUE && !prevIsTypeModifier()) {
			throw Jolly.unexpected(token);
		}
		
		while(operators.Count > 0 && operators.Peek().valCount > 0) {
			pushOperator(operators.Pop());
		}
		
		parseOperator();
		currentTokenKind = TokenKind.SEPARATOR;
		canDefine = true;
	} // parseComma()
	
	// Returns true if it parsed the token
	bool parseOperator()
	{
		Operator op;
		if(!Lookup.OPERATORS.TryGetValue(token.type, out op))
			return false;
		currentTokenKind = TokenKind.OPERATOR;
		
		if(prevTokenKind != TokenKind.VALUE)
		{
			// If the previous operator was an asterisk or question mark
			// then they arent operators but a pointer and nullable pointer:
			// _ = (i32?*: null);
			if(prevIsTypeModifier())
				return true;
			
			// If an operator is followed by a plus or minus operator they are unary: number * -10;
			// when it's an asterisk it is as dereference: 10 - *number;
			// in the context of a subscript a bracket open can be followed by a colon: int[:] data = values[:10];
			switch(token.type) {
				case TT.COLON:
					if(contextStack.Peek().kind == Context.Kind.SUBSCRIPT) {
						throw Jolly.unexpected(token);
					}
					break;
				case TT.ASTERISK: op = new Operator(02, 1, false, NT.DEREFERENCE); break; // TODO: Move these new Op's values to lookup file?
				case TT.AND:	  op = new Operator(02, 1, false, NT.REFERENCE  ); break;
				case TT.PLUS: case TT.MINUS: values.Push(new AST_Node(token.location, NT.LITERAL) { result = INT(0) }); break;
				default: throw Jolly.unexpected(token);
			}
		}
		
		Operator prevOp = operators.PeekOrDefault();
		// valCount of default(Op) == 0
		while(prevOp.valCount > 0 && 
			(prevOp.precedence < op.precedence || op.leftToRight && prevOp.precedence == op.precedence))
		{
			pushOperator(operators.Pop());
			prevOp = operators.PeekOrDefault();
		}
		
		if(op.isSpecial)
		{
			op.operatorIndex = parseData.ast.Count;
			
			if(op.operation == NT.MULTIPLY)
			{
				if(canDefine) {
					currentTokenKind = TokenKind.VALUE;
					modifyType(AST_ModifyType.TO_POINTER);
					return true;
				}
				op.isSpecial = false;
			}
			else if(op.operation == NT.TERNARY)
			{
				if(canDefine) {
					currentTokenKind = TokenKind.VALUE;
					modifyType(AST_ModifyType.TO_NULLABLE);
					return true;
				}
				contextStack.Push(new Context(parseData.ast.Count, Context.Kind.TERNARY));
			}
			else if(op.operation == NT.COLON)
			{
				Context context = contextStack.Pop();
				if(context.hasColon) {
					throw Jolly.unexpected(token);
				}
				context.hasColon = true;
				
				if(context.kind == Context.Kind.SUBSCRIPT) {
					op.operation = NT.SLICE;
					op.leftToRight = true;
					op.isSpecial = false;
				} else if(context.kind == Context.Kind.TERNARY) {
					op.operation = NT.TERNARY_SELECT;
					op.leftToRight = false;
				} else if(context.kind == Context.Kind.GROUP) {
					op.operation = NT.CAST;
					op.leftToRight = false;
					op.isSpecial = false;
				} else if(context.kind == Context.Kind.OBJECT) {
					op.operation = NT.INITIALIZER;
					op.leftToRight = false;
					op.isSpecial = false;
					
					context.hasColon = false; // Lazy, ignore hasColon check
				} else {
					throw Jolly.unexpected(token);
				}
				// Push popped context back because c#
				contextStack.Push(context);
			}
			else if(op.operation == NT.BITCAST)
			{
				Context context = contextStack.Pop();
				if(context.hasColon) {
					throw Jolly.unexpected(token);
				}
				context.hasColon = true;
				
				if(context.kind == Context.Kind.GROUP) {
					// For consistency sake only allow bitcast in a group
					// like the cast operator: _ = (i32:~ .5);
					op.isSpecial = false;
				} else {
					throw Jolly.unexpected(token);
				}
				contextStack.Push(context);
			}
		}
		
		op.location = token.location;
		operators.Push(op);		
		return true;
	} // parseOperator()
	
	void parseBraceOpen()
	{
		currentTokenKind = TokenKind.OPERATOR;
		
		Operator prevOp = operators.PeekOrDefault();
		// valCount of default(Op) == 0
		while(prevOp.valCount > 0 && prevOp.precedence < 14) {
			pushOperator(operators.Pop());
			prevOp = operators.PeekOrDefault();
		}
		
		AST_Node targetType = null;
		if(prevTokenKind == TokenKind.VALUE) {
			targetType = values.Pop();
		}
		
		values.Push(null);
		operators.Push(new Operator(255, 0, false, NT.BRACE_OPEN, false, token.location));
		contextStack.Push(new Context(parseData.ast.Count, Context.Kind.OBJECT){ target = targetType });
	}
	
	void parseBraceClose()
	{
		currentTokenKind = TokenKind.VALUE;

		Operator op;
		while((op = operators.PopOrDefault()).operation != NT.BRACE_OPEN) {
			if(op.operation == NT.UNDEFINED) {
				throw Jolly.unexpected(token);
			}
			pushOperator(op);
		}
		
		Context context = contextStack.Pop();
		while(context.kind != Context.Kind.OBJECT) {
			contextEnd(context);
			context = contextStack.Pop();
		}
		
		AST_Node[] initializers = null;
		if(context.startIndex != parseData.ast.Count) {
			AST_Node node = values.Pop();	
			initializers = (node as AST_Tuple)?.values.ToArray() ?? new AST_Node[] { node };
		}
		
		bool isArray = initializers?.All(i => i.nodeType != NT.INITIALIZER) ?? false;
		
		if(!isArray)
		{
			initializers?.forEach(i => {
				if(i.nodeType != NT.INITIALIZER) {
					throw Jolly.addError(i.location, "Invalid intializer member declarator");
				}
				((AST_Operation)i).a.nodeType = NT.OBJECT_MEMBER_NAME;
			});
		}
		
		// Use an AST_Definition for now
		var _object = new AST_Object(op.location, NT.OBJECT) { 
			memberCount = parseData.ast.Count - context.startIndex,
			inferFrom = context.target,
			nodeType = NT.OBJECT,
			isArray = isArray,
		};
		
		if(values.Peek() == null) {
			values.Pop();
		}
		
		parseData.ast.Insert(context.startIndex, _object);
		values.Push(_object);
	}
	
	void parseBracketOpen()
	{
		currentTokenKind = TokenKind.OPERATOR;
		
		if(prevTokenKind == TokenKind.OPERATOR) {
			throw Jolly.unexpected(token);
		}
		
		AST_Node target = values.PopOrDefault();
		if(target == null) {
			throw Jolly.unexpected(token);
		}
		operators.Push(new Operator(255, 0, false, NT.BRACKET_OPEN, false, token.location));
		contextStack.Push(new Context(parseData.ast.Count, Context.Kind.SUBSCRIPT){ target = target });
	}
	
	void parseBracketClose()
	{
		currentTokenKind = TokenKind.VALUE;

		Operator op;
		while((op = operators.PopOrDefault()).operation != NT.BRACKET_OPEN) {
			if(op.operation == NT.UNDEFINED) {
				throw Jolly.unexpected(token);
			}
			pushOperator(op);
		}
		
		Context context = contextStack.Pop();
		while(context.kind != Context.Kind.SUBSCRIPT) {
			contextEnd(context);
			context = contextStack.Pop();
		}
		
		if(context.hasColon)
		{
			var node = values.Peek();
			if(node.nodeType == NT.MODIFY_TYPE) {
				((AST_ModifyType)node).target = context.target;
			} else if(node.nodeType != NT.SLICE) {
				throw Jolly.unexpected(node);
			}
		}
		else
		{
			AST_Node a = values.PopOrDefault();
			
			if(a == null) {
				var mod = new AST_ModifyType(op.location, context.target, AST_ModifyType.TO_ARRAY);
				parseData.ast.Add(mod);
				values.Push(mod);
				return;
			}
			var opNode = new AST_Operation(token.location, NT.SUBSCRIPT, a, null);
			parseData.ast.Add(opNode);
			values.Push(opNode);
		}
	}
	
	void parseParenthesisOpen()
	{
		currentTokenKind = TokenKind.OPERATOR;
		
		Context context = new Context(parseData.ast.Count, Context.Kind.GROUP);
		if(prevTokenKind == TokenKind.VALUE) {
			context.isFunctionCall = true;
			context.target = values.Pop();
		}
		contextStack.Push(context);
		operators.Push(new Operator(255, 0, false, NT.PARENTHESIS_OPEN, false, token.location));
	} // parseParenthesisOpen()
	
	void parseParenthesisClose()
	{
		currentTokenKind = TokenKind.VALUE;
		
		Operator op;
		while((op = operators.PopOrDefault()).operation != NT.PARENTHESIS_OPEN) {
			if(op.operation == NT.UNDEFINED) {
				throw Jolly.unexpected(token);
			}
			pushOperator(op);
		}
		
		Context context = contextStack.Pop();
		while(context.kind != Context.Kind.GROUP) {
			contextEnd(context);
			context = contextStack.Pop();
		}
		
		if(context.isFunctionCall)
		{
			if(canDefine)
			{
				Debug.Fail("Not implemented");
				// Define multiple varibales: int (i, j, k);
				
				var target = context.target;
				var tup = values.PeekOrDefault() as AST_Tuple;
				if(tup == null) {
					throw Jolly.addError(op.location, "Expected a list of names");
				}
				
				parseData.ast.RemoveRange(parseData.ast.Count - tup.values.Count, tup.values.Count);
				for(int i = 0; i < tup.values.Count; i += 1)
				{
					var name = tup.values[i] as AST_Symbol;
					if(name == null) {
						throw Jolly.addError(tup.values[i].location, "Expected a name");
					}	
					var definition = declareVariable(name.text, target);
					parseData.ast.Add(tup.values[i] = definition);
				}
				
				// contextStack.Push(new Context(parseData.ast.Count, Context.Kind.DEFINITION) { target = v });
				parseData.ast.Add(tup);
			}
			else
			{
				AST_Node[] arguments = null;
				if(context.startIndex != parseData.ast.Count) {
					AST_Node node = values.Pop();
					arguments = (node as AST_Tuple)?.values.ToArray() ?? new AST_Node[] { node };
				}
				var call = new AST_FunctionCall(token.location, context.target, arguments ?? new AST_Node[0]);
				parseData.ast.Add(call);
				values.Push(call);
			}
		}
		else if(values.PeekOrDefault()?.nodeType == NT.TUPLE)
		{
			var tup = ((AST_Tuple)values.Pop());
			// Close list so you can't add to it: (a, b), c
			tup.closed = true;
			tup.memberCount = parseData.ast.Count - context.startIndex;
			if(operators.PeekOrDefault().operation == NT.GET_MEMBER) {
				operators.Pop(); // Remove GET_MEMBER
				tup.membersFrom = values.Pop();
				tup.nodeType = NT.MEMBER_TUPLE;
				parseData.ast.Insert(context.startIndex, tup);
			} else {
				parseData.ast.Add(tup);
			}
			values.Push(tup);
		}
	} // parseParenthesisClose()
	
	void parseTypeToReference()
	{
		currentTokenKind = TokenKind.VALUE;
		
		if(prevTokenKind == TokenKind.OPERATOR) {
			throw Jolly.unexpected(token);
		}
		AST_Node prev = values.Pop();
		if(prev == null) {
			throw Jolly.addError(token.location, "Invalid expression term");
		}
		var mod = new AST_ModifyType(token.location, prev, AST_ModifyType.TO_POINTER);
		parseData.ast.Add(mod);
		values.Push(mod);
	}
	
	void contextEnd(Context context)
	{
		if(context.kind == Context.Kind.DEFINITION) {
			((AST_Declaration)context.target).memberCount = parseData.ast.Count - context.startIndex;
			return;
		}
		
		// Not sure what kind of error to throw yet.
		// This code 'SHOULD' not be reachable
		Debug.Fail("Illigal context popped");
	}
	
	void pushOperator(Operator op)
	{
		AST_Node a, b = null;
		
		if(op.valCount == 2)
		{
			b = values.PopOrDefault();
			a = values.PopOrDefault();
			
			if(op.operation == NT.COMMA)
			{
				if(a == null) {
					values.Push(null);
					if(!(b is AST_Tuple)) {
						var tup = new AST_Tuple(b.location, NT.TUPLE) { result = TUPLE() };
						tup.values.Add(b);
						values.Push(tup);
						return;
					} 
					values.Push(b);
					return;
				}
				
				AST_Tuple tuple = a as AST_Tuple;
				if(tuple?.closed ?? true) {
					tuple = new AST_Tuple(b.location, NT.TUPLE) { result = TUPLE() };
					tuple.values.Add(a);
				}
				tuple.values.Add(b);
				values.Push(tuple);
				return;
			}
			else if(op.operation == NT.SLICE)
			{
				if(a == null & b == null) {
					var mod = new AST_ModifyType(op.location, null, AST_ModifyType.TO_SLICE);
					parseData.ast.Add(mod);
					values.Push(mod);
					return;
				}
				// Slice allows a value to be null
				var slice = (a == null) ?
					new AST_Operation(op.location, NT.SLICE, b, null) :
					new AST_Operation(op.location, NT.SLICE, a, b);
				parseData.ast.Add(slice);
				values.Push(slice);
				return;
			}
			
			if(a == null) {
				throw Jolly.addError(op.location, "Expecting 2 values");
			}
			if(op.operation == NT.GET_MEMBER && b.nodeType == NT.NAME) {
				b.nodeType = NT.MEMBER_NAME;
			}
		}
		else
		{
			a = values.PopOrDefault();
			if(a == null) {
				throw Jolly.addError(op.location, "Invalid expression term");
			}
		}
		
		if(op.operation != NT.GET_MEMBER) {
			canDefine = false;
		}
		
		if(op.isSpecial)
		{
			if(op.operation == NT.LOGIC_AND)
			{
				int memberCount = parseData.ast.Count - op.operatorIndex;
				var logic = new AST_Logic(op.location, NT.LOGIC_OR, memberCount, memberCount, condition: a, a: b, b: null);
				parseData.ast.Insert(op.operatorIndex, logic);
				values.Push(logic);
				return;
			}
			else if(op.operation == NT.LOGIC_OR)
			{
				int memberCount = parseData.ast.Count - op.operatorIndex;
				var logic = new AST_Logic(op.location, NT.LOGIC_AND, memberCount, memberCount, condition: a, a: b, b: null);
				parseData.ast.Insert(op.operatorIndex, logic);
				values.Push(logic);
				return;
			}
			else if(op.operation == NT.TERNARY)
			{
				Context context = contextStack.Pop();
				while(context.kind != Context.Kind.TERNARY) {
					contextEnd(context);
					context = contextStack.Pop();
				}
				context.target = a;
				contextStack.Push(context);
				values.Push(b);
				return;
			}
			else if(op.operation == NT.TERNARY_SELECT)
			{
				Context context = contextStack.Pop();
				while(context.kind != Context.Kind.TERNARY) {
					contextEnd(context);
					context = contextStack.Pop();
				}
				
				int memberCount = parseData.ast.Count - context.startIndex,
					count = op.operatorIndex - context.startIndex;
				var logic = new AST_Logic(op.location, NT.TERNARY, memberCount, count, context.target, a, b);
				parseData.ast.Insert(context.startIndex, logic);
				values.Push(logic);
				return;
			}
			Jolly.addNote(op.location, "Compiler: unnecessary operator marked special {0}".fill(op.operation));
		} // if(op.isSpecial)
		
		AST_Operation opNode = new AST_Operation(op.location, op.operation, a, b);
		parseData.ast.Add(opNode);
		values.Push(opNode);
	} // pushOperator()
}

}