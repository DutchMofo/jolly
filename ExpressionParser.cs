using System.Collections.Generic;
using System.Diagnostics;

namespace Jolly
{
using TT = Token.Type;
using NT = Node.NodeType;
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
	LESS,
	GREATER,
	NEW,
	DELETE,
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

enum DefineMode : byte
{
	NONE,
	MEMBER,
	ARGUMENT,
	FUNCTION_OR_VARIABLE,
	DITTO,
}

enum EnclosureKind : byte
{
	UNDEFINED = 0,
	TUPPLE,
	MEMBER_TUPPLE,
	FUNCTION_CALL,
}

struct Op
{
	public Op(byte precedence, byte valCount, bool leftToRight, OT operation, SourceLocation location = new SourceLocation())
	{
		this.leftToRight = leftToRight;
		this.precedence = precedence;
		this.enclosureKind = EnclosureKind.UNDEFINED;
		this.operation = operation;
		this.location = location;
		this.valCount = valCount;
	}
	public EnclosureKind enclosureKind;
	public byte precedence, valCount;
	public SourceLocation location;
	public bool leftToRight;
	public OT operation;
}

// Parses an expression using a modified Shunting-yard algorithm.
// Throws a ParseException when it's not a valid expression.
class ExpressionParser
{
	public ExpressionParser(Scope scope, Token[] tokens, Token.Type terminator, int cursor, List<Node> program)
	{
		this.startNodeCount = program.Count;
		this.terminator = terminator;
		this.expression = program;
		this.cursor = cursor;
		this.tokens = tokens;
		this.scope = scope;
	}
	
	const byte VALUE_KIND = 1, OPERATOR_KIND = 2, SEPARATOR_KIND = 3;

	byte prevTokenKind = 0,				// Was the previous parsed token an operator or a value
		 currentTokenKind = VALUE_KIND;	// Is the current token being parsed an operator or a value
	DefineMode defineMode;				// Are we defining variables
	
	Dictionary<TT, Op> opLookup;
	int cursor, startNodeCount;
	Token[] tokens;
	TT terminator;
	Token token;
	Scope scope;

	List<Node> expression;// = new List<Node>();
	Stack<Node> values = new Stack<Node>();
	Stack<Op> operators = new Stack<Op>();
	
	public int parseExpression(DefineMode defineMode)
	{
		this.defineMode = defineMode;
		if(this.defineMode != DefineMode.NONE)
			parseDefinition();
		
		this.defineMode = DefineMode.NONE;
		opLookup = Lookup.EXPRESSION_OP;
		
		for(token = tokens[cursor];
			token.type != terminator/* & cursor < end*/;
			token = tokens[cursor += 1])
		{
			switch(token.type)
			{
				case TT.IDENTIFIER:			parseIdentifier();			break;
				case TT.COMMA:				parseComma();				break;
				case TT.FLOAT_LITERAL:		case TT.INTEGER_LITERAL:	case TT.STRING_LITERAL: parseLiteral(); break;
				case TT.BRACKET_OPEN:		parseBracketOpen();			break;
				case TT.BRACKET_CLOSE:		parseBracketClose();		break;
				case TT.PARENTHESIS_OPEN:	parseParenthesisOpen();		break;
				case TT.PARENTHESIS_CLOSE:	parseParenthesisClose();	break;
				default:
					if(token.type >= TT.I8 & token.type <= TT.AUTO) {
						parseBasetype();
						break;
					}
					if(parseOperator())
						break;
						
					// Failed to parse token
					throw Jolly.unexpected(token);
			}
			prevTokenKind = currentTokenKind;
			currentTokenKind = VALUE_KIND;
		}
		
		if(token.type != terminator) {
			throw Jolly.unexpected(token);
		}
		
		while(operators.Count > 0)
			pushOperator(operators.Pop());
		
		return cursor;
	} // parseExpression()
	
	// Tries to define a function or variable,
	// stops when it defines a function or it encounters an unknown token
	void parseDefinition()
	{
		opLookup = Lookup.DEFINE_OP;
		
		for(token = tokens[cursor];
			token.type != terminator/* & cursor < end*/;
			token = tokens[cursor += 1])
		{
			switch(token.type)
			{
				case TT.IDENTIFIER:	parseDefineIdentifier(); break;
				case TT.COMMA:		parseComma();			 break;
				case TT.ASTERISK:	parseTypeToReference();	 break;
				default:
					if(token.type >= TT.I8 & token.type <= TT.AUTO) {
						parseBasetype();
						break;
					}
					if(parseOperator())
						break;
					
					// Definition parser doesn't know what to do with the current token
					return;
			}
			prevTokenKind = currentTokenKind;
			currentTokenKind = VALUE_KIND;
		}
	} // parseDefinition()
	
	void parseLiteral()
	{
		if(prevTokenKind == VALUE_KIND) {
			throw Jolly.unexpected(token);
		}
		NodeLiteral lit;
		if(token.type == TT.INTEGER_LITERAL) {
			lit = new NodeLiteral(token.location, token._integer);
			lit.dataType = Lookup.getBaseType(TT.I32); // TODO: Temporary
		} else if(token.type == TT.FLOAT_LITERAL) {
			lit = new NodeLiteral(token.location, token._float);
			lit.dataType = Lookup.getBaseType(TT.F32); // TODO: Temporary
		} else {
			lit = new NodeLiteral(token.location, token._string);
			lit.dataType = Lookup.getBaseType(TT.STRING);
		}
		lit.typeKind = TypeKind.VALUE;
		values.Push(lit);
	} // parseLiteral()
	
	void parseBasetype()
	{
		if(prevTokenKind == VALUE_KIND) {
			throw Jolly.unexpected(token);
		}
		values.Push(new Node(NT.BASETYPE, token.location) { dataType = Lookup.getBaseType(token.type), typeKind = TypeKind.STATIC });
	}
	
	void parseIdentifier()
	{
		if(prevTokenKind == VALUE_KIND) {
			throw Jolly.unexpected(token);
		}
		var symbol = new NodeSymbol(token.location, token.text, scope);
		expression.Add(symbol);
		values.Push(symbol);
	}
	
	void parseDefineIdentifier()
	{
		Node prev = values.PeekOrDefault();
		if(prevTokenKind == VALUE_KIND)
		{
			// Pop eventual period and comma operators
			while(operators.Count > 0)
				pushOperator(operators.Pop());
			
			// Define
			Token nextToken = tokens[cursor + 1];
			if(nextToken.type == TT.PARENTHESIS_OPEN)
			{ // Function
				if(defineMode != DefineMode.FUNCTION_OR_VARIABLE) {
					throw Jolly.addError(token.location, "Can't define function \"{0}\" here".fill(token.text));
				}
				
				var functionNode = new NodeFunction(token.location, token.text, scope);
				expression.Insert(startNodeCount, functionNode);
				expression.AddRange(values);
				functionNode.returnDefinitionCount = expression.Count - (startNodeCount += 1);
				int _startNodeCount2 = expression.Count;
				
				if(!scope.Add(token.text, null, TypeKind.STATIC)) {
					// TODO: add overloads
					Jolly.addError(token.location, "Trying to redefine function");
				}
				
				var theFunctionScope = new Scope(scope);
				cursor = new ExpressionParser(theFunctionScope, tokens, TT.PARENTHESIS_CLOSE, cursor + 2, expression)
					.parseExpression(DefineMode.ARGUMENT);
				
				functionNode.argumentDefinitionCount = expression.Count - _startNodeCount2;
				
				Token brace = tokens[cursor + 1];
				if(brace.type != TT.BRACE_OPEN) {
					throw Jolly.unexpected(brace);
				}
				
				new BlockParser(cursor + 2, brace.partnerIndex, theFunctionScope, tokens, expression).parseBlock();
				cursor = brace.partnerIndex - 1;
				
				functionNode.memberCount = expression.Count - startNodeCount;
				
				terminator = TT.BRACE_CLOSE; // HACK: stop parsing 
			}
			else
			{ // Variable
				var variable = new NodeSymbol(token.location, token.text, scope, NT.VARIABLE_DEFINITION);
				
				if(defineMode == DefineMode.MEMBER)
				{
					var structType = (DataTypeStruct)scope.dataType;
					if(structType.memberMap.ContainsKey(token.text)) {
						throw Jolly.addError(token.location, "Type {0} already contains a member named {1}".fill(structType.name, token.text));
					}
					structType.memberMap.Add(token.text, structType.memberMap.Count);
					variable.typeKind = TypeKind.STATIC;
				}
				else
				{
					scope.childVariableCount += 1;
					variable.typeKind = TypeKind.VALUE;
					if(!scope.Add(token.text, null, TypeKind.VALUE)) {
						throw Jolly.addError(token.location, "Trying to redefine variable");
					}
				}
				variable.memberCount = expression.Count - startNodeCount;
				
				if(variable.memberCount == 0) {
					variable.memberCount = 1;
					expression.Add(variable);
					expression.Add(prev);
				} else {
					expression.Insert(startNodeCount, variable);
				}
				values.Push(new NodeSymbol(token.location, token.text, scope));
				
				if(defineMode == DefineMode.FUNCTION_OR_VARIABLE)
					defineMode = DefineMode.DITTO;
			}
		}
		else if(prev == null || prev.nodeType != NT.VARIABLE_DEFINITION) {
			var symbol = new NodeSymbol(token.location, token.text, scope);
			expression.Add(symbol);
			values.Push(symbol);
		}
	} // parseDefineIdentifier()
	
	void parseComma()
	{		
		if(prevTokenKind != VALUE_KIND) {
			throw Jolly.unexpected(token);
		}
		
		while(operators.Count > 0 && operators.Peek().valCount > 0)
			pushOperator(operators.Pop());
		
		if(defineMode == DefineMode.ARGUMENT)
		{
			cursor += 1;
			parseDefinition();
		}
		else if(defineMode == DefineMode.DITTO)
		{
			throw new ParseException();
		}	
		else
		{
			parseOperator();
			currentTokenKind = SEPARATOR_KIND;	
		}
	} // parseComma()
	
	// Returns true if it parsed the token
	bool parseOperator()
	{
		Op op;
		if(!opLookup.TryGetValue(token.type, out op))
			return false;
		currentTokenKind = OPERATOR_KIND;
		
		if(prevTokenKind != VALUE_KIND)
		{
			switch(token.type) {
				case TT.ASTERISK: op = new Op(02, 1, false, OT.DEREFERENCE); break;
				case TT.AND		: op = new Op(02, 1, false, OT.REFERENCE  ); break;
				case TT.PLUS: case TT.MINUS: values.Push(new NodeLiteral(token.location, 0)); break;
				default: throw Jolly.unexpected(token);
			}
		}
		
		if(operators.Count > 0)
		{
			Op prevOp = operators.Peek();
			while(prevOp.valCount > 0 && (prevOp.precedence < op.precedence || op.leftToRight && prevOp.precedence == op.precedence)) {
				pushOperator(operators.Pop());
				if(operators.Count == 0) break;
				prevOp = operators.Peek();
			}
		}
		op.location = token.location;
		operators.Push(op);		
		return true;
	} // parseOperator()
	
	void parseBracketOpen()
	{
		currentTokenKind = OPERATOR_KIND;
		
		if(prevTokenKind == OPERATOR_KIND) {
			throw Jolly.unexpected(token);
		}
		operators.Push(new Op(255, 0, false, OT.BRACKET_OPEN, token.location));
	}
	
	void parseBracketClose()
	{
		currentTokenKind = VALUE_KIND;
		
		Op op;
		while((op = operators.PopOrDefault()).operation != OT.BRACKET_OPEN) {
			if(op.operation == OT.UNDEFINED) {
				throw Jolly.unexpected(new Token { type = TT.BRACKET_CLOSE, location = op.location });
			}
			pushOperator(op);
		}
		
		Debug.Assert(false);
	}
	
	void parseParenthesisOpen()
	{
		currentTokenKind = OPERATOR_KIND;
		operators.Push(new Op(255, 0, false, OT.PARENTHESIS_OPEN, token.location) {
			enclosureKind = (prevTokenKind == VALUE_KIND) ?  EnclosureKind.FUNCTION_CALL :
				(prevTokenKind == OPERATOR_KIND && operators.Peek().operation == OT.GET_MEMBER) ? EnclosureKind.MEMBER_TUPPLE :
				EnclosureKind.TUPPLE,
		});
	} // parseParenthesisOpen()
	
	void parseParenthesisClose()
	{
		Op op;
		while((op = operators.PopOrDefault()).operation != OT.PARENTHESIS_OPEN) {
			if(op.operation == OT.UNDEFINED) {
				throw Jolly.unexpected(new Token { type = TT.PARENTHESIS_CLOSE, location = op.location });
			}
			pushOperator(op);
		}
		
		if(op.enclosureKind == EnclosureKind.FUNCTION_CALL)
		{
			Node[] arguments;
			Node symbol = values.Pop();
			if(symbol.nodeType != NT.NAME) {
				arguments = (symbol.nodeType == NT.TUPPLE) ? ((NodeTupple)symbol).values.ToArray() : new Node[] { symbol };
				symbol = values.Pop(); 
			} else {
				arguments = new Node[0];
			}
			Debug.Assert(symbol.nodeType == NT.NAME);
			
			Node node = new NodeFunctionCall(token.location, ((NodeSymbol)symbol).text, scope, arguments);
			expression.Add(node);
			values.Push(node);
		} else {
			// Close list so you can't add to it: (a, b), c
			Node prevVal = values.PeekOrDefault();
			if(prevVal?.nodeType == NT.TUPPLE) {
				var tup = ((NodeTupple)prevVal);
				tup.closed = true;
				expression.Add(tup);
			}
		}
	} // parseParenthesisClose()
	
	void parseTypeToReference()
	{
		if(prevTokenKind == OPERATOR_KIND | prevTokenKind == 0) {
			throw Jolly.unexpected(token);
		}
		Node prev = values.Pop();
		var mod = new NodeModifyType(token.location, prev, NodeModifyType.TO_REFERENCE);
		expression.Add(mod);
		values.Push(mod);
	}
	
	void pushOperator(Op _op)
	{
		Node a, b = null;
		
		if(_op.valCount == 2)
		{
			b = values.PopOrDefault();
			a = values.PopOrDefault();
			
			if(a == null || b == null) {
				throw Jolly.addError(_op.location, "Invalid {0} expression term".fill(a==null ? "left" : "right"));
			}
			
			if(_op.operation == OT.COMMA)
			{
				NodeTupple list = a as NodeTupple;
				if(a.nodeType == NT.TUPPLE && !list.closed) {
					list.values.Add(b);
					values.Push(a);
				}  else {
					list = new NodeTupple(_op.location);
					list.values.Add(a);
					list.values.Add(b);
					values.Push(list);
				}
				return;
			}
			if(_op.operation == OT.GET_MEMBER && b.nodeType == NT.NAME) {
				b.nodeType = NT.MEMBER_NAME;
			}
		}
		else
		{
			a = values.PopOrDefault();
			if(a == null) {
				throw Jolly.addError(_op.location, "Invalid expression term");
			}
		}
		NodeOperator op = new NodeOperator(_op.location, _op.operation, a, b);
		expression.Add(op);
		values.Push(op);
	} // pushOperator()
}

}