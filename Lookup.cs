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

	public static readonly DataType[] baseTypes = new DataType[] {
		new DataType(1, 1),		// I8,
		new DataType(1, 1),		// U8,
		new DataType(2, 2),		// I16,
		new DataType(2, 2),		// U16,
		new DataType(4, 4),		// I32,
		new DataType(4, 4),		// U32,
		new DataType(8, 8),		// I64,
		new DataType(8, 8),		// U64,
		new DataType(4, 4),		// F32,
		new DataType(4, 4),		// F64,
		new DataType(1, 1),		// BYTE,
		new DataType(1, 1),		// UBYTE,
		new DataType(2, 2),		// SHORT,
		new DataType(2, 2),		// USHORT,
		new DataType(4, 4),		// INT,
		new DataType(4, 4),		// UINT,
		new DataType(8, 8),		// LONG,
		new DataType(8, 8),		// ULONG,
		new DataType(4, 4),		// FLOAT,
		new DataType(4, 4),		// DOUBLE,
		new DataType(0, 0),		// VOID,
		new DataType(2, 2),		// RUNE,
		new DataType(16, 8),	// STRING,
		new DataType(1, 1),		// BOOL,
		new DataType(0, 0),		// AUTO,
	};
	
	public readonly static Dictionary<TT, Op>
		EXPRESSION_PRE_OP = new Dictionary<TT, Op>() {
			// { TT.PLUS_PLUS,		new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.INCREMENT	}},
			// { TT.MINUS_MINUS,	new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.DECREMENT	}},
			{ TT.ASTERISK,		new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.DEREFERENCE	}},
			{ TT.AND,			new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.REFERENCE	}},
			
			// { TT.COMMA,			new Op { precedence = 13, valCount = 2, leftToRight = true , operation = TT.COMMA		}},
		},
		EXPRESSION_OP = new Dictionary<TT, Op>() {
			{ TT.PERIOD,			new Op { precedence = 01, valCount = 2, leftToRight = true,  operation = TT.GET_MEMBER		}},				
			// { TT.MINUS_MINUS,		new Op { precedence = 01, valCount = 1, leftToRight = true,  operation = TT.DECREMENT		}},
			// { TT.PLUS_PLUS,			new Op { precedence = 01, valCount = 1, leftToRight = true,  operation = TT.INCREMENT		}},
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
			
			{ TT.AND_EQUAL,			new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.AND_EQUAL		}},
			{ TT.EQUAL,				new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.ASSIGN			}},
			{ TT.SLASH_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.SLASH_EQUAL		}},
			{ TT.MINUS_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.MINUS_EQUAL		}},
			{ TT.PERCENT_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.PERCENT_EQUAL	}},
			{ TT.ASTERISK_EQUAL,	new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.ASTERISK_EQUAL	}},
			{ TT.OR_EQUAL,			new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.OR_EQUAL		}},
			{ TT.PLUS_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.PLUS_EQUAL		}},
			{ TT.CARET_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = TT.CARET_EQUAL		}},
			
			{ TT.COMMA,				new Op { precedence = 13, valCount = 2, leftToRight = true , operation = TT.COMMA			}},
			
			// { TT.COLON,				new Op { precedence = 14, valCount = 2, leftToRight = true,  operation = TT.SLICE			}},
		},
		DEFINE_OP = new Dictionary<TT, Op>() {
			{ TT.COMMA, EXPRESSION_OP[TT.COMMA] }
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