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
	AND_EQUAL,
	OR_EQUAL,
	ASTERISK_EQUAL,
	MINUS_EQUAL,
	PLUS_EQUAL,
	SLASH_EQUAL,
	PERCENT_EQUAL,
	CARET_EQUAL,
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
	lambda_thing,		// =>
	
	BRACKET_OPEN,
	BRACKET_CLOSE,
	PARENTHESIS_OPEN,
	PARENTHESIS_CLOSE,
	COMMA,
};

struct Op
{
	public OT operation;
	public bool leftToRight;
	public bool isFunctionCall;
	public SourceLocation location;
	public byte precedence, valCount;
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
	
	Token token;
	Token[] tokens;
	int end, cursor, startNodeCount;
	TableFolder scope;
	ScopeParser scopeParser;
	Token.Type terminator;
	
	Dictionary<TT, Op> opLookup, preOpLookup;
	
	const byte VALUE_KIND = 1, OPERATOR_KIND = 2, SEPARATOR_KIND = 3;

	byte prevTokenKind = 0,		// Was the previous parsed token an operator or a value
		 currentTokenKind = 0;	// Is the current token being parsed an operator or a value
	bool defining;				// Are we defining variables
	
	List<Node> expression;// = new List<Node>();
	Stack<Node> values = new Stack<Node>();
	Stack<Op> operators = new Stack<Op>();
	
	public bool isFunction;
	public TableFolder theFunction;
	
	public Node getValue() { return values.PeekOrDefault(); }
	
	void pushOperator(Op _op)
	{
		Node a, b = null;
		
		if(_op.valCount == 2)
		{
			b = values.PopOrDefault();
			a = values.PopOrDefault();
			
			if(a == null || b == null) {
				Jolly.addError(_op.location, "Invalid {0} expression term".fill(a==null?"left":"right"));
			}
		
			if(_op.operation == OT.COMMA)
			{
				Tupple list = a as Tupple;
				if(a.nodeType == NT.TUPPLE && !list.closed) {
					list.list.Add(b);
					values.Push(a);
				} else {
					list = new Tupple(_op.location);
					list.list.Add(a);
					list.list.Add(b);
					values.Push(list);
				}
				return;
			}
		}
		else
		{
			a = values.PopOrDefault();
			if(a == null) {
				Jolly.addError(_op.location, "Invalid expression term");
			}
		}
		
		Operator op = new Operator(_op.location, _op.operation, a, b, new Result(_op.location));
		values.Push(op.result);
		expression.Add(op);
	}
	
	bool parseBasetype()
	{
		if(token.type < TT.I8 | token.type > TT.AUTO)
			return false;
		
		if(prevTokenKind == VALUE_KIND) {
			throw Jolly.unexpected(token);
		}
		
		values.Push(new BaseType(token.location, Lookup.getBaseType(token.type)));
		return true; 
	}
	
	bool parseLiteral()
	{
		if(token.type < TT.STRING_LITERAL | token.type > TT.FLOAT_LITERAL)
			return false;
		
		if(prevTokenKind == VALUE_KIND) {
			throw Jolly.unexpected(token);
		}
			
		Literal lit;
		if(token.type == TT.INTEGER_LITERAL) {
			lit = new Literal(token.location, token._integer);
			lit.dataType = Lookup.getBaseType(TT.I32);
		} else if(token.type == TT.FLOAT_LITERAL) {
			lit = new Literal(token.location, token._float);
			lit.dataType = Lookup.getBaseType(TT.F32);
		} else {
			lit = new Literal(token.location, token._string);
			lit.dataType = Lookup.getBaseType(TT.STRING);
		}
		
		values.Push(lit);
		return true;
	}
	
	bool parseIdentifier()
	{
		if(token.type != TT.IDENTIFIER)
			return false;
		
		if(prevTokenKind == VALUE_KIND) {
			throw Jolly.unexpected(token);
		}
		
		values.Push(new Symbol(token.location, token.name, scope));
		return true;
	}
	
	bool parseDefineIdentifier()
	{
		if(token.type != TT.IDENTIFIER)
			return false;
				
		Node prev = values.PeekOrDefault();
		if(prevTokenKind == VALUE_KIND && (prev.nodeType == NT.NAME | prev.nodeType == NT.BASETYPE))
		{
			// Pop eventual period and comma operators
			while(operators.Count > 0)
				pushOperator(operators.Pop());
			
			// Define
			Token next = tokens[cursor + 1];
			if(next.type == TT.PARENTHESIS_OPEN)
			{ // Function
				var function = new Function(token.location, null, scope);
				function.returns = values.Pop();
				
				values.Push(new Symbol(token.location, token.name, scope, NT.FUNCTION));
				theFunction = new TableFolder(){ type = prev.dataType };
				
				if(!scope.Add(token.name, theFunction)) {
					// TODO: add overloads
					Jolly.addError(token.location, "Trying to redefine function");
				}
				
				var parser = new ExpressionParser(theFunction, tokens, TT.PARENTHESIS_CLOSE, cursor + 2, next.partnerIndex, expression);
				cursor = parser.parseExpression(scopeParser, true)-1;
				
				terminator = TT.PARENTHESIS_CLOSE; // HACK: stop parsing 
				isFunction = true;
			}
			else
			{ // Variable
				defining = true;
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
		
		return true;
	}
	
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
			while(prevOp.valCount > 0 && (prevOp.precedence < op.precedence || op.leftToRight && prevOp.precedence == op.precedence))
			{
				pushOperator(operators.Pop());
				if(operators.Count == 0) break;
				prevOp = operators.Peek();
			}
		}
		op.location = token.location;
		operators.Push(op);		
		return true;
	}
	
	bool parseBracket()
	{
		if(token.type == TT.BRACKET_OPEN)
		{
			currentTokenKind = OPERATOR_KIND;
			operators.Push(new Op {
				operation = OT.BRACKET_OPEN,
				location = token.location,
				leftToRight = false,
				precedence = 255,
				valCount = 0,
			});
			return true;
		}
		else if(token.type == TT.BRACKET_CLOSE)
		{
			currentTokenKind = OPERATOR_KIND;
			
			Op op;
			while((op = operators.PopOrDefault()).operation != OT.BRACKET_OPEN)
				pushOperator(op);
				
			if(op.operation == OT.UNDEFINED) {
				throw Jolly.unexpected(new Token { type = TT.BRACKET_CLOSE, location = op.location });
			}
			
			return true;
		}
		return false;
	}
	
	
	bool parseParenthesis()
	{
		if(token.type == TT.PARENTHESIS_OPEN)
		{
			currentTokenKind = OPERATOR_KIND;
			operators.Push(new Op {
				operation = OT.PARENTHESIS_OPEN,
				isFunctionCall = values.PeekOrDefault()?.nodeType == NT.NAME,
				leftToRight = false,
				location = token.location,
				precedence = 255,
				valCount = 0,
			});
			return true;
		}
		else if(token.type == TT.PARENTHESIS_CLOSE)
		{
			Op op;
			while((op = operators.PopOrDefault()).operation != OT.PARENTHESIS_OPEN)
				pushOperator(op);
			
			if(op.operation == OT.UNDEFINED) {
				throw Jolly.unexpected(new Token { type = TT.PARENTHESIS_CLOSE, location = op.location });
			}
			
			if(op.isFunctionCall)
			{
				Node[] arguments;
				Node symbol = values.Pop();
				if(symbol.nodeType != NT.NAME) {
					arguments = (symbol.nodeType == NT.TUPPLE) ? ((Tupple)symbol).list.ToArray() : new Node[] { symbol };
					symbol = values.Pop(); 
				} else {
					arguments = new Node[0];
				}
				Debug.Assert(symbol.nodeType == NT.NAME);
				
				values.Push(new Result(token.location));
				expression.Add(new Function_call(token.location, ((Symbol)symbol).name, arguments));
			} else {
				Node list = values.PeekOrDefault();
				if(list?.nodeType == NT.TUPPLE)
					((Tupple)list).closed = true;
			}
			return true;
		}
		return false;
	}
	
	bool parseComma()
	{
		if(token.type != TT.COMMA)
			return false;
				
		if(prevTokenKind == OPERATOR_KIND) {
			throw Jolly.unexpected(token);
		}
		
		while(operators.Count > 0)
			pushOperator(operators.Pop());
		
		// TODO: This probably has other side-effects but it works for now
		if(defining)
		{
			Token name = tokens[cursor + 1];
			prevTokenKind = currentTokenKind = VALUE_KIND;
			
			if(name.type != TT.IDENTIFIER) {
				throw Jolly.unexpected(name);
			}
			
			var variable = new Symbol(name.location, name.name, scope); 
			TableItem variableItem = new TableItem(null);
			
			if(!scope.Add(name.name, variableItem)) {
				Jolly.addError(name.location, "Trying to redefine variable");
			}
			values.Push(variable);
		}
		
		parseOperator();
		currentTokenKind = SEPARATOR_KIND;
		return true;
	}
	
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
			if( parseBasetype()			||
				parseComma()			||
				parseDefineIdentifier()	||
				parseOperator())
			{
				prevTokenKind = currentTokenKind;
				currentTokenKind = VALUE_KIND;
				continue;
			}
			return;
		}
	}
	
	public int parseExpression(ScopeParser scopeParser, bool allowDefine)
	{
		this.scopeParser = scopeParser;
		currentTokenKind = VALUE_KIND;
		
		if(allowDefine)
			parseDefinition();
		
		opLookup = Lookup.EXPRESSION_OP;
		preOpLookup = Lookup.EXPRESSION_PRE_OP;
		
		for(token = tokens[cursor];
			token.type != terminator/* & cursor < end*/;
			token = tokens[cursor += 1])
		{
			if(	parseLiteral()		||
				parseBasetype()		||
				parseIdentifier()	||
				parseComma()		||
				parseOperator()		||
				parseBracket()		||
				parseParenthesis())
			{
				prevTokenKind = currentTokenKind;
				currentTokenKind = VALUE_KIND;
				continue;
			}
			
			// Failed to parse token
			throw Jolly.unexpected(token);
		}
		
		if(token.type != terminator) {
			throw Jolly.unexpected(token);
		}
		
		while(operators.Count > 0)
			pushOperator(operators.Pop());
		
		return cursor;
	} // parseExpression
}

}