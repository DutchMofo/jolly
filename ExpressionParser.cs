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

enum DefineMode
{
	NONE,
	ARGUMENT,
	MEMBER_OR_VARIABLE,
}
	
struct Op
{
	public Op(byte precedence, byte valCount, bool leftToRight, OT operation, SourceLocation location = new SourceLocation())
	{
		this.leftToRight = leftToRight;
		this.precedence = precedence;
		this.isFunctionCall = false;
		this.operation = operation;
		this.location = location;
		this.valCount = valCount;
	}
	public byte precedence, valCount;
	public SourceLocation location;
	public bool isFunctionCall;
	public bool leftToRight;
	public OT operation;
}

// Parses an expression using a modified Shunting-yard algorithm.
// Throws a ParseException when it's not a valid expression.
class ExpressionParser
{
	public ExpressionParser(TableFolder scope, Token[] tokens, Token.Type terminator, int cursor, int end, List<Node> program)
	{
		startNodeCount = program.Count;
		this.terminator = terminator;
		this.expression = program;
		this.cursor = cursor;
		this.tokens = tokens;
		this.scope = scope;
		this.end = end;
	}
	
	const byte VALUE_KIND = 1, OPERATOR_KIND = 2, SEPARATOR_KIND = 3;

	byte prevTokenKind = 0,				// Was the previous parsed token an operator or a value
		 currentTokenKind = VALUE_KIND;	// Is the current token being parsed an operator or a value
	DefineMode defineMode;				// Are we defining variables
	
	Dictionary<TT, Op> opLookup, preOpLookup;
	int end, cursor, startNodeCount;
	TableFolder scope;
	Token[] tokens;
	TT terminator;
	Token token;

	List<Node> expression;// = new List<Node>();
	Stack<Node> values = new Stack<Node>();
	Stack<Op> operators = new Stack<Op>();
	
	public TableFolder theFunction;
	public bool isFunction;
	
	public Node getValue()
		=> values.PeekOrDefault();
	
	public int parseExpression(DefineMode defineMode)
	{
		this.defineMode = defineMode;
		if(this.defineMode != DefineMode.NONE)
			parseDefinition();
		
		this.defineMode = DefineMode.NONE;
		opLookup = Lookup.EXPRESSION_OP;
		preOpLookup = Lookup.EXPRESSION_PRE_OP;
		
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
		preOpLookup = Lookup.DEFINE_PRE_OP;
		
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
		Literal lit;
		if(token.type == TT.INTEGER_LITERAL) {
			lit = new Literal(token.location, token._integer);
			lit.dataType = Lookup.getBaseType(TT.I32); // TODO: Temporary
		} else if(token.type == TT.FLOAT_LITERAL) {
			lit = new Literal(token.location, token._float);
			lit.dataType = Lookup.getBaseType(TT.F32); // TODO: Temporary
		} else {
			lit = new Literal(token.location, token._string);
			lit.dataType = Lookup.getBaseType(TT.STRING);
		}
		values.Push(lit);
	} // parseLiteral()
	
	void parseBasetype()
	{
		if(prevTokenKind == VALUE_KIND) {
			throw Jolly.unexpected(token);
		}
		values.Push(new BaseType(token.location, Lookup.getBaseType(token.type)));
	}
	
	void parseIdentifier()
	{
		if(prevTokenKind == VALUE_KIND) {
			throw Jolly.unexpected(token);
		}
		values.Push(new Symbol(token.location, token.name, scope));
	}
	
	void parseDefineIdentifier()
	{
		Node prev = values.PeekOrDefault();
		if(prevTokenKind == VALUE_KIND/* && (prev.nodeType == NT.NAME | prev.nodeType == NT.BASETYPE)*/)
		{
			// Pop eventual period and comma operators
			while(operators.Count > 0)
				pushOperator(operators.Pop());
			
			// Define
			Token nextToken = tokens[cursor + 1];
			if(nextToken.type == TT.PARENTHESIS_OPEN)
			{ // Function
				if(defineMode == DefineMode.ARGUMENT) {
					throw Jolly.addError(token.location, "Trying to define function \"{0}\" as argument".fill(token.name));
				}
				
				values.Push(new Symbol(token.location, token.name, scope, NT.FUNCTION));
				
				expression.Add(new Symbol(token.location, token.name, scope, NT.FUNCTION));
				theFunction = new TableFolder();
				theFunction.type = new DataType(theFunction) { name = token.name };
				
				if(!scope.Add(token.name, theFunction)) {
					// TODO: add overloads
					Jolly.addError(token.location, "Trying to redefine function");
				}
				
				var parser = new ExpressionParser(theFunction, tokens, TT.PARENTHESIS_CLOSE, cursor + 2, nextToken.partnerIndex, expression);
				cursor = parser.parseExpression(DefineMode.ARGUMENT)-1;
				
				
				terminator = TT.PARENTHESIS_CLOSE; // HACK: stop parsing 
				isFunction = true;
			}
			else
			{ // Variable
				TableItem variableItem = new TableItem(prev.dataType);
				
				if(!scope.Add(token.name, variableItem)) {
					Jolly.addError(token.location, "Trying to redefine variable");
				}
				var variable = new Symbol(token.location, token.name, scope, NT.VARIABLE_DEFINITION);
				variable.childNodeCount = expression.Count - startNodeCount;
				
				if(variable.childNodeCount == 0) {
					variable.childNodeCount = 1;
					expression.Add(variable);
					expression.Add(prev);
				} else {
					expression.Insert(startNodeCount, variable);
				}
				values.Push(variable);
			}
		}
		else if(prev == null || prev.nodeType != NT.VARIABLE_DEFINITION)
			values.Push(new Symbol(token.location, token.name, scope));
	} // parseDefineIdentifier()
	
	void parseComma()
	{		
		if(prevTokenKind != VALUE_KIND) {
			throw Jolly.unexpected(token);
		}
		
		while(operators.Count > 0)
			pushOperator(operators.Pop());
		
		// TODO: This probably has other side-effects but it works for now
		if(defineMode != DefineMode.NONE)
		{
			if(defineMode == DefineMode.ARGUMENT)
			{
				cursor += 1;
				parseDefinition();
			}
			else
			{
				// TODO: implement define after comma: int a, b; so that it copies the datatype of the first
				//											  ^
				throw new ParseException();
			}
			
			// Token name = tokens[cursor + 1];
			// prevTokenKind = currentTokenKind = VALUE_KIND;
			
			// if(name.type != TT.IDENTIFIER) {
			// 	throw Jolly.unexpected(name);
			// }
			
			// var variable = new Symbol(name.location, name.name, scope); 
			// TableItem variableItem = new TableItem(null);
			
			// if(!scope.Add(name.name, variableItem)) {
			// 	Jolly.addError(name.location, "Trying to redefine variable");
			// }
			// values.Push(variable);
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
			if(token.type == TT.PLUS || token.type == TT.MINUS) {
				// unary plus and minus
				values.Push(new Literal(token.location, 0));
			} else if(!preOpLookup.TryGetValue(token.type, out op)) {
				throw Jolly.unexpected(token);
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
		// TODO: Should this be a operator kind?
		currentTokenKind = OPERATOR_KIND;
		
		Op op;
		while((op = operators.PopOrDefault()).operation != OT.BRACKET_OPEN) {
			if(op.operation == OT.UNDEFINED) {
				throw Jolly.unexpected(new Token { type = TT.BRACKET_CLOSE, location = op.location });
			}
			pushOperator(op);
		}
	}
	
	void parseParenthesisOpen()
	{
		currentTokenKind = OPERATOR_KIND;
		operators.Push(new Op(255, 0, false, OT.PARENTHESIS_OPEN, token.location) {
			isFunctionCall = (prevTokenKind == VALUE_KIND),
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
		
		if(op.isFunctionCall)
		{
			Node[] arguments;
			Node symbol = values.Pop();
			if(symbol.nodeType != NT.NAME) {
				arguments = (symbol.nodeType == NT.TUPPLE) ? ((Tupple)symbol).values.ToArray() : new Node[] { symbol };
				symbol = values.Pop(); 
			} else {
				arguments = new Node[0];
			}
			Debug.Assert(symbol.nodeType == NT.NAME);
			
			values.Push(new Result(token.location));
			expression.Add(new Function_call(token.location, ((Symbol)symbol).name, arguments));
		} else {
			// Close list so you can't add to it: (a, b), c
			Node list = values.PeekOrDefault();
			if(list?.nodeType == NT.TUPPLE)
				((Tupple)list).closed = true;
		}
	} // parseParenthesisClose()
	
	void parseTypeToReference()
	{
		if(prevTokenKind == OPERATOR_KIND | prevTokenKind == 0) {
			throw Jolly.unexpected(token);
		}
		currentTokenKind = VALUE_KIND;
		values.Push(new TypeToReference(token.location, values.Pop(), ReferenceType.POINTER));
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
				Tupple list = a as Tupple;
				if(a.nodeType == NT.TUPPLE && !list.closed) {
					list.values.Add(b);
					values.Push(a);
				} else {
					list = new Tupple(_op.location);
					list.values.Add(a);
					list.values.Add(b);
					values.Push(list);
				}
				return;
			}
		}
		else
		{
			a = values.PopOrDefault();
			if(a == null) {
				throw Jolly.addError(_op.location, "Invalid expression term");
			}
		}
		Operator op = new Operator(_op.location, _op.operation, a, b, new Result(_op.location));
		values.Push(op.result);
		expression.Add(op);
	} // pushOperator()
}

}