using System;
using System.Linq;
using System.Collections.Generic;

namespace Jolly
{
using TT = Token.Type;
using NT = AST_Node.Type;
using Operator = ExpressionParser.Operator;

using Cast = Func<IR, DataType, IR>;

static class Lookup 
{
	static Lookup()
	{
		string[] names = Enum.GetNames(typeof(TT));
		TT[] values = Enum.GetValues(typeof(TT)).Cast<TT>().ToArray();

		for (int i = (int)TT.I1; i <= (int)TT.WHILE; i += 1)
			keywords.Add(names[i].ToLower(), values[i]);
		for (int i = (int)TT.FLAGS; i <= (int)TT.SORT_DESC; i += 1)
			directives.Add(names[i].ToLower(), values[i]);
	}
	
	const DataType.Flags
		BASE_TYPE = DataType.Flags.BASE_TYPE | DataType.Flags.INSTANTIABLE | DataType.Flags.SIGNED,
		U_BASE_TYPE = DataType.Flags.BASE_TYPE | DataType.Flags.INSTANTIABLE,
		INSTANTIABLE = DataType.Flags.INSTANTIABLE;
	
	public static readonly DataType
		I1       = new DataType{ name = "i1",     size = 1,  align = 1, typeID = 1,  flags = BASE_TYPE },
		I8       = new DataType{ name = "i8",     size = 1,  align = 1, typeID = 3,  flags = BASE_TYPE },
		I16      = new DataType{ name = "i16",    size = 2,  align = 2, typeID = 5,  flags = BASE_TYPE },
		I32      = new DataType{ name = "i32",    size = 4,  align = 4, typeID = 7,  flags = BASE_TYPE },
		I64      = new DataType{ name = "i64",    size = 8,  align = 8, typeID = 9,  flags = BASE_TYPE },
		F32      = new DataType{ name = "f32",    size = 4,  align = 4, typeID = 10, flags = BASE_TYPE },
		F64      = new DataType{ name = "f64",    size = 4,  align = 4, typeID = 11, flags = BASE_TYPE },
		VOID     = new DataType{ name = "void",   size = 0,  align = 0, typeID = 12, flags = BASE_TYPE },
		STRING   = new DataType{ name = "string", size = 16, align = 8, typeID = 13, flags = INSTANTIABLE },
		AUTO     = new DataType{ name = "auto",   size = 0,  align = 0, typeID = 14, flags = 0 },
		TUPLE    = new DataType{ name = "tuple",  size = 0,  align = 0, typeID = 15, flags = 0 };
	public static DataType
		VOID_PTR = new DataType_Reference(new DataType{ size = 0, align = 0, typeID = 16,  flags = BASE_TYPE });
	
	// TODO: Change this garbage, currently the order is bound to Token.Type order
	static readonly DataType[] baseTypes = new DataType[] {
		I1,  I1,
		I8,  I8,
		I16, I16,
		I32, I32,
		I64, I64,
		F32, F32,
		F64, F64,
		VOID,
		STRING,
		AUTO,
	};

	public static DataType getBaseType(TT type)
	{
		return baseTypes[(type < TT.VOID) ? (type - TT.I1) & (~1) : type - TT.I1];
	}
	
	public readonly static Dictionary<TT, Operator>
		OPERATORS = new Dictionary<TT, Operator>() {
			{ TT.PERIOD,          new Operator(01, 2, true,  NT.GET_MEMBER        ){ canStillDefine = true }},
			{ TT.EXCLAMATION,     new Operator(02, 1, false, NT.LOGIC_NOT         )},
			{ TT.TILDE,           new Operator(02, 1, false, NT.BIT_NOT           )},
			{ TT.NEW,             new Operator(02, 1, false, NT.NEW               )},
			{ TT.DELETE,          new Operator(02, 1, false, NT.DELETE            )},
			{ TT.AS,              new Operator(03, 2, true,  NT.CAST              )},
			{ TT.PERCENT,         new Operator(03, 2, true,  NT.MODULO            )},
			{ TT.ASTERISK,        new Operator(03, 2, true,  NT.MULTIPLY,    true ){ canStillDefine = true }},
			{ TT.SLASH,           new Operator(03, 2, true,  NT.DIVIDE            )},
			{ TT.MINUS,           new Operator(04, 2, true,  NT.SUBTRACT          )},
			{ TT.PLUS,            new Operator(04, 2, true,  NT.ADD               )},
			{ TT.GREATER_GREATER, new Operator(05, 2, true,  NT.SHIFT_RIGHT       )},
			{ TT.LESS_LESS,       new Operator(05, 2, true,  NT.SHIFT_LEFT        )},
			{ TT.LESS_EQUAL,      new Operator(06, 2, true,  NT.LESS_EQUAL        )},
			{ TT.LESS,            new Operator(06, 2, true,  NT.LESS              )},
			{ TT.GREATER_EQUAL,   new Operator(06, 2, true,  NT.GREATER_EQUAL     )},
			{ TT.GREATER,         new Operator(06, 2, true,  NT.GREATER           )},
			{ TT.NOT_EQUAL,       new Operator(07, 2, true,  NT.NOT_EQUAL         )},
			{ TT.EQUAL_EQUAL,     new Operator(07, 2, true,  NT.EQUAL_TO          )},
			{ TT.AND,             new Operator(08, 2, true,  NT.BIT_AND           )},
			{ TT.CARET,           new Operator(09, 2, true,  NT.BIT_XOR           )},
			{ TT.PIPE,            new Operator(10, 2, true,  NT.BIT_OR            )},
			{ TT.AND_AND,         new Operator(11, 2, true,  NT.LOGIC_AND,   true )},
			{ TT.OR_OR,           new Operator(12, 2, true,  NT.LOGIC_OR,    true )},
			{ TT.COLON_TILDE,     new Operator(13, 2, false, NT.REINTERPRET, true )},
			{ TT.QUESTION_MARK,   new Operator(14, 2, false, NT.TERNARY,     true ){ canStillDefine = true }},
			{ TT.COLON,           new Operator(14, 2, true,  NT.COLON,       true )},
			{ TT.COMMA,           new Operator(15, 2, true,  NT.COMMA             ){ canStillDefine = true }},
			{ TT.EQUAL,           new Operator(16, 2, false, NT.ASSIGN            )},
			{ TT.AND_EQUAL,       new Operator(16, 2, false, NT.AND_ASSIGN        )},
			{ TT.SLASH_EQUAL,     new Operator(16, 2, false, NT.SLASH_ASSIGN      )},
			{ TT.MINUS_EQUAL,     new Operator(16, 2, false, NT.SUBTRACT_ASSIGN   )},
			{ TT.PERCENT_EQUAL,   new Operator(16, 2, false, NT.PERCENT_ASSIGN    )},
			{ TT.ASTERISK_EQUAL,  new Operator(16, 2, false, NT.MULTIPLY_ASSIGN   )},
			{ TT.OR_EQUAL,        new Operator(16, 2, false, NT.OR_ASSIGN         )},
			{ TT.PLUS_EQUAL,      new Operator(16, 2, false, NT.ADD_ASSIGN        )},
			{ TT.CARET_EQUAL,     new Operator(16, 2, false, NT.CARET_ASSIGN      )},
		};
	
	//In ascii order
	public static readonly Token.Type[] TOKEN = {
		TT.EXCLAMATION,       // !
		TT.QUOTE,             // "
		TT.HASH,              // #
		TT.DOLLAR,            // $
		TT.PERCENT,           // %
		TT.AND,               // &
		TT.APOSTROPHE,        // '
		TT.PARENTHESIS_OPEN,  // (
		TT.PARENTHESIS_CLOSE, // )
		TT.ASTERISK,          // *
		TT.PLUS,              // +
		TT.COMMA,             // ,
		TT.MINUS,             // -
		TT.PERIOD,            // .
		TT.SLASH,             // /
		TT.COLON,             // :
		TT.SEMICOLON,         // ;
		TT.LESS,              // <
		TT.EQUAL,             // =
		TT.GREATER,           // >
		TT.QUESTION_MARK,     // ?	
		TT.AT,                // @
		TT.BRACKET_OPEN,      // [
		TT.BACKSLASH,         // \
		TT.BRACKET_CLOSE,     // ]
		TT.CARET,             // ^
		TT.UNDEFINED,         // _ (NOT AN OPERATOR)
		TT.BACK_QUOTE,        // `
		TT.BRACE_OPEN,        // {
		TT.PIPE,              // |
		TT.BRACE_CLOSE,       // }
		TT.TILDE,             // ~
	};

	public static Dictionary<string, Token.Type>
		directives = new Dictionary<string, Token.Type>(),
		keywords = new Dictionary<string, Token.Type>();
	
	public struct Pair
	{
		public DataType a, b;
		public override bool Equals (object obj) { var other = (Pair)obj; return a == other.a && b == other.b; }
		public override int GetHashCode() => a.GetHashCode() << 13 + b.GetHashCode();	
	}
	
	
	public class CastLookup : Dictionary<Pair, Cast>
	{
		public void Add(DataType a, DataType b)
		{
			Add(a, b, casts[new Pair{ a = a, b = b }]);
		}
		
		public void Add(DataType a, DataType b, Cast cast)
		{
			Add(new Pair{ a = a, b = b }, cast);
		}
		
		public bool get(DataType a, DataType b, out Cast cast)
		{
			return TryGetValue(new Pair{ a = a, b = b }, out cast);
		}
	}
	
	public static CastLookup casts = new CastLookup() {
		{ I1,  I8,  (a,b) => IR.cast<IR_Extend>    (a,b, (c,d) =>   (long)((bool)c ? 1 : 0)) },
		{ I1,  I16, (a,b) => IR.cast<IR_Extend>    (a,b, (c,d) =>   (long)((bool)c ? 1 : 0)) },
		{ I1,  I32, (a,b) => IR.cast<IR_Extend>    (a,b, (c,d) =>   (long)((bool)c ? 1 : 0)) },
		{ I1,  I64, (a,b) => IR.cast<IR_Extend>    (a,b, (c,d) =>   (long)((bool)c ? 1 : 0)) },
		{ I1,  F32, (a,b) => IR.cast<IR_IntToFloat>(a,b, (c,d) => (double)((bool)c ? 1 : 0)) },
		{ I1,  F64, (a,b) => IR.cast<IR_IntToFloat>(a,b, (c,d) => (double)((bool)c ? 1 : 0)) },
		
		{ I8,  I1,  (a,b) => IR.cast<IR_Truncate>  (a,b, (c,d) => (long)c != 0) },
		{ I8,  I16, (a,b) => IR.cast<IR_Extend>    (a,b, (c,d) => c) },
		{ I8,  I32, (a,b) => IR.cast<IR_Extend>    (a,b, (c,d) => c) },
		{ I8,  I64, (a,b) => IR.cast<IR_Extend>    (a,b, (c,d) => c) },
		{ I8,  F32, (a,b) => IR.cast<IR_IntToFloat>(a,b, (c,d) => c) },
		{ I8,  F64, (a,b) => IR.cast<IR_IntToFloat>(a,b, (c,d) => c) },
		
		{ I16, I1,  (a,b) => IR.cast<IR_Truncate>  (a,b, (c,d) => (long)c != 0) },
		{ I16, I8,  (a,b) => IR.cast<IR_Truncate>  (a,b, (c,d) => c) },
		{ I16, I32, (a,b) => IR.cast<IR_Extend>    (a,b, (c,d) => c) },
		{ I16, I64, (a,b) => IR.cast<IR_Extend>    (a,b, (c,d) => c) },
		{ I16, F32, (a,b) => IR.cast<IR_IntToFloat>(a,b, (c,d) => c) },
		{ I16, F64, (a,b) => IR.cast<IR_IntToFloat>(a,b, (c,d) => c) },
		
		{ I32, I1,  (a,b) => IR.cast<IR_Truncate>  (a,b, (c,d) => (long)c != 0) },
		{ I32, I8,  (a,b) => IR.cast<IR_Truncate>  (a,b, (c,d) => c) },
		{ I32, I16, (a,b) => IR.cast<IR_Truncate>  (a,b, (c,d) => c) },
		{ I32, I64, (a,b) => IR.cast<IR_Extend>    (a,b, (c,d) => c) },
		{ I32, F32, (a,b) => IR.cast<IR_IntToFloat>(a,b, (c,d) => c) },
		{ I32, F64, (a,b) => IR.cast<IR_IntToFloat>(a,b, (c,d) => c) },
		
		{ I64, I1,  (a,b) => IR.cast<IR_Truncate>  (a,b, (c,d) => (long)c != 0) },
		{ I64, I8,  (a,b) => IR.cast<IR_Truncate>  (a,b, (c,d) => c) },
		{ I64, I16, (a,b) => IR.cast<IR_Truncate>  (a,b, (c,d) => c) },
		{ I64, I32, (a,b) => IR.cast<IR_Truncate>  (a,b, (c,d) => c) },
		{ I64, F32, (a,b) => IR.cast<IR_IntToFloat>(a,b, (c,d) => c) },
		{ I64, F64, (a,b) => IR.cast<IR_IntToFloat>(a,b, (c,d) => c) },
		
		{ F32, I1,  (a,b) => IR.cast<IR_FloatToInt>(a,b, (c,d) =>       (double)c != 0) },
		{ F32, I8,  (a,b) => IR.cast<IR_FloatToInt>(a,b, (c,d) => (long)(double)c) },
		{ F32, I16, (a,b) => IR.cast<IR_FloatToInt>(a,b, (c,d) => (long)(double)c) },
		{ F32, I32, (a,b) => IR.cast<IR_FloatToInt>(a,b, (c,d) => (long)(double)c) },
		{ F32, I64, (a,b) => IR.cast<IR_FloatToInt>(a,b, (c,d) => (long)(double)c) },
		{ F32, F64, (a,b) => IR.cast<IR_Extend>    (a,b, (c,d) => (long)(double)c) },
		
		{ F64, I1,  (a,b) => IR.cast<IR_FloatToInt>(a,b, (c,d)=>       (double)c != 0) },
		{ F64, I8,  (a,b) => IR.cast<IR_FloatToInt>(a,b, (c,d)=> (long)(double)c) },
		{ F64, I16, (a,b) => IR.cast<IR_FloatToInt>(a,b, (c,d)=> (long)(double)c) },
		{ F64, I32, (a,b) => IR.cast<IR_FloatToInt>(a,b, (c,d)=> (long)(double)c) },
		{ F64, I64, (a,b) => IR.cast<IR_FloatToInt>(a,b, (c,d)=> (long)(double)c) },
		{ F64, F32, (a,b) => IR.cast<IR_Truncate>  (a,b, (c,d)=> (long)(double)c) },
	}, implicitCast = new CastLookup() {
		{ I1,  I8  },
		{ I1,  I16 },
		{ I1,  I32 },
		{ I1,  I64 },
		{ I1,  F32 },
		{ I1,  F64 },
		
		{ I8,  I16 },
		{ I8,  I32 },
		{ I8,  I64 },
		{ I8,  F32 },
		{ I8,  F64 },
		
		{ I16, I32 },
		{ I16, I64 },
		{ I16, F32 },
		{ I16, F64 },
		
		{ I32, I64 },
		{ I32, F32 },
		{ I32, F64 },
		
		{ I64, F32 },
		{ I64, F64 },
		
		{ F32, F64 },
	};
}

}