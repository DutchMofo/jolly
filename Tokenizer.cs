using System.Collections.Generic;

namespace Jolly
{

class Token
{
	public enum Type
	{
		UNDEFINED = 0,
		
		/*############
			Tokens
		#############*/
		TILDE,				// ~
		BACK_QUOTE,			// `
		EXCLAMATION,		// !
		AT,					// @
		HASH,				// #
		DOLLAR,				// $
		PERCENT, 			// %
		CARET,				// ^
		AND,				// &
		ASTERISK,			// *
		PARENTHESIS_OPEN,	// (
		PARENTHESIS_CLOSE,	// )
		BRACKET_OPEN,		// [
		BRACKET_CLOSE,		// ]
		BRACE_OPEN,			// {
		BRACE_CLOSE,		// }
		MINUS,				// -
		PLUS,				// +
		EQUAL,				// =
		COLON,				// :
		SEMICOLON,			// ;
		APOSTROPHE,			// '
		QUOTE,				// "
		PIPE,				// |
		BACKSLASH,			// \
		SLASH,				// /
		LESS,				// <
		COMMA,				// ,
		GREATER,			// >
		PERIOD,				// .
		QUESTION_MARK,		// ?

		AND_AND,			// &&
		OR_OR, 				// ||
		MINUS_MINUS,		// --
		PLUS_PLUS,			// ++
		EQUAL_EQUAL,		// ==
		LESS_LESS,			// <<
		DOT_DOT,			// ..
		GREATER_GREATER,	// >>
		
		/*#############
			Assigns
		#############*/
		AND_EQUAL,			// &=
		OR_EQUAL,			// |=
		ASTERISK_EQUAL,		// *=
		MINUS_EQUAL,		// -=
		PLUS_EQUAL,			// +=
		SLASH_EQUAL,		// /=
		PERCENT_EQUAL,		// %=
		CARET_EQUAL,		// ^=
		
		/*#############
			Compare
		#############*/
		NOT_EQUAL,			// !=
		LESS_EQUAL,			// <=
		GREATER_EQUAL,		// >=
		EQUAL_GREATER,		// =>
		
		/*###############
			Constants
		###############*/
		IDENTIFIER,
		STRING_LITERAL,
		INTEGER_LITERAL,
		FLOAT_LITERAL,
		
		/*##############
			Operators
		##############*/
		REFERENCE,
		DEREFERENCE,
		// PLUS,
		// MINUS,
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
		// LESS_EQUAL,
		// GREATER_EQUAL,
		SLICE,
		CAST,
		
		/*##############
			Keywords
		##############*/
		I8,
		U8,
		I16,
		U16,
		I32,
		U32,
		I64,
		U64,
		F32,
		F64,
		
		BYTE,
		UBYTE,
		SHORT,
		USHORT,
		INT,
		UINT,
		LONG,
		ULONG,
		FLOAT,
		DOUBLE,

		VOID,
		RUNE,
		STRING,
		BOOL,
		AUTO,

		ENUM,
		STRUCT,
		UNION,

		CONST,
		EXTERN,
		THIS,

		AS,
		ASSERT,
		REINTERPRET,
		BREAK,
		CASE,
		CONTINUE,
		DEFER,
		DELETE,
		DO,
		ELSE,
		FALL,
		FALSE,
		FOR,
		FOREACH,
		GOTO,
		IF,
		NAMESPACE,
		NEW,
		RETURN,
		SIZEOF,
		SWITCH,
		TRUE,
		TYPEDEF,
		TYPEOF,
		WHILE,
		
		/*################
			Directives
		################*/
		FLAGS,
		INLINE,
		NO_INLINE,
		REQUIRE,
		SORT_ASC,
		SORT_DESC,

		FILE_END
	};
	
	public static string TypeToString(Type type, Token token)
	{
		string text = Jolly.formatEnum(type);
		
		if(type <= Type.EQUAL_GREATER)		return "token " + text;
		if(type == Type.IDENTIFIER)			return text + " " + token?.name;
		if(type == Type.STRING_LITERAL)		return text + " " + token?._string;
		if(type == Type.INTEGER_LITERAL)	return text + " " + token?._integer;
		if(type == Type.FLOAT_LITERAL)		return text + " " + token?._float;
		if(type <= Type.CAST)				return "operator " + text;
		if(type <= Type.WHILE)				return "keyword " + text;
		if(type <= Type.SORT_DESC)			return "directive " + text;
		
		return text;
	}
	
	public override string ToString()
	{
		return TypeToString(type, this);
	}
	
	public int index;
	public Type type;
	public SourceLocation location;

	// union {
	public Token partner;
	public string _string;
	public double _float;
	public ulong _integer;
	public string name;
	// };
};

class Tokenizer
{
	static readonly Token emptyToken = new Token();
	
	string source;
	int column, line, cursor;

	bool isWhiteSpace(char ch)
	{
		return ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r';
	}

	bool isIndentifierChar(char c)
	{
		return ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
			(c >= '0' && c <= '9') || (c >= 0xA1) || c == '_');// ||
			// (diacritical >= 0xCC80 && diacritical <= 0xCFBF);
	}

	bool isHex(char ch)
	{
		return (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f');
	}

	bool isDigit(char ch)
	{
		return ch >= '0' && ch <= '9';
	}

	int incrementCursor()
	{
		++cursor;
		++column;
		return 1;
	}

	void cursorNewLine()
	{
		column = 1;
		++line;
	}

	ulong getLongBinary()
	{
		ulong result = 0;
		for (char c = source[cursor]; c == '0' || c == '1'; c = source[cursor]) {
			result = (result << 1) + c - '0';
			incrementCursor();
		}
		return result;
	}

	ulong getLongHex()
	{
		ulong result = 0;
		for (char c = source[cursor]; isHex(c); c = source[cursor]) {
			result = (result << 4) + (ulong)(c - '0' - (c > '9' ? 7 : 0) - (c > 'F' ? 32 : 0));
			incrementCursor();
		}
		return result;
	}

	ulong getLong()
	{
		ulong result = 0;
		for (char c = source[cursor]; isDigit(c); c = source[cursor]) {
			result = result * 10 + c - '0';
			incrementCursor();
		}
		return result;
	}

	void overridePrev(Token newToken)
	{
		tokens[tokens.Count - 1] = newToken;
	}

	SourceLocation getLocation()
	{
		return new SourceLocation(line, column, filename);
	}

	Token getIndentifier(Token prevToken)
	{
		Token token = new Token()
		{
			location = getLocation(),
			type = Token.Type.IDENTIFIER,
			index = tokens.Count,
		};

		int start = cursor, size = 0;
		while (isIndentifierChar(source[cursor]))
		size += incrementCursor();
		token.name = source.Substring(start, size);

		if (prevToken.type == Token.Type.HASH)
		{
			if (Lookup.directives.TryGetValue(token.name, out prevToken.type)) {
				overridePrev(prevToken);
				return null;
			}
			Jolly.addError(prevToken.location, "Unknown compiler directive");
		}
		else
		{
			if( Lookup.keywords.ContainsKey(token.name))
				token.type = Lookup.keywords[token.name];
		}
		return token;
	}

	Token getString(Token prevToken)
	{
		bool verbatim = prevToken.type == Token.Type.AT;
		incrementCursor();

		List<char> theText = new List<char>();

		while (source[cursor] != '"')
		{
			if (!verbatim && source[cursor] == '\\')
			{
				char iChar = (char)0;
				incrementCursor();
				switch (source[cursor])
				{
					case '\\': iChar = '\\'; break;
					case 'n': iChar = '\n'; break;
					case 'r': iChar = '\r'; break;
					case 't': iChar = '\r'; break;
					case '0': iChar = '\0'; break;
					case 'u':
						incrementCursor();
						theText.Add((char)getLongHex());
						continue;
					case 'a': iChar = '\a'; break;
					case 'f': iChar = '\f'; break;
					case 'v': iChar = '\v'; break;
					case 'b': iChar = '\b'; break;
					case '"': iChar = '"'; break;
					default:
						Jolly.addError(getLocation(), "Unknown escape sequence");
						break;
				}
				theText.Add(iChar);
				incrementCursor();
			}
			else if (!verbatim && source[cursor] == '\n') {
				Jolly.addError(getLocation(), "Unexpected new line");
				return null;
			}
			else if (source[cursor] == 0) {
				Jolly.addError(getLocation(), "Unexpected end of file");
				return null;
			}
			else
				theText.Add(source[cursor]);

			incrementCursor();
		} // while
		incrementCursor();

		if (verbatim) {
			prevToken.type = Token.Type.STRING_LITERAL;
			prevToken._string = new string(theText.ToArray());
			overridePrev(prevToken);
			return null;
		}
		return new Token() {
			location = getLocation(),
			_string = new string(theText.ToArray()),
			type = Token.Type.STRING_LITERAL,
			index = tokens.Count,
		};
	}

	Token getNumber(Token prevToken)
	{
		Token token = new Token() {
			type = Token.Type.INTEGER_LITERAL,
			location = getLocation(),
			index = tokens.Count,
		};
		ulong integer = getLong();
		char chr = source[cursor];
		
		if (prevToken.type == Token.Type.PERIOD || (chr == '.' && source[cursor + 1] != '.'))
		{ // Float numbers
			token.type = Token.Type.INTEGER_LITERAL;
			if (prevToken.type == Token.Type.PERIOD) {
				// token._integer = 0;
				// token.number.fracture = integer;
			}
			else {
				incrementCursor();
				// token._integer = integer;
				// token._fracture = getLong();
			}
		}
		else if (chr == 'x' || chr == 'X')
		{ // Hexadecimal numbers
			// if(integer != 0) {
			// 	token._integer = integer;
			// 	return token;
			// }
			
			
			incrementCursor();
			token._integer = getLongHex();
			return token;
		}
		else if (chr == 'b' || chr == 'B')
		{ // Binary numbers
			if(integer != 0) {
				token._integer = integer;
				return token;
			}
			incrementCursor();
			token._integer = getLongBinary();
			return token;
		}
		else
		{ // Integer numbers
			token._integer = integer;
		}
		
		if (chr == 'u' || chr == 'U') {
			incrementCursor();
			chr = source[cursor];
			// token.number.signed = false;
		}
		
		if (chr == 'l' || chr == 'L') {
			incrementCursor();
			chr = source[cursor];
			// token.number.minSize = 8;
		}
		
		if (chr == 'e' || chr == 'E')
		{ // Exponent
			incrementCursor();
			chr = source[cursor];
			bool negative = chr == '-';
			if (chr == '-' || chr == '+') {
				incrementCursor();
				chr = source[cursor];
			}

			if (!isDigit(chr))
				Jolly.addWarning(getLocation(), "Expected exponent amount");
			
			// token.number.exponent = (short)(negative ? -(short)getLong() : (short)getLong());
		}

		if (prevToken.type == Token.Type.PERIOD) {
			prevToken._float = token._float;
			prevToken.type = token.type;
			overridePrev(prevToken);
			return null;
		}
		return token;
	}

	void skipWhitespace()
		{
		// ushort diacritical;
		do
		{
			if (source[cursor] == '\n') {
				cursorNewLine();
				--column;
			}
			incrementCursor();
			// diacritical = (ushort)((source[cursor + 1] << 8) | source[cursor + 2]);
		} while (isWhiteSpace(source[cursor])); // && (diacritical < 0xCC80 || diacritical > 0xCFBF));
	}

	ushort doubleC(char a, char b)
	{
		return (ushort)((a << 8) | b);
	}

	List<Token> tokens = new List<Token>();
	Stack<Token> closureStack = new Stack<Token>();
	string filename;

	public Token[] tokenize(string text, string filename)
	{
		cursor = 0;
		source = text + "\0";
		line = column = 1;
		this.filename = filename;

		Token prevToken = emptyToken;
		for (char c = source[0]; c > 0; c = source[cursor])
		{
			if (c == '/')
			{ // Comments
				ushort doubleChar = doubleC(source[cursor], source[cursor + 1]);

				if (doubleChar == doubleC('/', '/'))
				{ //Single line comment
					while (c != '\n') { c = source[++cursor]; }
					++cursor;
					prevToken = emptyToken;
					cursorNewLine();
					continue;
				}
				else if (doubleChar == doubleC('/', '*'))
				{ // Multi line comment
					char prevC = c;
					int indent = 0;
					do
					{
						incrementCursor();
						c = source[cursor];
						if (prevC == '*' && c == '/') {
							indent--;
						} else if (prevC == '/' && c == '*') {
							indent++;
						}
						else
						{
							if (c == '\n')
								cursorNewLine();
							prevC = c;
						}
					} while (indent > 0);
					incrementCursor();
					prevToken = emptyToken;
					continue;
				}
			}

			if (isDigit(c))
			{ // Numbers
				Token tmp = getNumber(prevToken);
				if (tmp == null)
					continue; // Previous token was overwritten
				tokens.Add(tmp);
				prevToken = tmp;
			}
			else if (isIndentifierChar(source[cursor]))
			{ // Indentifiers, keywords, directives
				Token tmp = getIndentifier(prevToken);
				if (tmp == null)
					continue; // Previous token was overwritten
				prevToken = tmp;
				tokens.Add(tmp);
			}
			else if (isWhiteSpace(c))
			{ // Whitespace
				skipWhitespace();
				prevToken = emptyToken;
			}
			else if (c == '"')
			{ // Strings
				Token tmp = getString(prevToken);
				if (tmp == null)
					continue; // Previous token was overwritten
				tokens.Add(tmp);
				prevToken = tmp;
			}
			else if (c >= '!' && c <= '~')
			{ // Operators
				ushort doubleChar = doubleC(source[cursor], source[cursor + 1]);

				Token token = new Token() {
					location = getLocation(),
					index = tokens.Count,
				};

				int size = 2;
				switch (doubleChar)
				{
					case ('-' << 8) | '-': token.type = Token.Type.MINUS_MINUS; break;
					case ('-' << 8) | '=': token.type = Token.Type.MINUS_EQUAL; break;
					case ('!' << 8) | '=': token.type = Token.Type.NOT_EQUAL; break;
					case ('.' << 8) | '.': token.type = Token.Type.DOT_DOT; break;
					case ('*' << 8) | '=': token.type = Token.Type.ASTERISK_EQUAL; break;
					case ('/' << 8) | '=': token.type = Token.Type.SLASH_EQUAL; break;
					case ('&' << 8) | '&': token.type = Token.Type.AND_AND; break;
					case ('&' << 8) | '=': token.type = Token.Type.AND_EQUAL; break;
					case ('%' << 8) | '=': token.type = Token.Type.PERCENT_EQUAL; break;
					case ('+' << 8) | '+': token.type = Token.Type.PLUS_PLUS; break;
					case ('+' << 8) | '=': token.type = Token.Type.PLUS_EQUAL; break;
					case ('<' << 8) | '<': token.type = Token.Type.LESS_LESS; break;
					case ('<' << 8) | '=': token.type = Token.Type.LESS_EQUAL; break;
					case ('=' << 8) | '=': token.type = Token.Type.EQUAL_EQUAL; break;
					case ('>' << 8) | '=': token.type = Token.Type.GREATER_EQUAL; break;
					case ('>' << 8) | '>': token.type = Token.Type.GREATER_GREATER; break;
					case ('=' << 8) | '>': token.type = Token.Type.EQUAL_GREATER; break;
					case ('|' << 8) | '=': token.type = Token.Type.OR_EQUAL; break;
					case ('|' << 8) | '|': token.type = Token.Type.OR_OR; break;
					case ('^' << 8) | '=': token.type = Token.Type.CARET_EQUAL; break;
					default:
						size = 1;
						int index = c - '!' - // ! is strart of range
							(c > '/' ? 10 : 0) -// Skip 0 to 9
							(c > '@' ? 26 : 0) -// Skip A to Z
							(c > '`' ? 26 : 0);// Skip a to z
						token.type = Lookup.TOKEN[index];

						if (c == '(' || c == '{' || c == '[')
						{
							closureStack.Push(token);
						}
						else if (c == ')' || c == '}' || c == ']')
						{
							Token.Type open = (c == ')' ?
							Token.Type.PARENTHESIS_OPEN :
							(c == '}' ? Token.Type.BRACE_OPEN : Token.Type.BRACKET_OPEN));

							Token t;
							if (closureStack.Count == 0 || (t = closureStack.Pop()).type != open) {
								Jolly.addError(token.location, "To many " + Jolly.formatEnum(token.type));
								return null;
							}
							token.partner = t;
							t.partner = token;
						}
						break;
				} // Switch
				cursor += size;
				column += size;

				prevToken = token;
				tokens.Add(token);
			}
			else
			{ //Invalid character
				Jolly.addError(getLocation(), "Invalid character");
				incrementCursor();
			}
		} // for

		foreach (Token t in closureStack)
			Jolly.addError(t.location, "To many " + Jolly.formatEnum(t.type));

		tokens.Add(new Token() {
			location = getLocation(),
			type = Token.Type.FILE_END,
		});
		return tokens.ToArray();
	}
}

} // namespace Jolly
