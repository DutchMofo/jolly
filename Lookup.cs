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
		string[] names = Enum.GetNames(typeof(Token.Type));
		Token.Type[] values = Enum.GetValues(typeof(Token.Type)).Cast<Token.Type>().ToArray();

		for (int i = (int)Token.Type.I8; i <= (int)Token.Type.WHILE; ++i)
			keywords.Add(names[i].ToLower(), values[i]);
		for (int i = (int)Token.Type.FLAGS; i <= (int)Token.Type.SORT_DESC; ++i)
			directives.Add(names[i].ToLower(), values[i]);
	}
	
	public readonly static Dictionary<TT, Op>
		EXPRESSION_PRE_OP = new Dictionary<TT, Op>() {
			{ TT.PLUS_PLUS,		new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.INCREMENT	}},
			{ TT.MINUS_MINUS,	new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.DECREMENT	}},
			{ TT.ASTERISK,		new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.DEREFERENCE	}},
			{ TT.AND,			new Op { precedence = 02, valCount = 1, leftToRight = false, operation = TT.REFERENCE	}},
			
			// { TT.COMMA,			new Op { precedence = 13, valCount = 2, leftToRight = true , operation = TT.COMMA		}},
		},
		EXPRESSION_OP = new Dictionary<TT, Op>() {
			{ TT.PERIOD,			new Op { precedence = 01, valCount = 2, leftToRight = true,  operation = TT.GET_MEMBER		}},				
			{ TT.MINUS_MINUS,		new Op { precedence = 01, valCount = 1, leftToRight = true,  operation = TT.DECREMENT		}},
			{ TT.PLUS_PLUS,			new Op { precedence = 01, valCount = 1, leftToRight = true,  operation = TT.INCREMENT		}},
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
		Token.Type.EXCLAMATION,			// !
		Token.Type.QUOTE,				// "
		Token.Type.HASH,				// #
		Token.Type.DOLLAR,				// $
		Token.Type.PERCENT,				// %
		Token.Type.AND,					// &
		Token.Type.APOSTROPHE,			// '
		Token.Type.PARENTHESIS_OPEN,	// (
		Token.Type.PARENTHESIS_CLOSE,	// )
		Token.Type.ASTERISK,			// *
		Token.Type.PLUS,				// +
		Token.Type.COMMA,				// ,
		Token.Type.MINUS,				// -
		Token.Type.PERIOD,				// .
		Token.Type.SLASH,				// /
		Token.Type.COLON,				// :
		Token.Type.SEMICOLON,			// ;
		Token.Type.LESS,				// <
		Token.Type.EQUAL,				// =
		Token.Type.GREATER,				// >
		Token.Type.QUESTION_MARK,		// ?	
		Token.Type.AT,					// @
		Token.Type.BRACKET_OPEN,		// [
		Token.Type.BACKSLASH,			// \
		Token.Type.BRACKET_CLOSE,		// ]
		Token.Type.CARET,				// ^
		Token.Type.UNDEFINED,			// _ (NOT AN OPERATOR)
		Token.Type.BACK_QUOTE,			// `
		Token.Type.BRACE_OPEN,			// {
		Token.Type.PIPE,				// |
		Token.Type.BRACE_CLOSE,			// }
		Token.Type.TILDE,				// ~
	};

	public static Dictionary<string, Token.Type>
		directives = new Dictionary<string, Token.Type>(),
		keywords = new Dictionary<string, Token.Type>();

}

}