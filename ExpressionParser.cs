using System.Collections.Generic;
using System.Diagnostics;

namespace Jolly
{
using TT = Token.Type;
using NT = Node.NodeType;

struct Op
{
	public TT operation;
	public bool leftToRight;
	public bool isFunctionCall;
	public SourceLocation location;
	public byte precedence, valCount;
}

// Parses an expression using a modified Shunting-yard algorithm.
// Throws a ParseException when it's not a valid expression.
class ExpressionParser
{
	public ExpressionParser(TableFolder scope, Token[] tokens, Token.Type terminator, int cursor, int end) {
		this.terminator = terminator;
		this.cursor = cursor;
		this.tokens = tokens;
		this.scope = scope;
		this.end = end;
	}
	
	Token token;
	TableFolder scope;
	ScopeParser scopeParser;
	Token[] tokens;
	int end, cursor;
	Token.Type terminator;
	
	bool wasOperator,	// Was the previous parsed token an operator
		isOperator,		// Is the current token being parsed an operator
		defining;		// Are we defining variables
	
	// TODO: check if the intermediate "expression" list is nessesary,
	// maybe can the node's be directly appended to the scope parsers "program" list
	List<Node> expression = new List<Node>();
	Stack<Node> values = new Stack<Node>();
	Stack<Op> operators = new Stack<Op>();
	
	public bool isFunction;
	public TableFolder theFunction;
	
	public List<Node> getExpression() { return expression; }
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
				throw new ParseException();
			}
		
			if(_op.operation == TT.COMMA)
			{
				Tupple list = a as Tupple;
				if(a.nodeType == NT.TUPPLE) {
					if(!list.locked) {
						list.list.Add(b);
						values.Push(a);
						return;
					} else {
						// Can't add to locked list
						throw new ParseException();
					}
				} else {
					list = new Tupple(_op.location);
					list.list.Add(a);
					list.list.Add(b);
					values.Push(list);
					return;
				}
			}
		}
		else
		{
			a = values.PopOrDefault();
			if(a == null) {
				Jolly.addError(_op.location, "Invalid expression term");
				throw new ParseException();
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
		
		if((!wasOperator && values.Count != 0) || operators.Count > 0 && operators.Peek().operation != TT.COMMA) {
			Jolly.unexpected(token);
			throw new ParseException();
		}
		
		values.Push(new BaseType(token.location, token.type));
		return true; 
	}
	
	bool parseLiteral()
	{
		if(token.type < TT.STRING_LITERAL | token.type > TT.FLOAT_LITERAL)
			return false;
					
		Literal lit;
		if(token.type == TT.INTEGER_LITERAL) {
			lit = new Literal(token.location, token._integer);
		} else if(token.type == TT.FLOAT_LITERAL) {
			lit = new Literal(token.location, token._float);
		} else {
			lit = new Literal(token.location, token._string);
		}
		
		values.Push(lit);
		return true;
	}
	
	bool parseIdentifier()
	{
		if(token.type != TT.IDENTIFIER)
			return false;
		
		values.Push(new Symbol(token.location, token.name));
		return true;
	}
	
	bool parseDefineIdentifier()
	{
		if(token.type != TT.IDENTIFIER)
			return false;
		
		Node prev = values.PeekOrDefault();
		if(prev != null && !wasOperator & (prev.nodeType == NT.NAME | prev.nodeType == NT.BASETYPE))
		{
			// Define
			Token next = tokens[cursor+1];
			if(next.type == TT.PARENTHESIS_OPEN)
			{ // Function
			
				// Pop eventual period and comma operators
				while(operators.Count > 0)
					pushOperator(operators.Pop());
				
				var function = new Function(token.location, null, scope);
				function.returns = values.Pop();
				
				TableFolder functionScope = theFunction = new TableFolder();
				
				if(!scope.addChild(token.name, functionScope)) {
					// TODO: add overloads
					Jolly.addError(token.location, "Trying to redefine function");
					throw new ParseException();	
				}
				
				var parser = new ExpressionParser(functionScope, tokens, TT.PARENTHESIS_CLOSE, cursor+2, next.partnerIndex);
				cursor = parser.parseExpression(scopeParser, true)-1;
				
				terminator = TT.PARENTHESIS_CLOSE; // HACK: stop parsing 
				isFunction = true;
			}
			else
			{ // Variable
				defining = true;
				var variable = new Symbol(token.location, token.name);
				TableItem variableItem = new TableItem(prev.dataType);
				
				if(!scope.addChild(token.name, variableItem)) {
					Jolly.addError(token.location, "Trying to redefine variable");
					throw new ParseException();
				}
				values.Push(variable);
			}
		}
		else if(prev?.nodeType != NT.VARIABLE)
			values.Push(new Symbol(token.location, token.name));
		
		return true;
	}
	
	bool parseOperator(Dictionary<TT, Op> lookup, Dictionary<TT, Op> preLookup)
	{
		Op op;
		if(!lookup.TryGetValue(token.type, out op))
			return false;
		isOperator = true;
		
		if(wasOperator || values.Count == 0)
		{
			if(token.type == TT.PLUS || token.type == TT.MINUS) {
				// unary plus and minus 
				values.Push(new Literal(token.location, 0));
			} else if(!preLookup.TryGetValue(token.type, out op)) {
				Jolly.unexpected(token);
				throw new ParseException();
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
	
	bool parseBracketOpen()
	{
		if(token.type != TT.BRACKET_OPEN)
			return false;
		isOperator = true;
		operators.Push(new Op {
			operation = TT.BRACKET_OPEN,
			location = token.location,
			leftToRight = false,
			precedence = 255,
			valCount = 0,
		});
		return true;
	}
	
	bool parseParenthesisOpen()
	{
		if(token.type != TT.PARENTHESIS_OPEN)
			return false;
		isOperator = true;
		operators.Push(new Op {
			operation = TT.PARENTHESIS_OPEN,
			isFunctionCall = values.PeekOrDefault()?.nodeType == NT.NAME,
			leftToRight = false,
			location = token.location,
			precedence = 255,
			valCount = 0,
		});
		return true;
	}
	
	bool parseBracketClose()
	{
		if(token.type != TT.BRACKET_CLOSE)
			return false;
		isOperator = true;
		
		Op op;
		while((op = operators.PopOrDefault()).operation != TT.BRACKET_OPEN)
			pushOperator(op);
			
		if(op.operation == TT.UNDEFINED) {
			Jolly.unexpected(new Token { type = TT.BRACKET_CLOSE, location = op.location });
			throw new ParseException();
		}
		
		return true;
	}
	
	bool parseParenthesisClose()
	{
		if(token.type != TT.PARENTHESIS_CLOSE)
			return false;
		
		Op op;
		while((op = operators.PopOrDefault()).operation != TT.PARENTHESIS_OPEN)
			pushOperator(op);
		
		if(op.operation == TT.UNDEFINED) {
			Jolly.unexpected(new Token { type = TT.PARENTHESIS_CLOSE, location = op.location });
			throw new ParseException();
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
				((Tupple)list).locked = true;
		}
		
		return true;
	}
	
	bool parseComma()
	{
		if(token.type != TT.COMMA)
			return false;
		
		if(wasOperator) {
			Jolly.unexpected(token);
			throw new ParseException();
		}
		
		while(operators.Count > 0)
			pushOperator(operators.Pop());
		
		if(defining)
		{
			Node n = values.Peek();
			Token name = tokens[cursor+1];
			wasOperator = isOperator = false;
			
			if(name.type != TT.IDENTIFIER) {
				Jolly.unexpected(name);
				throw new ParseException();
			}
			
			var variable = new Symbol(name.location, name.name); 
			TableItem variableItem = new TableItem(null);
			
			if(!scope.addChild(name.name, variableItem)) {
				Jolly.addError(name.location, "Trying to redefine variable");
				throw new ParseException();
			}
			values.Push(variable);
		}
		
		// Return false to let parseOperator handle other work
		return false;
	}
	
	// Tries to define a function or variable,
	// stops when it defines a function or it encounters an unknown token
	void parseDefinition()
	{
		for(token = tokens[cursor];
			token.type != terminator & cursor < end;
			token = tokens[++cursor])
		{
			if( parseBasetype()			||
				parseComma()			||
				parseDefineIdentifier()	||
				parseOperator(Lookup.DEFINE_OP, Lookup.DEFINE_PRE_OP))
			{
				wasOperator = isOperator;
				isOperator = false;
				continue;
			}
			return;
		}
	}
	
	public int parseExpression(ScopeParser scopeParser, bool allowDefine)
	{
		this.scopeParser = scopeParser;
		
		if(allowDefine)
			parseDefinition();
		
		for(token = tokens[cursor];
			token.type != terminator & cursor < end;
			token = tokens[++cursor])
		{
			if(	parseLiteral()			||
				parseBasetype()			||
				parseIdentifier()		||
				parseComma()			||
				parseOperator(Lookup.EXPRESSION_OP, Lookup.EXPRESSION_PRE_OP) ||
				parseBracketOpen()		||
				parseBracketClose()		||
				parseParenthesisOpen()	||
				parseParenthesisClose())
			{
				wasOperator = isOperator;
				isOperator = false;
				continue;
			}
			
			// Failed to parse token
			Jolly.unexpected(token);
			throw new System.Exception();
		}
		
		if(token.type != terminator) {
			Jolly.unexpected(token);
			throw new System.Exception();
		}
		
		while(operators.Count > 0)
			pushOperator(operators.Pop());
		
		return cursor;
	} // parseExpression
}

}