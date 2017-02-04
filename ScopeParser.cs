
namespace Jolly
{
    using TT = Token.Type;
    using NT = AST_Node.Type;
	using DefineMode = ExpressionParser.DefineMode;
	
    class ScopeParser
	{
		public AST_Scope scopeHead;
		SharedParseData parseData;
		SymbolTable scope;
		Token token;
		int end;
		
		public ScopeParser(SharedParseData parseData, int end, SymbolTable scope)
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
					new ExpressionParser(parseData, TT.SEMICOLON, scope, DefineMode.MEMBER, end)
						.parse(false);
					break;
				}
			}
			if(scopeHead != null) {
				scopeHead.memberCount = parseData.ast.Count - startNodeCount;
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
				case TT.IF:         parseIf();      break;
				// case TT.FOR:        parseFor();     break;
				case TT.RETURN:     parseReturn();  break;
				// case TT.WHILE:      parseWhile();   break;
				// case TT.BRACE_OPEN: parseBraceOpen; break;
				default:
					// TODO: Should i allow function nesting?
					new ExpressionParser(parseData, TT.SEMICOLON, scope, DefineMode.STATEMENT, end)
						.parse(false);
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
					new ExpressionParser(parseData, TT.SEMICOLON, scope, DefineMode.STATEMENT, end)
						.parse(false);
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
			
			DataType_Struct structType  = new DataType_Struct();
			AST_Scope       structNode  = new AST_Scope(token.location, NT.STRUCT);
			SymbolTable     structTable = new SymbolTable(scope);
			
			structNode.symbol = structTable;
			structNode.text   = structType.name  = name.text;
			structNode.result = structTable.type = new Value { kind = Value.Kind.STATIC_TYPE, type = structType };
			
			if(!scope.Add(name.text, structTable)) {
				Jolly.addError(name.location, "Trying to redefine \"{0}\"".fill(name.text));
			}
			
			parseData.ast.Add(structNode);
			parseData.cursor += 1;
			new ScopeParser(parseData, brace.partnerIndex, structTable)
				{ scopeHead = structNode } // Hacky
				.parseStructScope();
			
			structType.members = new DataType[structType.memberMap.Count];
		}
		
		void parseIf()
		{
			TT terminator = terminator = TT.PARENTHESIS_CLOSE;
			Token parenth = parseData.tokens[parseData.cursor + 1];
			if(parenth.type != TT.PARENTHESIS_OPEN) {
				throw Jolly.unexpected(parenth);
			}
			int parenthEnd = parenth.partnerIndex;
			parseData.cursor += 2; // Skip if and parenthesis open
			
			SymbolTable ifTable = new SymbolTable(scope);
			AST_If      ifNode  = new AST_If(token.location);
			ifNode.ifScope = ifTable;
			parseData.ast.Add(ifNode);
			int startNodeCount = parseData.ast.Count;
			
			var parser = new ExpressionParser(parseData, terminator, ifTable, DefineMode.ARGUMENT, parenthEnd)
				.parse(true);
			
			if(parseData.cursor < parenthEnd)
			{
				// Early exit
				if(!parser.isDefinition()) {
					throw Jolly.addError(token.location, "Expected first part to be a definition.");
				}
				parseData.cursor += 1; // Skip semicolon
				parser = new ExpressionParser(parseData, terminator, ifTable, DefineMode.EXPRESSION, parenthEnd)
					.parse(false);
			} else if(parser.isDefinition()) {
				throw Jolly.addError(token.location, "Definition not allowed as exression.");
			}
			ifNode.conditionCount = parseData.ast.Count - startNodeCount;
			startNodeCount = parseData.ast.Count;
			
			Token brace = parseData.tokens[parseData.cursor += 1];
			if(brace.type != TT.BRACE_OPEN) {
				// Incomplete can only parse expressions
				new ExpressionParser(parseData, TT.SEMICOLON, ifTable, DefineMode.STATEMENT, end)
					.parse(false);
			} else {
				parseData.cursor += 1; // Skip brace open
				new ScopeParser(parseData, brace.partnerIndex, ifTable)
					.parseBlockScope();
			}
			ifNode.ifCount = parseData.ast.Count - startNodeCount;
			startNodeCount = parseData.ast.Count;
			
			Token _else = parseData.tokens[parseData.cursor + 1];
			if(_else.type == TT.ELSE)
			{
				SymbolTable elseTable = new SymbolTable(scope);
				ifNode.elseScope = elseTable;
				
				brace = parseData.tokens[parseData.cursor += 2]; // Also skip else
				if(brace.type != TT.BRACE_OPEN) {
					// Incomplete can only parse expressions					
					new ExpressionParser(parseData, TT.SEMICOLON, elseTable, DefineMode.STATEMENT, end)
						.parse(false);
				} else {
					parseData.cursor += 1; // Skip brace open
					new ScopeParser(parseData, brace.partnerIndex, elseTable)
						.parseBlockScope();
				}
				ifNode.elseCount = parseData.ast.Count - startNodeCount;
			}
		}
			
		void parseReturn()
		{
			parseData.cursor += 1;
			var parser = new ExpressionParser(parseData, TT.SEMICOLON, scope, DefineMode.EXPRESSION, end)
				.parse(false);
			parseData.ast.Add(new AST_Return(token.location, parser.getValue()));
		}
	}
}