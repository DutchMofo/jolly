using System;
using System.Linq;
using System.Collections.Generic;

namespace Jolly
{
using TT = Token.Type;
	
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
		new DataType(1, 1){ is_baseType = true },	// I8,
		new DataType(1, 1){ is_baseType = true },	// U8,
		new DataType(2, 2){ is_baseType = true },	// I16,
		new DataType(2, 2){ is_baseType = true },	// U16,
		new DataType(4, 4){ is_baseType = true },	// I32,
		new DataType(4, 4){ is_baseType = true },	// U32,
		new DataType(8, 8){ is_baseType = true },	// I64,
		new DataType(8, 8){ is_baseType = true },	// U64,
		new DataType(4, 4){ is_baseType = true },	// F32,
		new DataType(4, 4){ is_baseType = true },	// F64,
		new DataType(1, 1){ is_baseType = true },	// BYTE,
		new DataType(1, 1){ is_baseType = true },	// UBYTE,
		new DataType(2, 2){ is_baseType = true },	// SHORT,
		new DataType(2, 2){ is_baseType = true },	// USHORT,
		new DataType(4, 4){ is_baseType = true },	// INT,
		new DataType(4, 4){ is_baseType = true },	// UINT,
		new DataType(8, 8){ is_baseType = true },	// LONG,
		new DataType(8, 8){ is_baseType = true },	// ULONG,
		new DataType(4, 4){ is_baseType = true },	// FLOAT,
		new DataType(4, 4){ is_baseType = true },	// DOUBLE,
		new DataType(0, 0){ is_baseType = true },	// VOID,
		new DataType(2, 2){ is_baseType = true },	// RUNE,
		new DataType(16, 8){ is_baseType = true },	// STRING,
		new DataType(1, 1){ is_baseType = true },	// BOOL,
		new DataType(0, 0){ is_baseType = true },	// AUTO,
	};

	public static DataType getBaseType(TT type)
	{
		return baseTypes[type - TT.I8];
	}

	public readonly static Dictionary<TT, Op>
		EXPRESSION_PRE_OP = new Dictionary<TT, Op>() {
			{ TT.ASTERISK,		new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.DEREFERENCE	}},
			{ TT.AND,			new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.REFERENCE	}},
		},
		EXPRESSION_OP = new Dictionary<TT, Op>() {
			{ TT.PERIOD,			new Op { precedence = 01, valCount = 2, leftToRight = true,  operation = TT.GET_MEMBER		}},
			{ TT.EXCLAMATION,		new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.LOGIC_NOT		}},
			{ TT.TILDE,				new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.BIT_NOT			}},
			{ TT.NEW,				new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.NEW				}},
			{ TT.DELETE,			new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.DELETE			}},
			{ TT.AS,				new Op { precedence = 03, valCount = 2, leftToRight = true,  operation = TT.CAST			}},
			{ TT.PERCENT,			new Op { precedence = 03, valCount = 2, leftToRight = true,  operation = TT.MODULO			}},
			{ TT.ASTERISK,			new Op { precedence = 03, valCount = 2, leftToRight = true,  operation = TT.MULTIPLY		}},
			{ TT.SLASH,				new Op { precedence = 03, valCount = 2, leftToRight = true,  operation = TT.DIVIDE			}},
			{ TT.MINUS,				new Op { precedence = 04, valCount = 2, leftToRight = true,  operation = TT.MINUS			}},
			{ TT.PLUS,				new Op { precedence = 04, valCount = 2, leftToRight = true,  operation = TT.PLUS			}},
			{ TT.GREATER_GREATER,	new Op { precedence = 05, valCount = 2, leftToRight = true,  operation = TT.SHIFT_RIGHT		}},
			{ TT.LESS_LESS,			new Op { precedence = 05, valCount = 2, leftToRight = true,  operation = TT.SHIFT_LEFT		}},
			{ TT.LESS_EQUAL,		new Op { precedence = 06, valCount = 2, leftToRight = true,  operation = TT.LESS_EQUAL		}},
			{ TT.LESS,				new Op { precedence = 06, valCount = 2, leftToRight = true,  operation = TT.LESS			}},
			{ TT.GREATER_EQUAL,		new Op { precedence = 06, valCount = 2, leftToRight = true,  operation = TT.GREATER_EQUAL	}},
			{ TT.GREATER,			new Op { precedence = 06, valCount = 2, leftToRight = true,  operation = TT.GREATER			}},
			{ TT.NOT_EQUAL,			new Op { precedence = 07, valCount = 2, leftToRight = true,  operation = TT.NOT_EQUAL		}},
			{ TT.EQUAL_EQUAL,		new Op { precedence = 07, valCount = 2, leftToRight = true,  operation = TT.EQUAL_TO		}},
			{ TT.AND,				new Op { precedence = 08, valCount = 2, leftToRight = true,  operation = TT.BIT_AND			}},
			{ TT.CARET,				new Op { precedence = 09, valCount = 2, leftToRight = true,  operation = TT.BIT_XOR			}},
			{ TT.PIPE,				new Op { precedence = 10, valCount = 2, leftToRight = true,  operation = TT.BIT_OR			}},
			{ TT.AND_AND,			new Op { precedence = 11, valCount = 2, leftToRight = true,  operation = TT.LOGIC_AND		}},
			{ TT.OR_OR,				new Op { precedence = 12, valCount = 2, leftToRight = true,  operation = TT.LOGIC_OR		}},
			
			{ TT.COMMA,				new Op { precedence = 13, valCount = 2, leftToRight = true , operation = TT.COMMA			}},
			
			{ TT.AND_EQUAL,			new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.AND_EQUAL		}},
			{ TT.EQUAL,				new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.ASSIGN			}},
			{ TT.SLASH_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.SLASH_EQUAL		}},
			{ TT.MINUS_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.MINUS_EQUAL		}},
			{ TT.PERCENT_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.PERCENT_EQUAL	}},
			{ TT.ASTERISK_EQUAL,	new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.ASTERISK_EQUAL	}},
			{ TT.OR_EQUAL,			new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.OR_EQUAL		}},
			{ TT.PLUS_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.PLUS_EQUAL		}},
			{ TT.CARET_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.CARET_EQUAL		}},
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