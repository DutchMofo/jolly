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
		protected DefineMode defineMode = DefineMode.FUNCTION_VARIABLE;
		
		public NodeSymbol scopeHead;
		public static readonly Node scopeEnd = new Node(NT.SCOPE_END, new SourceLocation());
		
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
			
			Token name = tokens[cursor += 1];
			if(name.type != TT.IDENTIFIER) {
				throw Jolly.unexpected(token);
			}
			
			Token brace = tokens[cursor += 1];
			if(brace.type != TT.BRACE_OPEN) {
				throw Jolly.unexpected(token);
			}
			
			TableFolder structScope = new TableFolder(scope) { flags = NameFlags.IS_TYPE };
			var structType = new DataTypeStruct() { name = name.text, structScope = structScope } as DataType;
			var structNode = new NodeSymbol(name.location, name.text, scope, NT.STRUCT)
				{ typeInfo = new TypeInfo(structType, true) };
			
			if(!scope.Add(name.text, structType, structNode)) {
				Jolly.addError(name.location, "Trying to redefine \"{0}\"".fill(name.text));
			}
			program.Add(structNode);
			new StructParser(cursor + 1, brace.partnerIndex, structScope, tokens, program)
				{ scopeHead = structNode, defineMode = DefineMode.MEMBER } // Hacky
				.parseBlock();
			
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
			
			var parser = new ExpressionParser(scope, tokens, TT.SEMICOLON, cursor + 1, end, program, this);
			cursor = parser.parseExpression(DefineMode.NONE);
			
			program.Add(new NodeResult(token.location, NT.RETURN));
			
			return true;
		}
		
		protected void parseExpression()
		{
			var parser = new ExpressionParser(scope, tokens, TT.SEMICOLON, cursor, end, program, this);
			cursor = parser.parseExpression(defineMode);
		}
		
		protected virtual void _parse()
		{
			if( 
				parseStruct() ||
				// parseNamespace() ||
				false)
				return;
			parseExpression();
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
				program.Add(scopeEnd);
				scopeHead.memberCount = program.Count - startNodeCount;
			}
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
				// parseBraceOpen()	||
				false)
				return;
			parseExpression();
		}
	}
	
	class StructParser : ScopeParser
	{
		public StructParser(int cursor, int end, TableFolder scope, Token[] tokens, List<Node> program)
			: base(cursor, end, scope, tokens, program) { }
		
		protected override void _parse()
		{
			if( 
				parseStruct()	||
				// parseUnion()	||
				false)
				return;
			parseExpression();
		}
	}
}