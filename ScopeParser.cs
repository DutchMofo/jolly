using System.Collections.Generic;

namespace Jolly
{
    using TT = Token.Type;
    using NT = Node.NodeType;
	
    class ScopeParser
	{
		protected Token token;
		protected Token[] tokens;
		protected int cursor, end;
		protected TableFolder scope;
		protected List<Node> program;
		
		// Is the parser stuck on a dependency that hasn't been parsed yet
		public bool parserStuck;
		
		public ScopeParser(int cursor, int end, TableFolder scope, Token[] tokens, List<Node> program)
		{
			this.program = program;
			this.tokens = tokens;
			this.cursor = cursor;
			this.scope = scope;
			this.end = end;
		}
		
		protected bool parseStruct()
		{
			if(token.type != TT.STRUCT)
				return false;
			
			Token name = tokens[(cursor += 1)];
			if(name.type != TT.IDENTIFIER) {
				Jolly.unexpected(token);
				throw new ParseException();
			}
			
			Token brace = tokens[(cursor += 1)];
			if(brace.type != TT.BRACE_OPEN) {
				Jolly.unexpected(token);
				throw new ParseException();
			}
			
			TableFolder _structScope = new TableFolder(NameFlags.IS_TYPE);
			scope.addChild(name.name, _structScope);
			new ScructParser(cursor + 1, brace.partnerIndex, _structScope, tokens, program).parseBlock();
			
			cursor = brace.partnerIndex;
			
			return true;
		}
		
		protected bool parseUnion()
		{
			if(token.type != TT.UNION)
				return false;
			
			Token name = tokens[(cursor += 1)];
			if(name.type != TT.IDENTIFIER) {
				Jolly.unexpected(token);
				throw new ParseException();
			}
			
			Token brace = tokens[(cursor += 1)];
			if(brace.type != TT.BRACE_OPEN) {
				Jolly.unexpected(token);
				throw new ParseException();
			}
			
			Symbol _union = new Symbol(token.location, name.name, NT.UNION);
			TableFolder unionScope = new TableFolder(NameFlags.UNION);
			
			program.Add(_union);
			scope.addChild(name.name, unionScope);
			new ScructParser(cursor + 1, brace.partnerIndex, unionScope, tokens, program).parseBlock();
			
			cursor = brace.partnerIndex;
			
			return true;
		}
		
		#if false
		protected bool parseFor()
		{
			if(token.type != TT.FOR)
				return false;
			
			Token parenthesis = tokens[(cursor += 1)];
			if(parenthesis.type != TT.PARENTHESIS_OPEN) {
				Jolly.unexpected(token);
				throw new ParseException();
			}
			
			For _for = new For(token.location, null, SymbolFlag.private_members);
			program.Add(_for);
			
			var parser = new ExpressionParser(_for, tokens, TT.SEMICOLON, cursor + 1, end, SymbolFlag.none);
			cursor = parser.parseExpression(true);
			_for.counter = parser.getExpression().ToArray();
			
			parser = new ExpressionParser(_for, tokens, TT.SEMICOLON, cursor + 1, end, SymbolFlag.none);
			cursor = parser.parseExpression(false);
			_for.condition = parser.getExpression().ToArray();
			_for.conditionValue = parser.getValue();
			
			parser = new ExpressionParser(_for, tokens, TT.PARENTHESIS_CLOSE, cursor + 1, end, SymbolFlag.none);
			cursor = parser.parseExpression(false);
			_for.increment = parser.getExpression().ToArray();
			
			Token brace = tokens[(cursor += 1)];
			if(brace.type == TT.BRACE_OPEN) {
				var blockParser = new BlockParser(cursor + 1, brace.partner.index, _for, tokens, program);
				blockParser.parseBlock();
				cursor = brace.partner.index;
			} else {
				var blockParser = new BlockParser(cursor + 1, end, _for, tokens, program);
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
			
			Token parenthesis = tokens[(cursor += 1)];
			if(parenthesis.type != TT.PARENTHESIS_OPEN) {
				Jolly.unexpected(token);
				throw new ParseException();
			}
			
			var parser = new ExpressionParser(scope, tokens, TT.SEMICOLON, cursor + 1, end, SymbolFlag.none);
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
			new BlockParser(cursor + 1, token.partnerIndex, scope, tokens, program).parseBlock();
			cursor = token.partnerIndex;
			
			return true;
		}
		
		protected bool parseNamespace()
		{
			if(token.type != TT.NAMESPACE)
				return false;
			
			Token name = tokens[(cursor += 1)];
			if(name.type != TT.IDENTIFIER) {
				Jolly.unexpected(token);
				throw new ParseException();
			}
			
			Symbol _namespace = new Symbol(token.location, name.name, NT.BLOCK);
			TableFolder _namespaceScope = new TableFolder();
			
			program.Add(_namespace);
			scope.addChild(name.name, _namespaceScope);
						
			token = tokens[(cursor += 1)];
			if(token.type == TT.BRACE_OPEN) {
				new ScopeParser(cursor + 1, token.partnerIndex, _namespaceScope, tokens, program).parseBlock();
				cursor = token.partnerIndex;
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
			
			// var iterator = scope;
			// while(scope != null) {
			// 	if(scope.node.nodeType == NT.FUNCTION)
			// 		goto isInFunction;
			// 	iterator = iterator.parent;
			// }
			// Jolly.addError(token.location, "Can only return from function.");
			// throw new ParseException();
			// isInFunction:
			
			var parser = new ExpressionParser(scope, tokens, TT.SEMICOLON, cursor+1, end);
			cursor = parser.parseExpression(this, false);
			var expression = parser.getExpression();
			
			Node node = parser.getValue();
			program.Add(new Result(token.location));
			
			return true;
		}
		
		protected void parseExpression()
		{
			var parser = new ExpressionParser(scope, tokens, TT.SEMICOLON, cursor, end);
			cursor = parser.parseExpression(this, true);
			
			if(parser.isFunction)
			{
				Token brace = tokens[cursor + 1];
				if(brace.type != TT.BRACE_OPEN) {
					Jolly.unexpected(brace);
					throw new ParseException();
				}
				
				// program.Add(parser.theFunction.node);
				new BlockParser(cursor+1, brace.partnerIndex, parser.theFunction, tokens, program).parseBlock();
				
				if(program[program.Count-1].nodeType != NT.RETURN)
					program.Add(new Result(tokens[brace.partnerIndex].location));
				
				cursor = brace.partnerIndex;
			} else {
				var expression = parser.getExpression();
				program.AddRange(expression);
			}
		}
		
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
				token = tokens[(cursor += 1)])
			{
				_parse();
			}
			return true;
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
	
	class ScructParser : ScopeParser
	{
		public ScructParser(int cursor, int end, TableFolder scope, Token[] tokens, List<Node> program)
			: base(cursor, end, scope, tokens, program) { }
		
		protected override void _parse()
		{
			if( parseStruct() ||
				parseUnion())
				return;
			parseExpression();
		}
	}
}