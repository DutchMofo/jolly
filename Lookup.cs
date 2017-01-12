using System;
using System.Linq;
using System.Collections.Generic;

namespace Jolly
{
using TT = Token.Type;
using OT = OperatorType;

static class Lookup 
{
	static Lookup()
	{
		string[] names = Enum.GetNames(typeof(TT));
		TT[] values = Enum.GetValues(typeof(TT)).Cast<TT>().ToArray();

		for (int i = (int)TT.I8; i <= (int)TT.WHILE; i += 1)
			keywords.Add(names[i].ToLower(), values[i]);
		for (int i = (int)TT.FLAGS; i <= (int)TT.SORT_DESC; i += 1)
			directives.Add(names[i].ToLower(), values[i]);
	}
	
	static readonly DataType[] baseTypes = new DataType[] {
		new DataType(1, 1) { name = "I8" },
		new DataType(1, 1) { name = "U8" },
		new DataType(2, 2) { name = "I16" },
		new DataType(2, 2) { name = "U16" },
		new DataType(4, 4) { name = "I32" },
		new DataType(4, 4) { name = "U32" },
		new DataType(8, 8) { name = "I64" },
		new DataType(8, 8) { name = "U64" },
		new DataType(4, 4) { name = "F32" },
		new DataType(4, 4) { name = "F64" },
		new DataType(1, 1) { name = "BYTE" },
		new DataType(1, 1) { name = "UBYTE" },
		new DataType(2, 2) { name = "SHORT" },
		new DataType(2, 2) { name = "USHORT" },
		new DataType(4, 4) { name = "INT" },
		new DataType(4, 4) { name = "UINT" },
		new DataType(8, 8) { name = "LONG" },
		new DataType(8, 8) { name = "ULONG" },
		new DataType(4, 4) { name = "FLOAT" },
		new DataType(4, 4) { name = "DOUBLE" },
		new DataType(0, 0) { name = "VOID" },
		new DataType(2, 2) { name = "RUNE" },
		new DataType(16, 8) { name ="STRING" },
		new DataType(1, 1) { name = "BOOL" },
		new DataType(0, 0) { name = "AUTO" },
	};

	public static DataType getBaseType(TT type)
	{
		return baseTypes[type - TT.I8];
	}

	public readonly static Dictionary<TT, Op>
		EXPRESSION_PRE_OP = new Dictionary<TT, Op>() {
			{ TT.ASTERISK,			new Op(02, 1, false, OT.DEREFERENCE		)},
			{ TT.AND,				new Op(02, 1, false, OT.REFERENCE		)},
		},
		EXPRESSION_OP = new Dictionary<TT, Op>() {
			{ TT.PERIOD,			new Op(01, 2, true,  OT.GET_MEMBER		)},
			{ TT.EXCLAMATION,		new Op(02, 1, false, OT.LOGIC_NOT		)},
			{ TT.TILDE,				new Op(02, 1, false, OT.BIT_NOT			)},
			{ TT.NEW,				new Op(02, 1, false, OT.NEW				)},
			{ TT.DELETE,			new Op(02, 1, false, OT.DELETE			)},
			
			{ TT.AS,				new Op(03, 2, true,  OT.CAST			)},
			{ TT.PERCENT,			new Op(03, 2, true,  OT.MODULO			)},
			{ TT.ASTERISK,			new Op(03, 2, true,  OT.MULTIPLY		)},
			{ TT.SLASH,				new Op(03, 2, true,  OT.DIVIDE			)},
			{ TT.MINUS,				new Op(04, 2, true,  OT.MINUS			)},
			{ TT.PLUS,				new Op(04, 2, true,  OT.PLUS			)},
			{ TT.GREATER_GREATER,	new Op(05, 2, true,  OT.SHIFT_RIGHT		)},
			{ TT.LESS_LESS,			new Op(05, 2, true,  OT.SHIFT_LEFT		)},
			{ TT.LESS_EQUAL,		new Op(06, 2, true,  OT.LESS_EQUAL		)},
			{ TT.LESS,				new Op(06, 2, true,  OT.LESS			)},
			{ TT.GREATER_EQUAL,		new Op(06, 2, true,  OT.GREATER_EQUAL	)},
			{ TT.GREATER,			new Op(06, 2, true,  OT.GREATER			)},
			{ TT.NOT_EQUAL,			new Op(07, 2, true,  OT.NOT_EQUAL		)},
			{ TT.EQUAL_EQUAL,		new Op(07, 2, true,  OT.EQUAL_TO		)},
			{ TT.AND,				new Op(08, 2, true,  OT.BIT_AND			)},
			{ TT.CARET,				new Op(09, 2, true,  OT.BIT_XOR			)},
			{ TT.PIPE,				new Op(10, 2, true,  OT.BIT_OR			)},
			{ TT.AND_AND,			new Op(11, 2, true,  OT.LOGIC_AND		)},
			{ TT.OR_OR,				new Op(12, 2, true,  OT.LOGIC_OR		)},
			
			{ TT.COMMA,				new Op(13, 2, true,  OT.COMMA			)},
			{ TT.COLON,				new Op(13, 2, false, OT.CAST			)},
			
			{ TT.EQUAL,				new Op(14, 2, false, OT.ASSIGN			)},
			{ TT.AND_EQUAL,			new Op(14, 2, false, OT.AND_ASSIGN		)},
			{ TT.SLASH_EQUAL,		new Op(14, 2, false, OT.SLASH_ASSIGN	)},
			{ TT.MINUS_EQUAL,		new Op(14, 2, false, OT.MINUS_ASSIGN	)},
			{ TT.PERCENT_EQUAL,		new Op(14, 2, false, OT.PERCENT_ASSIGN	)},
			{ TT.ASTERISK_EQUAL,	new Op(14, 2, false, OT.ASTERISK_ASSIGN	)},
			{ TT.OR_EQUAL,			new Op(14, 2, false, OT.OR_ASSIGN		)},
			{ TT.PLUS_EQUAL,		new Op(14, 2, false, OT.PLUS_ASSIGN		)},
			{ TT.CARET_EQUAL,		new Op(14, 2, false, OT.CARET_ASSIGN	)},
		},
		DEFINE_OP = new Dictionary<TT, Op>() {
			{ TT.PERIOD, EXPRESSION_OP[TT.PERIOD] },
			{ TT.COMMA, EXPRESSION_OP[TT.COMMA] },
		},
		DEFINE_PRE_OP = new Dictionary<TT, Op>() {
			// { TT.COMMA, EXPRESSION_OP[TT.COMMA] }
		};
	
	//In ascii order
	public static readonly Token.Type[] TOKEN = {
		TT.EXCLAMATION,			// !
		TT.QUOTE,				// "
		TT.HASH,				// #
		TT.DOLLAR,				// $
		TT.PERCENT,				// %
		TT.AND,					// &
		TT.APOSTROPHE,			// '
		TT.PARENTHESIS_OPEN,	// (
		TT.PARENTHESIS_CLOSE,	// )
		TT.ASTERISK,			// *
		TT.PLUS,				// +
		TT.COMMA,				// ,
		TT.MINUS,				// -
		TT.PERIOD,				// .
		TT.SLASH,				// /
		TT.COLON,				// :
		TT.SEMICOLON,			// ;
		TT.LESS,				// <
		TT.EQUAL,				// =
		TT.GREATER,				// >
		TT.QUESTION_MARK,		// ?	
		TT.AT,					// @
		TT.BRACKET_OPEN,		// [
		TT.BACKSLASH,			// \
		TT.BRACKET_CLOSE,		// ]
		TT.CARET,				// ^
		TT.UNDEFINED,			// _ (NOT AN OPERATOR)
		TT.BACK_QUOTE,			// `
		TT.BRACE_OPEN,			// {
		TT.PIPE,				// |
		TT.BRACE_CLOSE,			// }
		TT.TILDE,				// ~
	};

	public static Dictionary<string, Token.Type>
		directives = new Dictionary<string, Token.Type>(),
		keywords = new Dictionary<string, Token.Type>();

}

}