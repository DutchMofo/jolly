
namespace Jolly
{
    using TT = Token.Type;
    using NT = AST_Node.Type;
	using DefineMode = ExpressionParser.DefineMode;
	
    class ScopeParser
	{
		public AST_Symbol scopeHead;
		SharedParseData parseData;
		Scope scope;
		Token token;
		int end;
		
		public ScopeParser(SharedParseData parseData, int end, Scope scope)
		{
			this.parseData = parseData;
			this.scope = scope;
			this.end = end;
		}
		
		public void parseStructScope()
		{
			int startNodeCount = parseData.ast.Count;
			for (token = parseData.tokens[parseData.cursor];
				parseData.cursor < end;
				token = parseData.tokens[parseData.cursor += 1])
			{
				switch(token.type)
				{
				case TT.STRUCT:     parseStruct();  break;
				// case TT.UNION:     parseStruct();  break;
				default:
					new ExpressionParser(parseData, TT.SEMICOLON, DefineMode.MEMBER, scope).parse();
					break;
				}
			}
		}
		
		public void parseBlockScope()
		{
			int startNodeCount = parseData.ast.Count;
			for (token = parseData.tokens[parseData.cursor];
				parseData.cursor < end;
				token = parseData.tokens[parseData.cursor += 1])
			{
				switch(token.type)
				{
				// case TT.IF:         parseIf();      break;
				// case TT.FOR:        parseFor();     break;
				case TT.RETURN:     parseReturn();  break;
				// case TT.WHILE:      parseWhile();   break;
				// case TT.BRACE_OPEN: parseBraceOpen; break;
				default:
					new ExpressionParser(parseData, TT.SEMICOLON, DefineMode.FUNCTION_OR_VARIABLE, scope).parse();
					break;
				}
			}
			if(scopeHead != null) {
				scopeHead.memberCount = parseData.ast.Count - startNodeCount;
			}
		}
		
		public void parseGlobalScope()
		{
			int startNodeCount = parseData.ast.Count;
			for (token = parseData.tokens[parseData.cursor];
				parseData.cursor < end;
				token = parseData.tokens[parseData.cursor += 1])
			{
				switch(token.type)
				{
				case TT.STRUCT:     parseStruct();  break;
				// case TT.UNION:     parseStruct();  break;
				// case TT.NAMESPACE:     parseStruct();  break;
				default:
					new ExpressionParser(parseData, TT.SEMICOLON, DefineMode.FUNCTION_OR_VARIABLE, scope).parse();
					break;
				}
			}
			if(scopeHead != null) {
				scopeHead.memberCount = parseData.ast.Count - startNodeCount;
			}
		}
		
		void parseStruct()
		{
			Token name = parseData.tokens[parseData.cursor += 1];
			if(name.type != TT.IDENTIFIER) {
				throw Jolly.unexpected(token);
			}
			
			Token brace = parseData.tokens[parseData.cursor += 1];
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
			parseData.ast.Add(structNode);
			parseData.cursor += 1;
			new ScopeParser(parseData, brace.partnerIndex, structScope)
				{ scopeHead = structNode } // Hacky
				.parseStructScope();
			
			structType.members = new DataType[structType.memberMap.Count];
		}
				
		void parseReturn()
		{
			parseData.cursor += 1;
			var parser = new ExpressionParser(parseData, TT.SEMICOLON, DefineMode.NONE, scope);
			parser.parse();
			parseData.ast.Add(new AST_Return(token.location, parser.getValue()));
		}
	}
}