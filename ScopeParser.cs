using System.Collections.Generic;

namespace Jolly
{
    using TT = Token.Type;
    using NT = Node.NodeType;
	
    class ScopeParser
	{
		protected Token token;
		protected NT scopeHeader;
		protected Token[] tokens;
		protected int cursor, end;
		protected TableFolder scope;
		protected List<Node> program;
		protected List<ScopeParser> scopeQueue = new List<ScopeParser>();
		
		public ScopeParser(int cursor, int end, TableFolder scope, Token[] tokens, List<Node> program)
		{
			this.end = end;
			this.scope = scope;
			this.cursor = cursor;
			this.tokens = tokens;
			this.program = program;
		}
		
		protected bool parseStruct()
		{
			if(token.type != TT.STRUCT)
				return false;
			
			Token name = tokens[++cursor];
			if(name.type != TT.IDENTIFIER) {
				Jolly.unexpected(token);
				throw new ParseException();
			}
			
			Token brace = tokens[++cursor];
			if(brace.type != TT.BRACE_OPEN) {
				Jolly.unexpected(token);
				throw new ParseException();
			}
			
			Symbol _struct = new Symbol(NT.STRUCT, token.location, scope);
			TableFolder _structScope = new TableFolder(_struct);
			
			program.Add(_struct);
			scope.addChild(name.name, _structScope);
			scopeQueue.Add(new ScructParser(cursor+1, brace.partner.index, _structScope, tokens, program));
			
			cursor = brace.partner.index;
			
			return true;
		}
		
		protected bool parseUnion()
		{
			if(token.type != TT.UNION)
				return false;
			
				throw new ParseException();
			
			// return true;
		}
		
		#if false
		protected bool parseFor()
		{
			if(token.type != TT.FOR)
				return false;
			
			Token parenthesis = tokens[++cursor];
			if(parenthesis.type != TT.PARENTHESIS_OPEN) {
				Jolly.unexpected(token);
				throw new ParseException();
			}
			
			For _for = new For(token.location, null, SymbolFlag.private_members);
			program.Add(_for);
			
			var parser = new ExpressionParser(_for, tokens, TT.SEMICOLON, cursor+1, end, SymbolFlag.none);
			cursor = parser.parseExpression(true);
			_for.counter = parser.getExpression().ToArray();
			
			parser = new ExpressionParser(_for, tokens, TT.SEMICOLON, cursor+1, end, SymbolFlag.none);
			cursor = parser.parseExpression(false);
			_for.condition = parser.getExpression().ToArray();
			_for.conditionValue = parser.getValue();
			
			parser = new ExpressionParser(_for, tokens, TT.PARENTHESIS_CLOSE, cursor+1, end, SymbolFlag.none);
			cursor = parser.parseExpression(false);
			_for.increment = parser.getExpression().ToArray();
			
			Token brace = tokens[++cursor];
			if(brace.type == TT.BRACE_OPEN) {
				var blockParser = new BlockParser(cursor+1, brace.partner.index, _for, tokens, program);
				blockParser.parseBlock();
				cursor = brace.partner.index;
			} else {
				var blockParser = new BlockParser(cursor+1, end, _for, tokens, program);
				blockParser._parse();
			}
			
			return true;
		}
		
		protected bool parseForeach()
		{
			if(token.type != TT.FOREACH)
				return false;
			
				throw new ParseException();
			
			// return true;
		}
		
		protected bool parseWhile()
		{
			if(token.type != TT.WHILE)
				return false;
			
				throw new ParseException();
			
			// return true;
		}
		
		protected bool parseIf()
		{
			if(token.type != TT.IF)
				return false;
			
			Token parenthesis = tokens[++cursor];
			if(parenthesis.type != TT.PARENTHESIS_OPEN) {
				Jolly.unexpected(token);
				throw new ParseException();
			}
			
			var parser = new ExpressionParser(scope, tokens, TT.SEMICOLON, cursor+1, end, SymbolFlag.none);
			cursor = parser.parseExpression(true);
			var condtion = parser.getExpression().ToArray();
			var conditionValue = parser.getValue();
			
			
			
				throw new ParseException();
			
			// return true;
		}
		#endif
		
		protected bool parseBraceOpen()
		{
			if(token.type != TT.BRACE_OPEN)
				return false;
			
			// TODO: add new scope for block
			new BlockParser(cursor+1, token.partner.index, scope, tokens, program).parseBlock();
			cursor = token.partner.index;
			
			return true;
		}
		
		protected bool parseNamespace()
		{
			if(token.type != TT.NAMESPACE)
				return false;
			
			Token name = tokens[++cursor];
			if(name.type != TT.IDENTIFIER) {
				Jolly.unexpected(token);
				throw new ParseException();
			}
			
			Symbol _namespace = new Symbol(NT.BLOCK, token.location, scope);
			TableFolder _namespaceScope = new TableFolder(_namespace);
			
			program.Add(_namespace);
			scope.addChild(name.name, _namespaceScope);
						
			token = tokens[++cursor];
			if(token.type == TT.BRACE_OPEN) {
				scopeQueue.Add(new ScopeParser(cursor+1, token.partner.index, _namespaceScope, tokens, program));
				cursor = token.partner.index;
			} else if(token.type == TT.SEMICOLON) {
				scope = _namespaceScope;
			} else {
				Jolly.unexpected(token);
				throw new ParseException();
			}
			return true;
		}
		
		protected bool parseReturn()
		{
			if(token.type != TT.RETURN)
				return false;
			
			var iterator = scope;
			while(scope != null) {
				if(scope.node.nType == NT.FUNCTION)
					goto isInFunction;
				iterator = iterator.parent;
			}
			Jolly.addError(token.location, "Can only return from function.");
			throw new ParseException();
			isInFunction:
			
			var parser = new ExpressionParser(scope, tokens, TT.SEMICOLON, cursor+1, end);
			cursor = parser.parseExpression(false);
			var expression = parser.getExpression();
			
			Node node = parser.getValue();
			program.Add(new Return(token.location, node));
			
			return true;
		}
		
		protected void parseExpression()
		{
			var parser = new ExpressionParser(scope, tokens, TT.SEMICOLON, cursor, end);
			cursor = parser.parseExpression(true);
			
			if(parser.isFunction)
			{
				Token brace = tokens[cursor+1];
				if(brace.type != TT.BRACE_OPEN) {
					Jolly.unexpected(brace);
					throw new ParseException();
				}
				
				program.Add(parser.theFunction.node);
				scopeQueue.Add(new FunctionParser(cursor+1, brace.partner.index, parser.theFunction, tokens, program));
				cursor = brace.partner.index;
			} else {
				var expression = parser.getExpression();
				program.AddRange(expression);
			}
		}
		
		// ENUM IDENTIFIER (COLON [BYTE INT SHORT LONG UBYTE USHORT UINT ULONG])?
		// STRUCT IDENTIFIER BRACE_OPEN vars... BRACE_CLOSE
		// UNION IDENTIFIER? BRACE_OPEN vars... BRACE_CLOSE
		// FOR(expression;expression;expression) [block expression]
		// IF(expression) [block expression] (ELSE [block expression])?
		
		protected virtual void _parse()
		{
			if( parseStruct() ||
				parseNamespace())
				return;
			parseExpression();
		}
		
		public void parseBlock()
		{
			for (token = tokens[cursor];
				cursor < end;
				token = tokens[++cursor])
			{
				_parse();
			}
			
			foreach(var scopeParser in scopeQueue)
				scopeParser.parseBlock();
		}
	}
	
	class BlockParser : ScopeParser
	{
		public BlockParser(int cursor, int end, TableFolder scope, Token[] tokens, List<Node> program)
			: base(cursor, end, scope, tokens, program) { }
		
		protected override void _parse()
		{
			if(
				// parseIf()			||
				// parseFor()			||
				parseReturn()		||
				// parseForeach()		||
				// parseWhile()		||
				parseBraceOpen())
				return;
			parseExpression();
		}
	}
	
	class FunctionParser : BlockParser
	{
		public FunctionParser(int cursor, int end, TableFolder scope, Token[] tokens, List<Node> program)
			: base(cursor, end, scope, tokens, program)
		{
			scopeHeader = NT.FUNCTION;
		}
	}
	
	class ScructParser : ScopeParser
	{
		public ScructParser(int cursor, int end, TableFolder scope, Token[] tokens, List<Node> program)
			: base(cursor, end, scope, tokens, program) { }
		
		protected override void _parse()
		{
			if( parseUnion()	||
				parseStruct())
				return;
			parseExpression();
		}
	}
}