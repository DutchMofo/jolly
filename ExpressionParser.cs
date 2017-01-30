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
		enclosureStack.Push(new Enclosure(program.Count, Enclosure.Kind.STATEMENT));
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
	
	struct Enclosure
	{
		public enum Kind : byte
		{
			STATEMENT     = 0,
			GROUP         = 1, // A group is are the (values between parenthesis)
			TERNARY       = 2,
			SUBSCRIPT     = 3,
		}
		
		public Enclosure(int startIndex, Kind kind)
		{
			this.startIndex = startIndex;
			this.isFunctionCall = false;
			this.hasColon = false;
			this.kind = kind;
			this.node = null;
		}
		
		public int startIndex;
		public bool isFunctionCall, hasColon;
		public Node node;
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
	Stack<Enclosure> enclosureStack = new Stack<Enclosure>();
	
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
			int startNodeCount = enclosureStack.Peek().startIndex;
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
				case TT.ASTERISK: op = new Op(02, 1, false, OT.DEREFERENCE); break; // TODO: Move these new Op's values to lookup file?
				case TT.AND:	  op = new Op(02, 1, false, OT.REFERENCE  ); break;
				case TT.PLUS: case TT.MINUS: values.Push(new NodeLiteral(token.location, 0)); break;
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
			op.operatorIndex = expression.Count;
			
			if(op.operation == OT.TERNARY)
			{
				enclosureStack.Push(new Enclosure(expression.Count, Enclosure.Kind.TERNARY));
			}
			else if(op.operation == OT.COLON)
			{
				Enclosure enclosure = enclosureStack.Pop();
				if( enclosure.kind == Enclosure.Kind.SUBSCRIPT ||
					enclosure.kind == Enclosure.Kind.TERNARY)
				{
					if(enclosure.hasColon) {
						throw Jolly.unexpected(token);
					}
					if(enclosure.kind == Enclosure.Kind.SUBSCRIPT) {
						op.operation = OT.SLICE;
						op.leftToRight = true;
						op.isSpecial = false;
					} else {
						op.leftToRight = false;
					}
					enclosure.hasColon = true;
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
		operators.Push(new Op(255, 0, false, OT.BRACKET_OPEN, false, token.location));
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
		Debug.Assert(enclosure.kind == Enclosure.Kind.SUBSCRIPT);
		
		// TODO: should check if there are enough values
		if(enclosure.hasColon)
		{
			var node = values.Peek();
			var slice = node as NodeOperator;
			if(slice == null || slice.operation != OT.SLICE) {
				throw Jolly.unexpected(node);
			}
		}
		else
		{
			Node b = values.Pop(), a = values.Pop();
			var opNode = new NodeOperator(token.location, OT.SUBSCRIPT, a, b);
			expression.Add(opNode);
			values.Push(opNode);
		}
	}
	
	void parseParenthesisOpen()
	{
		currentTokenKind = TokenKind.OPERATOR;
		
		Enclosure enclosure = new Enclosure(expression.Count, Enclosure.Kind.GROUP);
		if(prevTokenKind == TokenKind.VALUE) {
			enclosure.isFunctionCall = true;
			enclosure.node = values.Pop();
		}
		enclosureStack.Push(enclosure);
		operators.Push(new Op(255, 0, false, OT.PARENTHESIS_OPEN, false, token.location));
	} // parseParenthesisOpen()
	
	void parseParenthesisClose()
	{
		Op op;
		while((op = operators.PopOrDefault()).operation != OT.PARENTHESIS_OPEN) {
			if(op.operation == OT.UNDEFINED) {
				throw Jolly.unexpected(token);
			}
			pushOperator(op);
		}
		
		Enclosure enclosure = enclosureStack.Pop();
		Debug.Assert(enclosure.kind == Enclosure.Kind.GROUP);
		
		if(enclosure.isFunctionCall)
		{
			Node[] arguments = null;
			if(enclosure.startIndex != expression.Count) {
				Node node = values.Pop();
				arguments = (node as NodeTuple)?.values.ToArray() ?? new Node[] { node };
			}
			values.Push(new NodeFunctionCall(token.location, enclosure.node, arguments ?? new Node[0]));
		}
		else if(values.PeekOrDefault()?.nodeType == NT.TUPLE)
		{
			var tup = ((NodeTuple)values.Pop());
			// Close list so you can't add to it: (a, b), c
			tup.closed = true;
			tup.memberCount = expression.Count - enclosure.startIndex;
			if(operators.PeekOrDefault().operation == OT.GET_MEMBER) {
				operators.Pop(); // Remove GET_MEMBER
				tup.membersFrom = values.Pop();
				tup.nodeType = NT.MEMBER_TUPLE;
				expression.Insert(enclosure.startIndex, tup);
			} else {
				expression.Add(tup);
			}
			values.Push(tup);
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
			if(op.operation == OT.LOGIC_AND)
			{
				/*
				...
				condition_a = ...
				jump condition_a, whentrue, whenfalse
				whentrue:
				...
				condition_b = ...
				goto whenfalse
				whenfalse:
				phi [from condition_a: false], [from condition_b: condition_b]
				*/
				var jump = new NodeJump();
				var phi = new NodePhi(new PhiBranch[] {
					new PhiBranch(a, Lookup.NODE_FALSE),
					new PhiBranch(b, b) }
				);
				jump.condition = a;
				jump.whenTrue = newLabel();
				jump.whenFalse = newLabel();
				
				expression.Insert(op.operatorIndex, jump);
				expression.Insert(op.operatorIndex + 1, jump.whenTrue);
				
				expression.Add(new NodeGoto(){ label = jump.whenFalse });
				expression.Add(jump.whenFalse);
				expression.Add(phi);
				values.Push(phi);
				return;
			}
			else if(op.operation == OT.LOGIC_OR)
			{
				/*
				...
				condition_a = ...
				jump condition_a, whentrue, whenfalse
				whenFalse:
				...
				condition_b = ...
				goto whenTrue
				whenTrue:
				phi [from condition_a: true], [from condition_b: cond_b]
				*/
				var jump = new NodeJump();
				var phi = new NodePhi(new PhiBranch[] {
					new PhiBranch(a, Lookup.NODE_TRUE),
					new PhiBranch(b, b) }
				) { dataType = Lookup.getBaseType(TT.BOOL), typeKind = TypeKind.VALUE };
				jump.condition = a;
				jump.whenTrue = newLabel();
				jump.whenFalse = newLabel();
				
				expression.Insert(op.operatorIndex, jump);
				expression.Insert(op.operatorIndex + 1, jump.whenFalse);
				
				expression.Add(new NodeGoto(){ label = jump.whenTrue });
				expression.Add(jump.whenTrue);
				expression.Add(phi);
				values.Push(phi);
				return;
			}
			else if(op.operation == OT.TERNARY)
			{
				var enclosure = enclosureStack.Pop();
				enclosure.node = a;
				enclosureStack.Push(enclosure);
				values.Push(b);
				Debug.Assert(enclosure.kind == Enclosure.Kind.TERNARY);
				return;
			}
			else if(op.operation == OT.COLON)
			{
				/*
				...
				condition = ...
				jump condition, whentrue, whenfalse
				whentrue:
				...
				val_a = ...
				goto end
				whenfalse:
				...
				val_b = ...
				goto end
				end:
				phi [from val_a: val_a], [from val_b: val_b]
				*/	
				var enclosure = enclosureStack.Pop();
				Debug.Assert(enclosure.kind == Enclosure.Kind.TERNARY);
				
				var jump = new NodeJump();
				var phi = new NodePhi(new PhiBranch[] {
					new PhiBranch(a, a),
					new PhiBranch(b, b) }
				);
				jump.condition = enclosure.node;
				jump.whenTrue = newLabel();
				jump.whenFalse = newLabel();
				var end = newLabel();
				
				expression.Insert(enclosure.startIndex, jump);
				expression.Insert(enclosure.startIndex + 1, jump.whenTrue);
				expression.Insert(op.operatorIndex + 2, new NodeGoto() { label = end });
				expression.Insert(op.operatorIndex + 3, jump.whenFalse);
				expression.Add(new NodeGoto() { label = end });
				expression.Add(end);
				expression.Add(phi);
				return;
			}
		} // if(op.isSpecial)
		
		NodeOperator opNode = new NodeOperator(op.location, op.operation, a, b);
		expression.Add(opNode);
		values.Push(opNode);
	} // pushOperator()
}

}