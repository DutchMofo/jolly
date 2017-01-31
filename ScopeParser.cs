using System.Collections.Generic;

namespace Jolly
{
    using TT = Token.Type;
    using NT = AST_Node.Type;
	using DefineMode = ExpressionParser.DefineMode;
	
    class ScopeParser
	{
		protected Token token;
		protected Token[] tokens;
		protected int cursor, end;
		protected Scope scope;
		protected List<AST_Node> program;
		protected DefineMode defineMode = DefineMode.FUNCTION_OR_VARIABLE;
		
		public AST_Symbol scopeHead;
		
		public ScopeParser(int cursor, int end, Scope scope, Token[] tokens, List<AST_Node> program)
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
			
			Token name = tokens[cursor += 1];
			if(name.type != TT.IDENTIFIER) {
				throw Jolly.unexpected(token);
			}
			
			Token brace = tokens[cursor += 1];
			if(brace.type != TT.BRACE_OPEN) {
				throw Jolly.unexpected(token);
			}
			
			var structScope       = new Scope(scope);
			var structType        = new DataType_Struct() { name = name.text, structScope = structScope };
			var structDefinition  = new Value{ type = structType, kind = Value.Kind.STATIC_TYPE };
			var structNode        = new AST_Scope(name.location, NT.STRUCT, structScope, name.text) { result = structDefinition };
			structScope.scopeType = structDefinition;
			
			if(!scope.Add(name.text, structDefinition)) {
				Jolly.addError(name.location, "Trying to redefine \"{0}\"".fill(name.text));
			}
			program.Add(structNode);
			new StructParser(cursor + 1, brace.partnerIndex, structScope, tokens, program)
				{ scopeHead = structNode, defineMode = DefineMode.MEMBER } // Hacky
				.parseBlock();
			
			structType.members = new DataType[structType.memberMap.Count];
			cursor = brace.partnerIndex;
			
			return true;
		}
		
		#if false
		protected bool parseUnion()
		{
			if(token.type != TT.UNION)
				return false;
			
			Token name = tokens[cursor += 1];
			if(name.type != TT.IDENTIFIER) {
				throw Jolly.unexpected(token);
			}
			
			Token brace = tokens[cursor += 1];
			if(brace.type != TT.BRACE_OPEN) {
				throw Jolly.unexpected(token);
			}
			
			TableFolder unionScope = new TableFolder(){ flags = NameFlags.UNION | NameFlags.FOLDER };
			unionScope.type = new DataType(unionScope) { name = name.name };
			
			if(!scope.Add(name.name, unionScope)) {
				Jolly.addError(name.location, "Trying to redefine \"{0}\"".fill(name.name));
			}
			var unionNode = new NodeSymbol(token.location, name.name, scope, NT.UNION) /*{ dataType = unionScope }*/;
			program.Add(unionNode);
			new StructParser(cursor + 1, brace.partnerIndex, unionScope, tokens, program)
				{ scopeHead = unionNode } // Hacky
				.parseBlock();
			
			cursor = brace.partnerIndex;
			
			return true;
		}
		
		protected bool parseFor()
		{
			if(token.type != TT.FOR)
				return false;
			
			Token parenthesis = tokens[cursor += 1];
			if(parenthesis.type != TT.PARENTHESIS_OPEN) {
				Jolly.unexpected(token);
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
			
			Token brace = tokens[cursor += 1];
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
			
			Token parenthesis = tokens[cursor += 1];
			if(parenthesis.type != TT.PARENTHESIS_OPEN) {
				Jolly.unexpected(token);
			}
			
			var parser = new ExpressionParser(scope, tokens, TT.SEMICOLON, cursor + 1, end, SymbolFlag.none);
			cursor = parser.parseExpression(true);
			var condtion = parser.getExpression().ToArray();
			var conditionValue = parser.getValue();
			
			
			
				throw new ParseException();
			
			// return true;
		}
		
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
			
			Token name = tokens[cursor += 1];
			if(name.type != TT.IDENTIFIER) {
				throw Jolly.unexpected(token);
			}
			
			NodeSymbol _namespace = new NodeSymbol(token.location, name.name, scope, NT.BLOCK);
			TableFolder _namespaceScope = new TableFolder() { flags = NameFlags.FOLDER };
			
			program.Add(_namespace); 
			if(!scope.Add(name.name, _namespaceScope)) {
				throw Jolly.addError(name.location, "Trying to redefine \"{0}\"".fill(name.name));
			}
						
			token = tokens[cursor += 1];
			if(token.type == TT.BRACE_OPEN) {
				new ScopeParser(cursor + 1, token.partnerIndex, _namespaceScope, tokens, program).parseBlock();
				cursor = token.partnerIndex;
			} else if(token.type == TT.SEMICOLON) {
				scope = _namespaceScope;
			} else {
				throw Jolly.unexpected(token);
			}
			return true;
		}
		#endif
		
		protected bool parseReturn()
		{
			if(token.type != TT.RETURN)
				return false;
			
			var parser = new ExpressionParser(scope, tokens, TT.SEMICOLON, cursor + 1, program, DefineMode.NONE);
			cursor = parser.parseExpression();
			
			program.Add(new AST_Node(token.location, NT.RETURN));
			
			return true;
		}
				
		protected virtual void _parse()
		{
			if( 
				parseStruct() ||
				// parseNamespace() ||
				false)
				return;
			var parser = new ExpressionParser(scope, tokens, TT.SEMICOLON, cursor, program, defineMode);
			cursor = parser.parseExpression();
		}
		
		public void parseBlock()
		{
			int startNodeCount = program.Count;
			for (token = tokens[cursor];
				cursor < end;
				token = tokens[cursor += 1])
			{
				_parse();
			}
			if(scopeHead != null) {
				scopeHead.memberCount = program.Count - startNodeCount;
			}
		}
	}
	
	class BlockParser : ScopeParser
	{
		public BlockParser(int cursor, int end, Scope scope, Token[] tokens, List<AST_Node> program)
			: base(cursor, end, scope, tokens, program) { }
		
		protected override void _parse()
		{
			if(
				// parseIf()			||
				// parseFor()			||
				parseReturn()		||
				// parseForeach()		||
				// parseWhile()		||
				// parseBraceOpen()	||
				false)
				return;
			var parser = new ExpressionParser(scope, tokens, TT.SEMICOLON, cursor, program, defineMode);
			cursor = parser.parseExpression();
		}
	}
	
	class StructParser : ScopeParser
	{
		public StructParser(int cursor, int end, Scope scope, Token[] tokens, List<AST_Node> program)
			: base(cursor, end, scope, tokens, program) { }
		
		protected override void _parse()
		{
			if( 
				parseStruct()	||
				// parseUnion()	||
				false)
				return;
			var parser = new ExpressionParser(scope, tokens, TT.SEMICOLON, cursor, program, defineMode);
			cursor = parser.parseExpression();
		}
	}
}