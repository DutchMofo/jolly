
namespace Jolly
{
    using TT = Token.Type;
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
				parseNextStruct();
			}
			if(scopeHead != null) {
				scopeHead.memberCount = parseData.ast.Count - startNodeCount;
			}
		}
		
		void parseNextStruct()
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
		
		public void parseBlockScope()
		{
			int startNodeCount = parseData.ast.Count;
			for (token = parseData.tokens[parseData.cursor];
				parseData.cursor < end;
				token = parseData.tokens[parseData.cursor += 1])
			{
				parseNextBlock();
			}
			if(scopeHead != null) {
				scopeHead.memberCount = parseData.ast.Count - startNodeCount;
			}
		}
		
		void parseNextBlock()
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
		
		public void parseGlobalScope()
		{
			int startNodeCount = parseData.ast.Count;
			for (token = parseData.tokens[parseData.cursor];
				parseData.cursor < end;
				token = parseData.tokens[parseData.cursor += 1])
			{
				parseNextGlobal();
			}
			if(scopeHead != null) {
				scopeHead.memberCount = parseData.ast.Count - startNodeCount;
			}
		}
		
		void parseNextGlobal()
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
		
		void parseStruct()
		{
			Token name = parseData.tokens[parseData.cursor += 1];
			if(name.type != TT.IDENTIFIER) {
				throw Jolly.unexpected(token);
			}
			
			Token next = parseData.tokens[parseData.cursor += 1];
			AST_Node inherits = null;
			
			if(next.type == TT.COLON) {
				parseData.cursor += 1;
				inherits = new ExpressionParser(parseData, TT.BRACE_OPEN, scope, DefineMode.EXPRESSION, end)
					.parse(false).getValue();
				next = parseData.tokens[parseData.cursor];
			}
			
			if(next.type != TT.BRACE_OPEN) {
				throw Jolly.unexpected(token);
			}
			
			AST_Struct      structNode  = new AST_Struct(token.location);
			DataType_Struct structType  = new DataType_Struct();
			SymbolTable     structTable = new SymbolTable(scope);
			
			structNode.inherits = inherits;
			structNode.symbol   = structTable;
			structNode.text     = structType.name  = name.text;
			structNode.result   = structTable.type = new Value { kind = Value.Kind.STATIC_TYPE, type = structType };
			
			if(!scope.Add(name.text, structTable)) {
				Jolly.addError(name.location, "Trying to redefine \"{0}\"".fill(name.text));
			}
			
			parseData.ast.Add(structNode);
			parseData.cursor += 1;
			new ScopeParser(parseData, next.partnerIndex, structTable)
				{ scopeHead = structNode } // Hacky
				.parseStructScope();
			
			structType.members = new DataType[structType.memberMap.Count];
		}
		
		void parseIf()
		{
			TT terminator = TT.PARENTHESIS_CLOSE;
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
			{ // Early exit	
				if(!parser.isDefinition()) {
					throw Jolly.addError(token.location, "Expected first part to be a definition.");
				}
				parseData.cursor += 1; // Skip semicolon
				parser = new ExpressionParser(parseData, terminator, ifTable, DefineMode.EXPRESSION, parenthEnd)
					.parse(false);
			} else if(parser.isDefinition()) {
				throw Jolly.addError(token.location, "Definition not allowed as exression.");
			}
			ifNode.condition = parser.getValue();
			ifNode.conditionCount = parseData.ast.Count - startNodeCount;
			startNodeCount = parseData.ast.Count;
			
			Token brace = parseData.tokens[parseData.cursor += 1];
			
			var scopeParser = new ScopeParser(parseData, brace.partnerIndex, ifTable);
			if(brace.type != TT.BRACE_OPEN) {
				scopeParser.parseNextBlock();
			} else {
				parseData.cursor += 1; // Skip brace open
				scopeParser.parseBlockScope();
			}
			ifNode.ifCount = parseData.ast.Count - startNodeCount;
			startNodeCount = parseData.ast.Count;
			
			Token _else = parseData.tokens[parseData.cursor + 1];
			if(_else.type == TT.ELSE)
			{
				SymbolTable elseTable = new SymbolTable(scope);
				ifNode.elseScope = elseTable;
				
				brace = parseData.tokens[parseData.cursor += 2]; // Also skip else
				scopeParser = new ScopeParser(parseData, brace.partnerIndex, elseTable);
				if(brace.type != TT.BRACE_OPEN) {
					scopeParser.parseNextBlock();
				} else {
					parseData.cursor += 1; // Skip brace open
					scopeParser.parseBlockScope();
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