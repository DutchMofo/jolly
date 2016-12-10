using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace Jolly
{
using TT = Token.Type;
using NT = Node.NodeType;

struct Op
{
	public TT operation;
	public byte precedence, valCount;
	public bool leftToRight;
	public bool isFunctionCall;
	public SourceLocation location;
}

class ExpressionParser
{
	public ExpressionParser(Scope scope, Token[] tokens, Token.Type terminator, int cursor, int end, SymbolFlag variableFlags) {
		this.variableFlags = variableFlags | SymbolFlag.overwrite_global;
		this.terminator = terminator;
		this.cursor = cursor;
		this.tokens = tokens;
		this.scope = scope;
		this.end = end;
	}
	
	Token token;
	Scope scope;
	Token[] tokens;
	int end, cursor;
	Token.Type terminator;
	SymbolFlag variableFlags;
	
	bool wasOperator, isOperator, defining;
	List<Node> expression = new List<Node>();
	Stack<Node> values = new Stack<Node>();
	Stack<Op> operators = new Stack<Op>();
	
	public bool isFunction;
	public Function theFunction;
	
	public List<Node> getExpression() { return expression; }
	public Node getValue() { return values.PeekOrDefault(); }
	
	void pushOperator(Op _op)
	{
		if(_op.valCount == 0) return;
		
		Node a, b = null;
		// TypeInfo dataType;
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
				_List list = a as _List;
				if(a.nType == NT.LIST) {
					if(!list.locked) {
						list.list.Add(b);
						values.Push(a);
						return;
					} else {
						// Can't add to locked list
						throw new ParseException();
					}
				} else {
					list = new _List(_op.location, scope);
					list.list.Add(a);
					list.list.Add(b);
					values.Push(list);
					return;
				}
			} 
			// dataType = b.dType;
		}
		else
		{
			a = values.PopOrDefault();
			if(a == null) {
				Jolly.addError(_op.location, "Invalid expression term");
				throw new ParseException();
			}
			// dataType = a.dType;
		}
		
		Operator op = new Operator(_op.location, scope, _op.operation, a, b, new Result(_op.location, scope) { /*dType = dataType*/ });
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
		
		// var refs = parseTypeReferences();
		// TypeInfo baseT = Jolly.baseTypes[token.type - TT.I8];
		values.Push(new Node(NT.BASETYPE, token.location, scope){ /*dType = TypeInfo.getTypeInfo(baseT, refs)*/ });
		return true; 
	}
	
	bool parseLiteral()
	{
		if(token.type < TT.STRING_LITERAL | token.type > TT.FLOAT_LITERAL)
			return false;
					
		Literal lit;
		if(token.type == TT.INTEGER_LITERAL) {
			lit = new Literal(token.location, scope, token._integer){ /*dType = Jolly.baseTypes[TT.I32 - TT.I8]*/ };
		} else if(token.type == TT.FLOAT_LITERAL) {
			lit = new Literal(token.location, scope, token._float){ /*dType = Jolly.baseTypes[TT.F32 - TT.I8]*/ };
		} else {
			lit = new Literal(token.location, scope, token._string){ /*dType = Jolly.baseTypes[TT.STRING - TT.I8]*/ };
		}
		
		values.Push(lit);
		return true;
	}
	
	bool parseIdentifier()
	{
		if(token.type != TT.IDENTIFIER)
			return false;
		
		values.Push(new Symbol(NT.NAME, token.location, scope, token.name, SymbolFlag.none));
		return true;
	}
	
	bool parseDefineIdentifier()
	{
		if(token.type != TT.IDENTIFIER)
			return false;
		
		Node prev = values.PeekOrDefault();
		if(prev != null && !wasOperator & (prev.nType == NT.NAME | prev.nType == NT.BASETYPE))
		{
			if(prev.nType == NT.NAME) {
				// sigh... c#
				Node t = values.Pop();
				t.nType = NT.USERTYPE;
				values.Push(t);
			}
			
			// Define
			Token next = tokens[cursor+1];
			if(next.type == TT.PARENTHESIS_OPEN)
			{ // Function
				while(operators.Count > 0)
					pushOperator(operators.Pop());
				
				var function = new Function(token.location, scope, token.name, null, SymbolFlag.private_members/*, returns*/);
				function.returns = values.Pop();
				
				if(!scope.addSymbol(function)) {
					Jolly.addError(token.location, "Trying to redefine function");
					throw new ParseException();
				}
				
				var parser = new ExpressionParser(function, tokens, TT.PARENTHESIS_CLOSE, cursor+2, next.partner.index, variableFlags);
				cursor = parser.parseExpression(true)-1;
				function.arguments = function.symbols.Select(s => (Variable)s).ToArray();
				
				terminator = TT.PARENTHESIS_CLOSE; // HACK: stop parsing 
				theFunction = function;
				isFunction = true;
			}
			else
			{ // Variable
				defining = true;
				var variable = new Variable(token.location, scope, token.name, values.Pop(), variableFlags); 
				if(!scope.addSymbol(variable)) {
					Jolly.addError(token.location, "Trying to redefine variable");
					throw new ParseException();
				}
				values.Push(variable);
			}
		}
		else if(prev?.nType != NT.VARIABLE)
			values.Push(new Symbol(NT.NAME,token.location, scope, token.name, SymbolFlag.none));
		
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
				values.Push(new Literal(token.location, scope, 0));
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
			isFunctionCall = values.PeekOrDefault()?.nType == NT.NAME,
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
			Node name = values.Pop();
			if(name.nType != NT.NAME) {
				arguments = name.nType == NT.LIST ? ((_List)name).list.ToArray() : new Node[] { name };
				name = values.Pop(); 
			} else {
				arguments = new Node[0];
			}
			Debug.Assert(name.nType == NT.NAME);
			
			values.Push(new Result(token.location, scope/*, null*/));
			expression.Add(new Function_call(token.location, scope, ((Symbol)name).name, arguments));
		} else {
			Node list = values.PeekOrDefault();
			if(list?.nType == NT.LIST)
				((_List)list).locked = true;
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
			
			var variable = new Variable(name.location, scope, name.name, n, variableFlags); 
			if(!scope.addSymbol(variable)) {
				Jolly.addError(name.location, "Trying to redefine variable");
				throw new ParseException();
			}
			values.Push(variable);
		}
		
		return false;
	}
	
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
	
	public int parseExpression(bool allowDefine)
	{
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