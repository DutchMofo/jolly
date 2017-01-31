using System;
using System.Linq;
using System.Collections.Generic;

namespace Jolly
{
using TT = Token.Type;
using OT = OperatorType;
using Op = ExpressionParser.Op;

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
	
	const DataType.Flags BASE_TYPE = DataType.Flags.BASE_TYPE | DataType.Flags.INSTANTIABLE, INSTANTIABLE = DataType.Flags.INSTANTIABLE;
	
	public static readonly DataType
		I8     = new DataType(1, 1, BASE_TYPE)    { name = "i8" },
		U8     = new DataType(1, 1, BASE_TYPE)    { name = "u8" },
		I16    = new DataType(2, 2, BASE_TYPE)    { name = "i16" },
		U16    = new DataType(2, 2, BASE_TYPE)    { name = "u16" },
		I32    = new DataType(4, 4, BASE_TYPE)    { name = "i32" },
		U32    = new DataType(4, 4, BASE_TYPE)    { name = "u32" },
		I64    = new DataType(8, 8, BASE_TYPE)    { name = "i64" },
		U64    = new DataType(8, 8, BASE_TYPE)    { name = "u64" },
		F32    = new DataType(4, 4, BASE_TYPE)    { name = "f32" },
		F64    = new DataType(4, 4, BASE_TYPE)    { name = "f64" },
		VOID   = new DataType(0, 0, BASE_TYPE),
		STRING = new DataType(16,8, INSTANTIABLE),
		BOOL   = new DataType(1, 1, BASE_TYPE)    { name = "i1" },
		AUTO   = new DataType(0, 0, 0),
		TUPLE  = new DataType(0, 0, 0);
	
	static readonly DataType[] baseTypes = new DataType[] {
		I8,  I8,
		U8,  U8,
		I16, I16,
		U16, U16,
		I32, I32,
		U32, U32,
		I64, I64,
		U64, U64,
		F32, F32,
		F64, F64,
		VOID,
		STRING,
		BOOL,
		AUTO,
	};

	public static DataType getBaseType(TT type)
	{
		return baseTypes[(type < TT.VOID) ? (type - TT.I8) & (~1) : type - TT.I8];
	}
	
	public readonly static Dictionary<TT, Op>
		OPERATORS = new Dictionary<TT, Op>() {
			{ TT.PERIOD,			new Op(01, 2, true,  OT.GET_MEMBER		)},
			
			{ TT.EXCLAMATION,		new Op(02, 1, false, OT.LOGIC_NOT		)},
			{ TT.TILDE,				new Op(02, 1, false, OT.BIT_NOT			)},
			{ TT.NEW,				new Op(02, 1, false, OT.NEW				)},
			{ TT.DELETE,			new Op(02, 1, false, OT.DELETE			)},
			
			{ TT.AS,				new Op(03, 2, true,  OT.CAST			)},
			{ TT.PERCENT,			new Op(03, 2, true,  OT.MODULO			)},
			{ TT.ASTERISK,			new Op(03, 2, true,  OT.MULTIPLY,  true	)},
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
			{ TT.AND_AND,			new Op(11, 2, true,  OT.LOGIC_AND, true	)},
			{ TT.OR_OR,				new Op(12, 2, true,  OT.LOGIC_OR,  true	)},
			
			// { TT.COLON_COLON,		new Op(13, 2, false, OT.CAST			)},
			// { TT.COLON_TILDE,		new Op(13, 2, false, OT.BITCAST			)},
			
			{ TT.QUESTION_MARK,		new Op(14, 2, false, OT.TERNARY,   true	)},
			{ TT.COLON,				new Op(14, 2, true,  OT.COLON,     true )},
			
			{ TT.COMMA,				new Op(15, 2, true,  OT.COMMA			)},
			
			{ TT.EQUAL,				new Op(16, 2, false, OT.ASSIGN			)},
			{ TT.AND_EQUAL,			new Op(16, 2, false, OT.AND_ASSIGN		)},
			{ TT.SLASH_EQUAL,		new Op(16, 2, false, OT.SLASH_ASSIGN	)},
			{ TT.MINUS_EQUAL,		new Op(16, 2, false, OT.MINUS_ASSIGN	)},
			{ TT.PERCENT_EQUAL,		new Op(16, 2, false, OT.PERCENT_ASSIGN	)},
			{ TT.ASTERISK_EQUAL,	new Op(16, 2, false, OT.ASTERISK_ASSIGN	)},
			{ TT.OR_EQUAL,			new Op(16, 2, false, OT.OR_ASSIGN		)},
			{ TT.PLUS_EQUAL,		new Op(16, 2, false, OT.PLUS_ASSIGN		)},
			{ TT.CARET_EQUAL,		new Op(16, 2, false, OT.CARET_ASSIGN	)},
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