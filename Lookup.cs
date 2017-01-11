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
		// for(int i = 0; i < baseTypes.Length; i += 1)
		// 	baseTypeScope.Add(baseTypeText[i], /*baseTypes[i]*/ null);
	}
	
	static readonly NameFlags tFlag = NameFlags.IS_BASETYPE | NameFlags.IS_TYPE;
	
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
			{ TT.ASTERISK,		new Op { precedence = 02, valCount = 1, leftToRight = false, operation = OT.DEREFERENCE	}},
			{ TT.AND,			new Op { precedence = 02, valCount = 1, leftToRight = false, operation = OT.REFERENCE	}},
		},
		EXPRESSION_OP = new Dictionary<TT, Op>() {
			{ TT.PERIOD,			new Op { precedence = 01, valCount = 2, leftToRight = true,  operation = OT.GET_MEMBER		}},
			{ TT.EXCLAMATION,		new Op { precedence = 02, valCount = 1, leftToRight = false, operation = OT.LOGIC_NOT		}},
			{ TT.TILDE,				new Op { precedence = 02, valCount = 1, leftToRight = false, operation = OT.BIT_NOT			}},
			{ TT.NEW,				new Op { precedence = 02, valCount = 1, leftToRight = false, operation = OT.NEW				}},
			{ TT.DELETE,			new Op { precedence = 02, valCount = 1, leftToRight = false, operation = OT.DELETE			}},
			{ TT.AS,				new Op { precedence = 03, valCount = 2, leftToRight = true,  operation = OT.CAST			}},
			{ TT.PERCENT,			new Op { precedence = 03, valCount = 2, leftToRight = true,  operation = OT.MODULO			}},
			{ TT.ASTERISK,			new Op { precedence = 03, valCount = 2, leftToRight = true,  operation = OT.MULTIPLY		}},
			{ TT.SLASH,				new Op { precedence = 03, valCount = 2, leftToRight = true,  operation = OT.DIVIDE			}},
			{ TT.MINUS,				new Op { precedence = 04, valCount = 2, leftToRight = true,  operation = OT.MINUS			}},
			{ TT.PLUS,				new Op { precedence = 04, valCount = 2, leftToRight = true,  operation = OT.PLUS			}},
			{ TT.GREATER_GREATER,	new Op { precedence = 05, valCount = 2, leftToRight = true,  operation = OT.SHIFT_RIGHT		}},
			{ TT.LESS_LESS,			new Op { precedence = 05, valCount = 2, leftToRight = true,  operation = OT.SHIFT_LEFT		}},
			{ TT.LESS_EQUAL,		new Op { precedence = 06, valCount = 2, leftToRight = true,  operation = OT.LESS_EQUAL		}},
			{ TT.LESS,				new Op { precedence = 06, valCount = 2, leftToRight = true,  operation = OT.LESS			}},
			{ TT.GREATER_EQUAL,		new Op { precedence = 06, valCount = 2, leftToRight = true,  operation = OT.GREATER_EQUAL	}},
			{ TT.GREATER,			new Op { precedence = 06, valCount = 2, leftToRight = true,  operation = OT.GREATER			}},
			{ TT.NOT_EQUAL,			new Op { precedence = 07, valCount = 2, leftToRight = true,  operation = OT.NOT_EQUAL		}},
			{ TT.EQUAL_EQUAL,		new Op { precedence = 07, valCount = 2, leftToRight = true,  operation = OT.EQUAL_TO		}},
			{ TT.AND,				new Op { precedence = 08, valCount = 2, leftToRight = true,  operation = OT.BIT_AND			}},
			{ TT.CARET,				new Op { precedence = 09, valCount = 2, leftToRight = true,  operation = OT.BIT_XOR			}},
			{ TT.PIPE,				new Op { precedence = 10, valCount = 2, leftToRight = true,  operation = OT.BIT_OR			}},
			{ TT.AND_AND,			new Op { precedence = 11, valCount = 2, leftToRight = true,  operation = OT.LOGIC_AND		}},
			{ TT.OR_OR,				new Op { precedence = 12, valCount = 2, leftToRight = true,  operation = OT.LOGIC_OR		}},
			
			{ TT.COMMA,				new Op { precedence = 13, valCount = 2, leftToRight = true , operation = OT.COMMA			}},
			
			{ TT.AND_EQUAL,			new Op { precedence = 14, valCount = 2, leftToRight = false, operation = OT.AND_EQUAL		}},
			{ TT.EQUAL,				new Op { precedence = 14, valCount = 2, leftToRight = false, operation = OT.ASSIGN			}},
			{ TT.SLASH_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = OT.SLASH_EQUAL		}},
			{ TT.MINUS_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = OT.MINUS_EQUAL		}},
			{ TT.PERCENT_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = OT.PERCENT_EQUAL	}},
			{ TT.ASTERISK_EQUAL,	new Op { precedence = 14, valCount = 2, leftToRight = false, operation = OT.ASTERISK_EQUAL	}},
			{ TT.OR_EQUAL,			new Op { precedence = 14, valCount = 2, leftToRight = false, operation = OT.OR_EQUAL		}},
			{ TT.PLUS_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = OT.PLUS_EQUAL		}},
			{ TT.CARET_EQUAL,		new Op { precedence = 14, valCount = 2, leftToRight = false, operation = OT.CARET_EQUAL		}},
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