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
	BITCAST,
	LESS,
	GREATER,
	NEW,
	DELETE,
	
	TERNARY,
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

// Parses an expression using a modified Shunting-yard algorithm.
// Throws a ParseException when it's not a valid expression.
class ExpressionParser
{
	public ExpressionParser(Scope scope, Token[] tokens, Token.Type terminator, int cursor, List<Node> program, DefineMode defineMode)
	{
		enclosureStack.Push(new Enclosure(EnclosureKind.NONE, program.Count));
		this.defineMode = defineMode;
		this.terminator = terminator;
		this.expression = program;
		this.cursor = cursor;
		this.tokens = tokens;
		this.scope = scope;
	}
	
	public enum DefineMode : byte
	{
		NONE,
		MEMBER,
		ARGUMENT,
		FUNCTION_OR_VARIABLE,
		DITTO,
	}
	
	enum TokenKind : byte
	{
		VALUE = 1,
		OPERATOR = 2,
		SEPARATOR = 3,
	}
	
	public enum EnclosureKind : byte
	{
		NONE          = 0,
		PARENTHS      = 1,
		FUNCTION_CALL = 2,
		LOGIC_OR      = 3,
		LOGIC_AND     = 4,
		TERNARY       = 5,
		NULLCOALESCE  = 6,
		SUBSCRIPT     = 7,
	}
	
	public struct Enclosure
	{
		public Enclosure(EnclosureKind k, int sNC)
			{ kind = k; startNodeCount = sNC; node = null; }
		public EnclosureKind kind;
		public int startNodeCount;
		public Node node;
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
			this.startNodeCount = 0;
		}
		public byte precedence, valCount;
		public SourceLocation location;
		public bool leftToRight, isSpecial;
		public int startNodeCount;
		public OT operation;
	}

	TokenKind prevTokenKind = 0,					// Was the previous parsed token an operator or a value
			  currentTokenKind = TokenKind.VALUE;	// Is the current token being parsed an operator or a value
	DefineMode defineMode;							// Are we defining variables
	
	Dictionary<TT, Op> opLookup;
	Token[] tokens;
	TT terminator;
	Token token;
	Scope scope;
	int cursor;

	List<Node> expression;// = new List<Node>();
	Stack<Node> values = new Stack<Node>();
	Stack<Op> operators = new Stack<Op>();
	Stack<ExpressionParser.Enclosure> enclosureStack = new Stack<Enclosure>();
	
	public int parseExpression()
	{
		if(defineMode != DefineMode.NONE) {
			parseDefinition();
		}
		
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
			currentTokenKind = TokenKind.VALUE;
		}
		
		// while(enclosureStack.Count > 1) {
		// 	enclosureEnd(enclosureStack.Pop(), 0, null);
		// }
		
		while(operators.Count > 0) {
			pushOperator(operators.Pop());
		}
		
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
			currentTokenKind = TokenKind.VALUE;
		}
	} // parseDefinition()
	
	void parseLiteral()
	{
		if(prevTokenKind == TokenKind.VALUE) {
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
		if(prevTokenKind == TokenKind.VALUE) {
			throw Jolly.unexpected(token);
		}
		values.Push(new Node(token.location, NT.BASETYPE) { dataType = Lookup.getBaseType(token.type), typeKind = TypeKind.STATIC });
	}
	
	void parseIdentifier()
	{
		if(prevTokenKind == TokenKind.VALUE) {
			throw Jolly.unexpected(token);
		}
		var symbol = new NodeSymbol(token.location, token.text);
		values.Push(symbol);
	}
	
	void parseDefineIdentifier()
	{
		if(prevTokenKind == TokenKind.VALUE)
		{
			// Pop eventual period and comma operators
			while(operators.Count > 0) {
				pushOperator(operators.Pop());
			}
						
			Node prev = values.Pop();
			Token nextToken = tokens[cursor + 1];
			int startNodeCount = enclosureStack.Peek().startNodeCount;
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
				expression.Add(prev);
				functionNode.returnDefinitionCount = expression.Count - (startNodeCount += 1);
				int _startNodeCount2 = expression.Count;
				
				if(!scope.Add(token.text, null, TypeKind.STATIC_FUNCTION)) {
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
				values.Push(symbol);
			}
		}
	} // parseDefineIdentifier()
	
	void parseComma()
	{		
		if(prevTokenKind != TokenKind.VALUE) {
			throw Jolly.unexpected(token);
		}
		
		while(operators.Count > 0 && operators.Peek().valCount > 0) {
			pushOperator(operators.Pop());
		}
		
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
			currentTokenKind = TokenKind.SEPARATOR;	
		}
	} // parseComma()
	
	// Returns true if it parsed the token
	bool parseOperator()
	{
		Op op;
		if(!opLookup.TryGetValue(token.type, out op))
			return false;
		currentTokenKind = TokenKind.OPERATOR;
		
		if(prevTokenKind != TokenKind.VALUE)
		{
			switch(token.type) {
				case TT.ASTERISK: op = new Op(02, 1, false, OT.DEREFERENCE); break;
				case TT.AND:	  op = new Op(02, 1, false, OT.REFERENCE  ); break;
				case TT.PLUS: case TT.MINUS: values.Push(new NodeLiteral(token.location, 0)); break;
				default: throw Jolly.unexpected(token);
			}
		}
		
		if(operators.Count > 0)
		{
			Op prevOp = operators.Peek();
			while(prevOp.valCount > 0 && 
				 (prevOp.precedence < op.precedence || op.leftToRight && prevOp.precedence == op.precedence))
			{
				pushOperator(operators.Pop());
				if(operators.Count == 0) break;
				prevOp = operators.Peek();
			}
		}
		
		if(op.isSpecial)
		{
			Enclosure newEnclosure = new Enclosure();
			switch(op.operation)
			{
			case OT.TERNARY:   // newEnclosure = new Enclosure(EnclosureKind.TERNARY_TRUE, expression.Count, 4); goto pushNewEnclosure;
			case OT.LOGIC_OR:  // newEnclosure = new Enclosure(EnclosureKind.LOGIC_OR, expression.Count, 3); goto pushNewEnclosure;
			case OT.LOGIC_AND: // newEnclosure = new Enclosure(EnclosureKind.LOGIC_AND, expression.Count, 2); goto pushNewEnclosure;
				op.startNodeCount = expression.Count;
				break;
			case OT.COLON:
				switch(enclosureStack.Peek().kind)
				{
					case EnclosureKind.SUBSCRIPT: break;
					case EnclosureKind.TERNARY:
						newEnclosure = new Enclosure(EnclosureKind.TERNARY, expression.Count);
						goto pushNewEnclosure;
					default: throw Jolly.unexpected(token);
				}
				break;
			}
			
			pushNewEnclosure:
			// while(enclosureStack.Peek().precedence <= newEnclosure.precedence) {
			// 	enclosureEnd(enclosureStack.Pop());
			// }
			enclosureStack.Push(newEnclosure);
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
		operators.Push(new Op(255, 0, false, OT.BRACKET_OPEN, false, token.location));
	}
	
	void parseBracketClose()
	{
		currentTokenKind = TokenKind.VALUE;
		
		// Enclosure enclosure;
		// do {
		// 	enclosure = enclosureStack.Pop();
		// 	enclosureEnd(enclosure, 0, null);
		// } while(enclosure.kind != EnclosureKind.SUBSCRIPT);
		
		Op op;
		while((op = operators.PopOrDefault()).operation != OT.BRACKET_OPEN) {
			if(op.operation == OT.UNDEFINED) {
				throw Jolly.unexpected(token);
			}
			pushOperator(op);
		}
	}
	
	void parseParenthesisOpen()
	{
		currentTokenKind = TokenKind.OPERATOR;
		
		Node called = null;
		EnclosureKind kind = EnclosureKind.PARENTHS;
		if(prevTokenKind == TokenKind.VALUE) {
			kind = EnclosureKind.FUNCTION_CALL;
			called = values.Pop();
		}
		enclosureStack.Push(new Enclosure(kind, expression.Count){ node = called });
		operators.Push(new Op(255, 0, false, OT.PARENTHESIS_OPEN, false, token.location));
	} // parseParenthesisOpen()
	
	void parseParenthesisClose()
	{
		// Enclosure enclosure;
		// do {
		// 	enclosure = enclosureStack.Pop();
		// 	enclosureEnd(enclosure, 0, null);
		// } while(enclosure.kind <  EnclosureKind.PARENTHS & enclosure.kind > EnclosureKind.FUNCTION_CALL);
		
		Op op;
		while((op = operators.PopOrDefault()).operation != OT.PARENTHESIS_OPEN) {
			if(op.operation == OT.UNDEFINED) {
				throw Jolly.unexpected(token);
			}
			pushOperator(op);
		}
	} // parseParenthesisClose()
	
	void parseTypeToReference()
	{
		if(prevTokenKind == TokenKind.OPERATOR | prevTokenKind == 0) {
			throw Jolly.unexpected(token);
		}
		Node prev = values.Pop();
		if(prev == null) {
			throw Jolly.addError(token.location, "Invalid expression term");
		}
		var mod = new NodeModifyType(token.location, prev, NodeModifyType.TO_REFERENCE);
		expression.Add(mod);
		values.Push(mod);
	}
	
	static Node newLabel() => new Node(new SourceLocation(), NT.LABEL);
	
	void enclosureEnd(Enclosure ended, int opIndex, NodeOperator opNode)
	{
		switch(ended.kind)
		{
		case EnclosureKind.LOGIC_OR: {
			
		} break;
		case EnclosureKind.LOGIC_AND: {
			
		} break;
		case EnclosureKind.FUNCTION_CALL: {
			Node[] arguments = null;
			if(ended.startNodeCount != expression.Count) {
				Node node = values.Pop();
				arguments = (node as NodeTuple)?.values.ToArray() ?? new Node[] { node };
			}
			values.Push(new NodeFunctionCall(token.location, ended.node, arguments ?? new Node[0]));
		} break;
		case EnclosureKind.PARENTHS: {
			if(values.PeekOrDefault()?.nodeType == NT.TUPLE)
			{
				var tup = ((NodeTuple)values.Pop());
				tup.closed = true; // Close list so you can't add to it: (a, b), c
				if(operators.PeekOrDefault().operation == OT.GET_MEMBER)
				{
					operators.Pop(); // Remove GET_MEMBER
					tup.scopeFrom = values.Pop();
					tup.nodeType = NT.MEMBER_TUPLE;
					expression.Insert(ended.startNodeCount, tup);
				} else {
					expression.Add(tup);
				}
				tup.memberCount = expression.Count - ended.startNodeCount;
				values.Push(tup);
			}
		} break;
		default:
			Debug.Assert(false);
			break;
		}
	}
	
	void pushOperator(Op op)
	{
		Node a, b = null;
		
		if(op.valCount == 2)
		{
			b = values.PopOrDefault();
			a = values.PopOrDefault();
			
			if(op.operation == OT.COMMA)
			{
				NodeTuple tuple = a as NodeTuple;
				if(tuple != null && !tuple.closed) {
					values.Push(tuple);
					if(b != null) {
						tuple.values.Add(b);
					}
				}
				else
				{
					tuple = new NodeTuple(op.location, scope, NT.TUPLE);
					values.Push(tuple);
					tuple.values.Add(a);
					if(b != null) {
						tuple.values.Add(b);
					}
				}
				return;
			}
			
			if(b == null) {
				throw Jolly.addError(op.location, "Invalid right expression term");
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
		
		
		
		if(op.isSpecial)
		{
			if(op.operation == OT.LOGIC_AND) {
				/*
				...
				cond_a = ...
				br cond_a, whentrue, whenfalse
				whentrue:
				...
				cond_b = ...
				goto whenfalse
				whenfalse:
				phi [from cond_a: false], [from cond_b: cond_b]
				*/
				var jump = new NodeJump();
				var phi = new NodePhi(new PhiBranch[] {
					new PhiBranch(a, Lookup.NODE_FALSE),
					new PhiBranch(b, b) }
				);
				jump.condition = a;
				jump.whenTrue = newLabel();
				jump.whenFalse = newLabel();
				
				expression.Insert(op.startNodeCount, jump);
				expression.Insert(op.startNodeCount + 1, jump.whenTrue);
				
				expression.Add(new NodeGoto(){ label = jump.whenFalse });
				expression.Add(jump.whenFalse);
				expression.Add(phi);
				values.Push(phi);
			} else if(op.operation == OT.LOGIC_OR) {
				/*
				...
				cond_a = ...
				br cond_a, whentrue, whenfalse
				whenFalse:
				...
				cond_b = ...
				goto whenTrue
				whenTrue:
				phi [from cond_a: true], [from cond_b: cond_b]
				*/
				
				// Op op = operators.Pop();
				// while(op.operation != OT.LOGIC_OR) {
				// 	pushOperator(op);
				// 	op = operators.Pop();
				// }
				
				// Node b = values.Pop(), a = values.Pop();
				var jump = new NodeJump();
				var phi = new NodePhi(new PhiBranch[] {
					new PhiBranch(a, Lookup.NODE_TRUE),
					new PhiBranch(b, b) }
				) { dataType = Lookup.getBaseType(TT.BOOL), typeKind = TypeKind.VALUE };
				jump.condition = a;
				jump.whenTrue = newLabel();
				jump.whenFalse = newLabel();
				
				expression.Insert(op.startNodeCount, jump);
				expression.Insert(op.startNodeCount + 1, jump.whenFalse);
				
				expression.Add(new NodeGoto(){ label = jump.whenTrue });
				expression.Add(jump.whenTrue);
				expression.Add(phi);
				values.Push(phi);
			}
		}
		
		NodeOperator opNode = new NodeOperator(op.location, op.operation, a, b);
		expression.Add(opNode);
		values.Push(opNode);
	} // pushOperator()
}

}