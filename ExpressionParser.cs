using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

struct Op
{
	public Op(byte precedence, byte valCount, bool leftToRight, OT operation, SourceLocation location = new SourceLocation())
	{
		this.leftToRight = leftToRight;
		this.precedence = precedence;
		this.operation = operation;
		this.location = location;
		this.valCount = valCount;
		this.startNodeCount = 0;
		this.parenthType = 0;
	}
	public byte precedence, valCount;
	public SourceLocation location;
	public int startNodeCount;
	public byte parenthType;
	public bool leftToRight;
	public OT operation;
}

// Parses an expression using a modified Shunting-yard algorithm.
// Throws a ParseException when it's not a valid expression.
class ExpressionParser
{
	public ExpressionParser(Scope scope, Token[] tokens, Token.Type terminator, int cursor, List<Node> program, DefineMode defineMode)
	{
		this.startNodeCount = program.Count;
		this.defineMode = defineMode;
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
	
	const byte FUNCTION_CALL = 1, MEMBER_TUPLE = 2, TUPLE = 3;
	
	public int parseExpression()
	{
		if(defineMode != DefineMode.NONE)
			parseDefinition();
		
		// TODO: Make sure you can only define inside of a struct.
		
		defineMode = DefineMode.NONE;
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
		values.Push(new Node(token.location, NT.BASETYPE) { dataType = Lookup.getBaseType(token.type), typeKind = TypeKind.STATIC });
	}
	
	void parseIdentifier()
	{
		if(prevTokenKind == VALUE_KIND) {
			throw Jolly.unexpected(token);
		}
		var symbol = new NodeSymbol(token.location, token.text);
		expression.Add(symbol);
		values.Push(symbol);
	}
	
	void parseDefineIdentifier()
	{
		if(prevTokenKind == VALUE_KIND)
		{
			// Pop eventual period and comma operators
			while(operators.Count > 0)
				pushOperator(operators.Pop());
						
			Node prev = values.Pop();
			Token nextToken = tokens[cursor + 1];
			// Define
			if(nextToken.type == TT.PARENTHESIS_OPEN)
			{ // Function
				if(defineMode != DefineMode.FUNCTION_OR_VARIABLE) {
					throw Jolly.addError(token.location, "Can't define function \"{0}\" here".fill(token.text));
				}
				
				var functionScope = new Scope(scope);
				var functionType = new DataTypeFunction() { name = token.text };
				var functionNode = new NodeFunction(token.location, functionScope, token.text)
					{ dataType = functionType, returns = prev };
				expression.Insert(startNodeCount, functionNode);
				expression.AddRange(values);
				functionNode.returnDefinitionCount = expression.Count - (startNodeCount += 1);
				int _startNodeCount2 = expression.Count;
				
				if(!scope.Add(token.text, null, TypeKind.STATIC)) {
					// TODO: add overloads
					Jolly.addError(token.location, "Trying to redefine function");
				}
				
				cursor = new ExpressionParser(functionScope, tokens, TT.PARENTHESIS_CLOSE, cursor + 2, expression, DefineMode.ARGUMENT)
					.parseExpression();
				
				functionType.arguments = new DataType[functionScope.variableCount];
				functionType.returns = new DataType[(prev as NodeTuple)?.values.Count ?? 1];
				functionNode.argumentDefinitionCount = expression.Count - _startNodeCount2;
				
				Token brace = tokens[cursor + 1];
				if(brace.type != TT.BRACE_OPEN) {
					throw Jolly.unexpected(brace);
				}
				
				new BlockParser(cursor + 2, brace.partnerIndex, functionScope, tokens, expression).parseBlock();
				cursor = brace.partnerIndex - 1;
				
				functionNode.memberCount = expression.Count - startNodeCount;
				
				terminator = TT.BRACE_CLOSE; // HACK: stop parsing 
			}
			else
			{ // Variable
				var variable = new NodeVariableDefinition(token.location, scope, token.text, prev);
				
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
					scope.variableCount += 1;
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
				
				values.Push(variable);
				
				if(defineMode == DefineMode.FUNCTION_OR_VARIABLE)
					defineMode = DefineMode.DITTO;
			}
		}
		else
		{
			Node prev = values.PeekOrDefault();
			if(prev == null || prev.nodeType != NT.VARIABLE_DEFINITION) {
				var symbol = new NodeSymbol(token.location, token.text);
				expression.Add(symbol);
				values.Push(symbol);
			}
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
		
		byte kind = TUPLE;
		if(prevTokenKind == VALUE_KIND) {
			kind = FUNCTION_CALL;
		} else if(prevTokenKind == OPERATOR_KIND && operators.Peek().operation == OT.GET_MEMBER) {
			kind = MEMBER_TUPLE;
			operators.Pop();
		}
		operators.Push(new Op(255, 0, false, OT.PARENTHESIS_OPEN, token.location) {
			parenthType = kind,
			startNodeCount = expression.Count
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
		
		if(op.parenthType == FUNCTION_CALL)
		{
			Node[] arguments;
			Node symbol = values.Pop();
			if(symbol.nodeType != NT.NAME) {
				arguments = (symbol.nodeType == NT.TUPLE) ? ((NodeTuple)symbol).values.ToArray() : new Node[] { symbol };
				symbol = values.Pop(); 
			} else {
				arguments = new Node[0];
			}
			Debug.Assert(symbol.nodeType == NT.NAME);
			
			Node node = new NodeFunctionCall(token.location, ((NodeSymbol)symbol).text, arguments);
			expression.Add(node);
			values.Push(node);
		}
		else
		{
			// Close list so you can't add to it: (a, b), c
			Node prevVal = values.PeekOrDefault();
			if(prevVal?.nodeType == NT.TUPLE)
			{
				var tup = ((NodeTuple)prevVal);
				if(op.parenthType == MEMBER_TUPLE) {
					tup.scopeFrom = values.ElementAt(1);
					tup.nodeType = NT.MEMBER_TUPLE;
				}
				tup.closed = true;
				tup.memberCount = expression.Count - op.startNodeCount;;
				expression.Insert(op.startNodeCount, tup);
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
				NodeTuple tuple = a as NodeTuple;
				if(a.nodeType == NT.TUPLE && !tuple.closed) {
					tuple.values.Add(b);
					values.Push(a);
				}  else {
					tuple = new NodeTuple(_op.location, scope);
					tuple.values.Add(a);
					tuple.values.Add(b);
					values.Push(tuple);
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