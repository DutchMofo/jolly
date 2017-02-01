using System.Collections.Generic;
using System.Diagnostics;

namespace Jolly
{
using TT = Token.Type;
using NT = AST_Node.Type;
using OT = OperatorType;

enum OperatorType
{
	UNDEFINED = 0,
	/*##############
	    Operators
	##############*/
	REFERENCE,
	DEREFERENCE,
	PLUS,
	MINUS,
	INCREMENT,
	DECREMENT,
	LOGIC_AND,
	EQUAL_TO,
	LOGIC_OR,
	LOGIC_NOT,
	BIT_NOT,
	BIT_AND,
	BIT_OR,
	BIT_XOR,
	MODULO,
	DIVIDE,
	MULTIPLY,
	GET_MEMBER,
	SUBSCRIPT,
	READ,
	ASSIGN,
	SHIFT_LEFT,
	SHIFT_RIGHT,
	SLICE,
	CAST,
	BITCAST,
	LESS,
	GREATER,
	NEW,
	DELETE,
	
	TERNARY,
	TERNARY_SELECT,
	COLON,
	/*##########################
	    Compound assignment
	##########################*/
	AND_ASSIGN,
	OR_ASSIGN,
	ASTERISK_ASSIGN,
	MINUS_ASSIGN,
	PLUS_ASSIGN,
	SLASH_ASSIGN,
	PERCENT_ASSIGN,
	CARET_ASSIGN,
	/*##########################
	    Relational operators 
	##########################*/
	NOT_EQUAL,	
	LESS_EQUAL,
	GREATER_EQUAL,
	/*########################
	    Not really operators
	########################*/
	//TODO implement lambda
	lambda_thing,
	
	TYPE_TO_REFERENCE,
	BRACKET_OPEN,
	BRACKET_CLOSE,
	PARENTHESIS_OPEN,
	PARENTHESIS_CLOSE,
	COMMA,
};

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
	struct Enclosure
	{
		public enum Kind : byte
		{
			STATEMENT = 0,
			GROUP     = 1, // (A group are the values between parenthesis)
			TERNARY   = 2,
			SUBSCRIPT = 3,
			TUPLE     = 4,
		}
		
		public Enclosure(int startIndex, Kind kind)
		{
			this.startIndex = startIndex;
			this.isFunctionCall = false;
			this.hasColon = false;
			this.kind = kind;
			this.target = null;
		}
		
		public int startIndex;
		public bool isFunctionCall, hasColon;
		public AST_Node target;
		public Kind kind;
	}
	
	public struct Op
	{
		public Op(byte precedence, byte valCount, bool leftToRight, OT operation, bool isSpecial = false, SourceLocation location = new SourceLocation())
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
		public bool leftToRight, isSpecial; // TODO: Maybe change these to flags?
		public int operatorIndex;
		public OT operation;
	}

	public enum DefineMode : byte
	{
		NONE                 = 0,
		DITTO                = 1,
		MEMBER               = 2,
		ARGUMENT             = 3,
		FUNCTION_OR_VARIABLE = 4,
	}
	
	enum TokenKind : byte
	{
		NONE      = 0,
		VALUE     = 1,
		OPERATOR  = 2,
		SEPARATOR = 3,
	}
	
	public ExpressionParser(SharedParseData parseData, Token.Type terminator, DefineMode defineMode, Scope scope)
	{
		enclosureStack.Push(new Enclosure(parseData.ast.Count, Enclosure.Kind.STATEMENT));
		this.defineMode = defineMode;
		this.terminator = terminator;
		this.parseData = parseData;
		this.scope = scope;
	}
	
	TokenKind prevTokenKind    = TokenKind.SEPARATOR, // Was the previous parsed token an operator or a value
	          currentTokenKind = TokenKind.VALUE;     // Is the current token being parsed an operator or a value
	DefineMode defineMode;                            // Are we defining variables
	
	SharedParseData parseData;
	TT terminator;
	Token token;
	Scope scope;

	Stack<Enclosure> enclosureStack = new Stack<Enclosure>();
	Stack<AST_Node> values = new Stack<AST_Node>();
	Stack<Op> operators = new Stack<Op>();
	
	static Value   BOOL(bool   data) => new Value{ type = Lookup.I1,      kind = Value.Kind.STATIC_VALUE, data = data };
	static Value    INT(ulong  data) => new Value{ type = Lookup.I32,     kind = Value.Kind.STATIC_VALUE, data = data };
	static Value  FLOAT(double data) => new Value{ type = Lookup.F32,     kind = Value.Kind.STATIC_VALUE, data = data };
	static Value STRING(string data) => new Value{ type = Lookup.STRING,  kind = Value.Kind.STATIC_VALUE, data = data };
	
	public AST_Node getValue() => values.PeekOrDefault();
	public void addValue(AST_Node _value)
	{
		prevTokenKind = TokenKind.VALUE;
		values.Push(_value);
	}
	
	public void parseExpression()
	{
		AST_Node _value = null;
		
		for(token = parseData.tokens[parseData.cursor];
			token.type != terminator/* & cursor < end*/;
			token = parseData.tokens[parseData.cursor += 1])
		{
			switch(token.type)
			{
				case TT.IDENTIFIER:        parseIdentifier();       break;
				case TT.COMMA:             parseComma();            break;
				case TT.BRACKET_OPEN:      parseBracketOpen();      break;
				case TT.BRACKET_CLOSE:     parseBracketClose();		break;
				case TT.PARENTHESIS_OPEN:  parseParenthesisOpen();  break;
				case TT.PARENTHESIS_CLOSE: parseParenthesisClose(); break;
				case TT.INTEGER_LITERAL:   _value = new AST_Node(token.location, NT.LITERAL) { result =    INT(token._integer) }; goto case 0;
				case TT.STRING_LITERAL:    _value = new AST_Node(token.location, NT.LITERAL) { result = STRING(token._string)  }; goto case 0;
				case TT.FLOAT_LITERAL:     _value = new AST_Node(token.location, NT.LITERAL) { result =  FLOAT(token._float)   }; goto case 0;
				case TT.TRUE:              _value = new AST_Node(token.location, NT.LITERAL) { result =   BOOL(true)           }; goto case 0;
				case TT.FALSE:             _value = new AST_Node(token.location, NT.LITERAL) { result =   BOOL(false)          }; goto case 0;
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
		
		while(operators.Count > 0) {
			pushOperator(operators.Pop());
		}
		
		while(enclosureStack.Count > 1) {
			enclosureEnd(enclosureStack.Pop());
		}
	} // parseparseData.ast()
	
	void parseIdentifier()
	{
		currentTokenKind = TokenKind.VALUE;
		
		if(prevTokenKind != TokenKind.VALUE) {
			var symbol = new AST_Symbol(token.location, token.text);
			parseData.ast.Add(symbol);
			values.Push(symbol);
			return;
		}
		
		// Pop eventual period and comma operators
		while(operators.Count > 0) {
			pushOperator(operators.Pop());
		}
					
		AST_Node prev = values.Pop();
		Token nextToken = parseData.tokens[parseData.cursor + 1];
		int startNodeCount = enclosureStack.Peek().startIndex;
		// Define
		if(nextToken.type == TT.PARENTHESIS_OPEN)
		{ // Function
			if(defineMode != DefineMode.FUNCTION_OR_VARIABLE) {
				throw Jolly.addError(token.location, "Can't define function \"{0}\" here".fill(token.text));
			}
			
			var functionScope      = new Scope(parent: scope);
			var functionType       = new DataType_Function() { name = token.text };
			var functionDefinition = new Value{ type = functionType, kind = Value.Kind.STATIC_FUNCTION };
			var functionNode       = new AST_Function(token.location, functionScope, token.text)
				{ result = functionDefinition, returns = prev };
			parseData.ast.Insert(startNodeCount, functionNode);
			functionNode.returnDefinitionCount = parseData.ast.Count - (startNodeCount += 1);
			int _startNodeCount2 = parseData.ast.Count;
			
			if(!scope.Add(token.text, functionDefinition)) {
				// TODO: add overloads
				Jolly.addError(token.location, "Trying to redefine function");
			}
			
			parseData.cursor += 2;
			new ExpressionParser(parseData, TT.PARENTHESIS_CLOSE, DefineMode.ARGUMENT, functionScope).parseExpression();
			
			functionType.arguments = new DataType[functionScope.variableCount];
			functionType.returns = new DataType[(prev as AST_Tuple)?.values.Count ?? 1];
			functionNode.argumentDefinitionCount = parseData.ast.Count - _startNodeCount2;
			
			Token brace = parseData.tokens[parseData.cursor + 1];
			if(brace.type != TT.BRACE_OPEN) {
				throw Jolly.unexpected(brace);
			}
			
			parseData.cursor += 2;
			new BlockParser(parseData, brace.partnerIndex, functionScope).parseBlock();
			parseData.cursor = brace.partnerIndex - 1;
			
			functionNode.memberCount = parseData.ast.Count - startNodeCount;
			
			terminator = TT.BRACE_CLOSE; // HACK: stop parsing 
		}
		else
		{ // Variable
			var variable = new AST_VariableDefinition(token.location, scope, token.text, prev);
			
			if(defineMode == DefineMode.MEMBER)
			{
				var structType = (DataType_Struct)scope.scopeType.type;
				if(structType.memberMap.ContainsKey(token.text)) {
					throw Jolly.addError(token.location, "Type {0} already contains a member named {1}".fill(structType.name, token.text));
				}
				structType.memberMap.Add(token.text, structType.memberMap.Count);
				variable.result.kind = Value.Kind.VALUE;
			}
			else
			{
				scope.variableCount += 1;
				variable.result.kind = Value.Kind.VALUE;
				if(!scope.Add(token.text, variable.result)) {
					throw Jolly.addError(token.location, "Trying to redefine variable");
				}
			}
			variable.memberCount = parseData.ast.Count - startNodeCount;
			
			if(variable.memberCount == 0) {
				parseData.ast.Add(variable);
			} else {
				parseData.ast.Insert(startNodeCount, variable);
			}
			values.Push(variable);
			
			if(defineMode == DefineMode.FUNCTION_OR_VARIABLE) {
				defineMode = DefineMode.DITTO;
			}
		}
	} // parseIdentifier()
	
	bool prevToPointer()
	{
		if(operators.PeekOrDefault().operation == OT.MULTIPLY)
		{
			operators.Pop(); // Remove multiply
			AST_Node target = values.PopOrDefault();
			if(target == null) {
				throw Jolly.unexpected(token);
			}
			
			currentTokenKind = TokenKind.VALUE;
			var mod = new AST_ModifyType(token.location, target, AST_ModifyType.TO_POINTER);
			parseData.ast.Add(mod);
			values.Push(mod);
			return true;
		}
		return false;
	}
	
	void parseComma()
	{		
		if(prevTokenKind != TokenKind.VALUE && !prevToPointer()) {
			throw Jolly.unexpected(token);
		}
		
		while(operators.Count > 0 && operators.Peek().valCount > 0) {
			pushOperator(operators.Pop());
		}
		
		if(defineMode == DefineMode.ARGUMENT)
		{
			parseData.cursor += 1;
			// parseDefinition();
		}
		else if(defineMode == DefineMode.DITTO)
		{
			throw new ParseException();
		}	
		else
		{
			parseOperator();
			currentTokenKind = TokenKind.SEPARATOR;	
		}
	} // parseComma()
	
	// Returns true if it parsed the token
	bool parseOperator()
	{
		Op op;
		if(!Lookup.OPERATORS.TryGetValue(token.type, out op))
			return false;
		currentTokenKind = TokenKind.OPERATOR;
		
		if(prevTokenKind != TokenKind.VALUE)
		{
			if(prevToPointer())
				return true;
			
			switch(token.type) {
				case TT.COLON: break;
				case TT.ASTERISK: op = new Op(02, 1, false, OT.DEREFERENCE); break; // TODO: Move these new Op's values to lookup file?
				case TT.AND:	  op = new Op(02, 1, false, OT.REFERENCE  ); break;
				case TT.PLUS: case TT.MINUS: values.Push(new AST_Node(token.location, NT.LITERAL)
					{ result = new Value{ type = Lookup.I32, kind = Value.Kind.STATIC_VALUE, data = 0 } }); break;
				default: throw Jolly.unexpected(token);
			}
		}
		
		if(operators.Count > 0)
		{
			Op prevOp = operators.Peek();
			while(prevOp.valCount > 0 && 
				 (prevOp.precedence < op.precedence ||
				  op.leftToRight && prevOp.precedence == op.precedence))
			{
				pushOperator(operators.Pop());
				if(operators.Count == 0) break;
				prevOp = operators.Peek();
			}
		}
		
		if(op.isSpecial)
		{
			op.operatorIndex = parseData.ast.Count;
			
			if(op.operation == OT.MULTIPLY)
			{
				op.isSpecial = false;
				if(defineMode != DefineMode.NONE) {
					currentTokenKind = TokenKind.VALUE;
					
					AST_Node target = values.PopOrDefault();
					if(target == null) {
						throw Jolly.unexpected(token);
					}
					var mod = new AST_ModifyType(token.location, target, AST_ModifyType.TO_POINTER);
					parseData.ast.Add(mod);
					values.Push(mod);
					return true;
				}
			}
			if(op.operation == OT.TERNARY)
			{
				enclosureStack.Push(new Enclosure(parseData.ast.Count, Enclosure.Kind.TERNARY));
			}
			else if(op.operation == OT.COLON)
			{
				Enclosure enclosure = enclosureStack.Pop();
				if(enclosure.hasColon) {
					throw Jolly.unexpected(token);
				}
				enclosure.hasColon = true;
				
				if(enclosure.kind == Enclosure.Kind.SUBSCRIPT) {
					op.operation = OT.SLICE;
					op.leftToRight = true;
					op.isSpecial = false;
				} else if(enclosure.kind == Enclosure.Kind.TERNARY) {
					op.operation = OT.TERNARY_SELECT;
					op.leftToRight = false;
				} else if(enclosure.kind == Enclosure.Kind.GROUP) {
					op.operation = OT.CAST;
					op.isSpecial = false;
				} else {
					throw Jolly.unexpected(token);
				}
				enclosureStack.Push(enclosure);
			}
			else if(op.operation == OT.BITCAST)
			{
				Enclosure enclosure = enclosureStack.Pop();
				if(enclosure.hasColon) {
					throw Jolly.unexpected(token);
				}
				enclosure.hasColon = true;
				
				if(enclosure.kind == Enclosure.Kind.GROUP) {
					op.isSpecial = false;
				} else {
					throw Jolly.unexpected(token);
				}
				enclosureStack.Push(enclosure);
			}
		}
		
		op.location = token.location;
		operators.Push(op);		
		return true;
	} // parseOperator()
	
	void parseBracketOpen()
	{
		currentTokenKind = TokenKind.OPERATOR;
		
		if(prevTokenKind == TokenKind.OPERATOR) {
			throw Jolly.unexpected(token);
		}
		
		AST_Node target = values.PopOrDefault();
		Debug.Assert(target != null);
		
		operators.Push(new Op(255, 0, false, OT.BRACKET_OPEN, false, token.location));
		enclosureStack.Push(new Enclosure(parseData.ast.Count, Enclosure.Kind.SUBSCRIPT){ target = target });
	}
	
	void parseBracketClose()
	{
		currentTokenKind = TokenKind.VALUE;

		Op op;
		while((op = operators.PopOrDefault()).operation != OT.BRACKET_OPEN) {
			if(op.operation == OT.UNDEFINED) {
				throw Jolly.unexpected(token);
			}
			pushOperator(op);
		}
		
		Enclosure enclosure = enclosureStack.Pop();
		while(enclosure.kind != Enclosure.Kind.SUBSCRIPT) {
			enclosureEnd(enclosure);
			enclosure = enclosureStack.Pop();
		}
		
		// TODO: should check if there are enough values
		if(enclosure.hasColon)
		{
			var node = values.Peek();
			if(node.nodeType == NT.OPERATOR) {
				var slice = node as AST_Operator;
				if(slice.operation != OT.SLICE) {
					throw Jolly.unexpected(node);
				}
			} else if(node.nodeType == NT.MODIFY_TYPE) {
				((AST_ModifyType)node).target = enclosure.target;
			} else {
				throw Jolly.unexpected(node);
			}
		}
		else
		{
			AST_Node a = values.PopOrDefault();
			
			if(a == null) {
				var mod = new AST_ModifyType(op.location, enclosure.target, AST_ModifyType.TO_ARRAY);
				parseData.ast.Add(mod);
				values.Push(mod);
				return;
			}
			var opNode = new AST_Operator(token.location, OT.SUBSCRIPT, a, null);
			parseData.ast.Add(opNode);
			values.Push(opNode);
		}
	}
	
	void parseParenthesisOpen()
	{
		currentTokenKind = TokenKind.OPERATOR;
		
		Enclosure enclosure = new Enclosure(parseData.ast.Count, Enclosure.Kind.GROUP);
		if(prevTokenKind == TokenKind.VALUE) {
			enclosure.isFunctionCall = true;
			enclosure.target = values.Pop();
		}
		enclosureStack.Push(enclosure);
		operators.Push(new Op(255, 0, false, OT.PARENTHESIS_OPEN, false, token.location));
	} // parseParenthesisOpen()
	
	void parseParenthesisClose()
	{
		currentTokenKind = TokenKind.VALUE;
		
		Op op;
		while((op = operators.PopOrDefault()).operation != OT.PARENTHESIS_OPEN) {
			if(op.operation == OT.UNDEFINED) {
				throw Jolly.unexpected(token);
			}
			pushOperator(op);
		}
		
		Enclosure enclosure = enclosureStack.Pop();
		while(enclosure.kind != Enclosure.Kind.GROUP) {
			enclosureEnd(enclosure);
			enclosure = enclosureStack.Pop();
		}
		
		if(enclosure.isFunctionCall)
		{
			AST_Node[] arguments = null;
			if(enclosure.startIndex != parseData.ast.Count) {
				AST_Node node = values.Pop();
				arguments = (node as AST_Tuple)?.values.ToArray() ?? new AST_Node[] { node };
			}
			var call = new AST_FunctionCall(token.location, enclosure.target, arguments ?? new AST_Node[0]);
			parseData.ast.Add(call);
			values.Push(call);
		}
		else if(values.PeekOrDefault()?.nodeType == NT.TUPLE)
		{
			var tup = ((AST_Tuple)values.Pop());
			// Close list so you can't add to it: (a, b), c
			tup.closed = true;
			tup.memberCount = parseData.ast.Count - enclosure.startIndex;
			if(operators.PeekOrDefault().operation == OT.GET_MEMBER) {
				operators.Pop(); // Remove GET_MEMBER
				tup.membersFrom = values.Pop();
				tup.nodeType = NT.MEMBER_TUPLE;
				parseData.ast.Insert(enclosure.startIndex, tup);
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
	
	void enclosureEnd(Enclosure enclosure)
	{
		// Not sure what kind of error to throw yet.
		// This code 'SHOULD' not be reachable
		throw new ParseException();
	}
	
	void pushOperator(Op op)
	{
		AST_Node a, b = null;
		
		if(op.valCount == 2)
		{
			b = values.PopOrDefault();
			a = values.PopOrDefault();
			
			if(op.operation == OT.COMMA)
			{
				AST_Tuple tuple = a as AST_Tuple;
				if(tuple != null && !tuple.closed)
				{
					values.Push(tuple);
					if(b != null) {
						tuple.values.Add(b);
					}
					return;
				}
				tuple = new AST_Tuple(op.location, scope, NT.TUPLE)
					{ result = new Value{ type = Lookup.TUPLE, kind = Value.Kind.STATIC_TYPE } };
				values.Push(tuple);
				tuple.values.Add(a);
				if(b != null) {
					tuple.values.Add(b);
				}
				return;
			}
			else if(op.operation == OT.SLICE)
			{
				if(a == null & b == null) {
					var mod = new AST_ModifyType(op.location, null, AST_ModifyType.TO_SLICE);
					parseData.ast.Add(mod);
					values.Push(mod);
					return;
				}
				// Slice allows a value to be null
				var slice = (a == null) ?
					new AST_Operator(op.location, OT.SLICE, b, null) :
					new AST_Operator(op.location, OT.SLICE, a, b);
				parseData.ast.Add(slice);
				values.Push(slice);
				return;
			}
			
			if(a == null) {
				throw Jolly.addError(op.location, "Expecting 2 values");
			}
			if(op.operation == OT.GET_MEMBER && b.nodeType == NT.NAME) {
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
		
		if(op.operation != OT.GET_MEMBER) {
			defineMode = DefineMode.NONE;
		}
		
		if(op.isSpecial)
		{
			if(op.operation == OT.LOGIC_AND)
			{
				int memberCount = parseData.ast.Count - op.operatorIndex;
				var logic = new AST_Logic(op.location, OT.LOGIC_OR, memberCount, memberCount, condition: a, a: b, b: null);
				parseData.ast.Insert(op.operatorIndex, logic);
				values.Push(logic);
				return;
			}
			else if(op.operation == OT.LOGIC_OR)
			{
				int memberCount = parseData.ast.Count - op.operatorIndex;
				var logic = new AST_Logic(op.location, OT.LOGIC_AND, memberCount, memberCount, condition: a, a: b, b: null);
				parseData.ast.Insert(op.operatorIndex, logic);
				values.Push(logic);
				return;
			}
			else if(op.operation == OT.TERNARY)
			{
				Enclosure enclosure = enclosureStack.Pop();
				while(enclosure.kind != Enclosure.Kind.TERNARY) {
					enclosureEnd(enclosure);
					enclosure = enclosureStack.Pop();
				}
				enclosure.target = a;
				enclosureStack.Push(enclosure);
				values.Push(b);
				return;
			}
			else if(op.operation == OT.TERNARY_SELECT)
			{
				Enclosure enclosure = enclosureStack.Pop();
				while(enclosure.kind != Enclosure.Kind.TERNARY) {
					enclosureEnd(enclosure);
					enclosure = enclosureStack.Pop();
				}
				
				int memberCount = parseData.ast.Count - enclosure.startIndex,
					count = op.operatorIndex - enclosure.startIndex;
				var logic = new AST_Logic(op.location, OT.TERNARY, memberCount, count, enclosure.target, a, b);
				parseData.ast.Insert(enclosure.startIndex, logic);
				values.Push(logic);
				return;
			}
		} // if(op.isSpecial)
		
		AST_Operator opNode = new AST_Operator(op.location, op.operation, a, b);
		parseData.ast.Add(opNode);
		values.Push(opNode);
	} // pushOperator()
}

}