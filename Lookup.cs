using System;
using System.Linq;
using System.Collections.Generic;

namespace Jolly
{
using TT = Token.Type;
using NT = AST_Node.Type;
using Operator = ExpressionParser.Operator;

using Cast = Func<Value,DataType,Value>;
using Instr = Func<Value,Value,Value>;

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
	
	const DataType.Flags BASE_TYPE = DataType.Flags.BASE_TYPE | DataType.Flags.INSTANTIABLE, INSTANTIABLE = DataType.Flags.INSTANTIABLE;
	
	public static readonly DataType
		I1       = new DataType{ name = "i1",     size = 1,  align = 1, typeID = 1,  flags = BASE_TYPE },
		I8       = new DataType{ name = "i8",     size = 1,  align = 1, typeID = 2,  flags = BASE_TYPE },
		U8       = new DataType{ name = "u8",     size = 1,  align = 1, typeID = 3,  flags = BASE_TYPE },
		I16      = new DataType{ name = "i16",    size = 2,  align = 2, typeID = 4,  flags = BASE_TYPE },
		U16      = new DataType{ name = "u16",    size = 2,  align = 2, typeID = 5,  flags = BASE_TYPE },
		I32      = new DataType{ name = "i32",    size = 4,  align = 4, typeID = 6,  flags = BASE_TYPE },
		U32      = new DataType{ name = "u32",    size = 4,  align = 4, typeID = 7,  flags = BASE_TYPE },
		I64      = new DataType{ name = "i64",    size = 8,  align = 8, typeID = 8,  flags = BASE_TYPE },
		U64      = new DataType{ name = "u64",    size = 8,  align = 8, typeID = 9,  flags = BASE_TYPE },
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
		AUTO,
	};

	public static DataType getBaseType(TT type)
	{
		return baseTypes[(type < TT.VOID) ? (type - TT.I1) & (~1) : type - TT.I1];
	}
	
	public readonly static Dictionary<TT, Operator>
		OPERATORS = new Dictionary<TT, Operator>() {
			{ TT.PERIOD,          new Operator(01, 2, true,  NT.GET_MEMBER      )},
			{ TT.EXCLAMATION,     new Operator(02, 1, false, NT.LOGIC_NOT       )},
			{ TT.TILDE,           new Operator(02, 1, false, NT.BIT_NOT         )},
			{ TT.NEW,             new Operator(02, 1, false, NT.NEW             )},
			{ TT.DELETE,          new Operator(02, 1, false, NT.DELETE          )},
			{ TT.AS,              new Operator(03, 2, true,  NT.CAST            )},
			{ TT.PERCENT,         new Operator(03, 2, true,  NT.MODULO          )},
			{ TT.ASTERISK,        new Operator(03, 2, true,  NT.MULTIPLY,  true )},
			{ TT.SLASH,           new Operator(03, 2, true,  NT.DIVIDE          )},
			{ TT.MINUS,           new Operator(04, 2, true,  NT.MINUS           )},
			{ TT.PLUS,            new Operator(04, 2, true,  NT.PLUS            )},
			{ TT.GREATER_GREATER, new Operator(05, 2, true,  NT.SHIFT_RIGHT     )},
			{ TT.LESS_LESS,       new Operator(05, 2, true,  NT.SHIFT_LEFT      )},
			{ TT.LESS_EQUAL,      new Operator(06, 2, true,  NT.LESS_EQUAL      )},
			{ TT.LESS,            new Operator(06, 2, true,  NT.LESS            )},
			{ TT.GREATER_EQUAL,   new Operator(06, 2, true,  NT.GREATER_EQUAL   )},
			{ TT.GREATER,         new Operator(06, 2, true,  NT.GREATER         )},
			{ TT.NOT_EQUAL,       new Operator(07, 2, true,  NT.NOT_EQUAL       )},
			{ TT.EQUAL_EQUAL,     new Operator(07, 2, true,  NT.EQUAL_TO        )},
			{ TT.AND,             new Operator(08, 2, true,  NT.BIT_AND         )},
			{ TT.CARET,           new Operator(09, 2, true,  NT.BIT_XOR         )},
			{ TT.PIPE,            new Operator(10, 2, true,  NT.BIT_OR          )},
			{ TT.AND_AND,         new Operator(11, 2, true,  NT.LOGIC_AND, true )},
			{ TT.OR_OR,           new Operator(12, 2, true,  NT.LOGIC_OR,  true )},
			{ TT.COLON_TILDE,     new Operator(13, 2, false, NT.BITCAST,   true )},
			{ TT.QUESTION_MARK,   new Operator(14, 2, false, NT.TERNARY,   true )},
			{ TT.COLON,           new Operator(14, 2, true,  NT.COLON,     true )},
			{ TT.COMMA,           new Operator(15, 2, true,  NT.COMMA           )},
			{ TT.EQUAL,           new Operator(16, 2, false, NT.ASSIGN          )},
			{ TT.AND_EQUAL,       new Operator(16, 2, false, NT.AND_ASSIGN      )},
			{ TT.SLASH_EQUAL,     new Operator(16, 2, false, NT.SLASH_ASSIGN    )},
			{ TT.MINUS_EQUAL,     new Operator(16, 2, false, NT.MINUS_ASSIGN    )},
			{ TT.PERCENT_EQUAL,   new Operator(16, 2, false, NT.PERCENT_ASSIGN  )},
			{ TT.ASTERISK_EQUAL,  new Operator(16, 2, false, NT.ASTERISK_ASSIGN )},
			{ TT.OR_EQUAL,        new Operator(16, 2, false, NT.OR_ASSIGN       )},
			{ TT.PLUS_EQUAL,      new Operator(16, 2, false, NT.PLUS_ASSIGN     )},
			{ TT.CARET_EQUAL,     new Operator(16, 2, false, NT.CARET_ASSIGN    )},
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
		
	public struct CastPair
	{
		public DataType _from, _to;
		public override int GetHashCode()
		{
			return (_from.GetHashCode() << 7) ^ _to.GetHashCode();
		}
		public override bool Equals(object obj)
		{
			var pair = (CastPair)obj;
			return _to.Equals(pair._to) && _from.Equals(pair._from);
		}
	}
	
	public class PairDict : Dictionary<CastPair, Cast>
	{
		public void Add(Tuple<CastPair, Cast> kvp) => Add(kvp.Item1, kvp.Item2);
		public bool getCast(DataType _from, DataType _to, out Cast cast)
			=> TryGetValue(new Lookup.CastPair{ _from = _from, _to = _to }, out cast);
	}
	
	// Ugly, dont look
	static Value zero(DataType _in) => new Value{ type = _in, kind = Value.Kind.STATIC_VALUE, data = 0 };
	static Tuple<CastPair, Cast> tp(DataType _from, DataType _to, Cast cast) => new Tuple<CastPair, Cast>(new CastPair{ _from = _from, _to = _to }, cast);
	
	static Value doInstr<T>(Value a, Value b) where T : IR_Instr, new()
	{
		var t = new T { a = a, b = b, result = Analyser.newResult(a) };
		Analyser.instructions.Add(t);
		t.result.kind = Value.Kind.VALUE;
		return t.result;
	}
	
	static Value doCast<T>(Value _from, DataType _to) where T : IR_Cast, new()
	{
		var t = new T { _to = _to, _from = _from };
		Analyser.instructions.Add(t);
		t.result.type = _to;
		t.result.kind = Value.Kind.VALUE;
		return t.result = Analyser.newResult(t.result);
	}
	
	static Value doIcmp(Value _from, DataType _to)
	{
		var t = new IR_Icmp { a = _from, b = zero(_from.type), result = Analyser.newResult(new Value{ type = I1 }), compare = IR_Icmp.Compare.ne  };
		Analyser.instructions.Add(t);
		t.result.kind = Value.Kind.VALUE;
		return t.result;
	}
	
	static Value doFcmp(Value _from, DataType _to)
	{
		var t = new IR_Fcmp { a = _from, b = zero(_from.type), result = Analyser.newResult(new Value{ type = I1 }), compare = IR_Fcmp.Compare.une };
		Analyser.instructions.Add(t);
		t.result.kind = Value.Kind.VALUE;
		return t.result;
	}
	
	static Value nop(Value _from, DataType _to) => _from;
	
	// TODO: Change this array bs back to a dictionary
	public static PairDict
		casts = new PairDict() {
			// I1
			tp(I1, U8,   doCast<IR_Zext>),
			tp(I1, I8,   doCast<IR_Zext>),
			tp(I1, U16,  doCast<IR_Zext>),
			tp(I1, I16,  doCast<IR_Zext>),
			tp(I1, U32,  doCast<IR_Zext>),
			tp(I1, I32,  doCast<IR_Zext>),
			tp(I1, U64,  doCast<IR_Zext>),
			tp(I1, I64,  doCast<IR_Zext>),
			tp(I1, F32,  doCast<IR_Sitofp>),
			tp(I1, F64,  doCast<IR_Sitofp>),
			// U8                            I8
			tp(U8, I1,   doIcmp),            tp(I8, I1,  doIcmp),
			tp(U8, I8,   nop),               tp(I8, U8,  nop),
			tp(U8, U16,  doCast<IR_Zext>),   tp(I8, U16, doCast<IR_Sext>),
			tp(U8, I16,  doCast<IR_Zext>),   tp(I8, I16, doCast<IR_Sext>),
			tp(U8, U32,  doCast<IR_Zext>),   tp(I8, U32, doCast<IR_Sext>),
			tp(U8, I32,  doCast<IR_Zext>),   tp(I8, I32, doCast<IR_Sext>),
			tp(U8, U64,  doCast<IR_Zext>),   tp(I8, U64, doCast<IR_Sext>),
			tp(U8, I64,  doCast<IR_Zext>),   tp(I8, I64, doCast<IR_Sext>),
			tp(U8, F32,  doCast<IR_Uitofp>), tp(I8, F32, doCast<IR_Sitofp>),
			tp(U8, F64,  doCast<IR_Uitofp>), tp(I8, F64, doCast<IR_Sitofp>),
			// U16                           I16
			tp(U16, I1,  doIcmp),            tp(I16, I1,  doIcmp),
			tp(U16, U8,  doCast<IR_Trunc>),  tp(I16, U8,  doCast<IR_Trunc>),
			tp(U16, I8,  doCast<IR_Trunc>),  tp(I16, I8,  doCast<IR_Trunc>),
			tp(U16, I16, nop),               tp(I16, U16, nop),
			tp(U16, U32, doCast<IR_Zext>),   tp(I16, U32, doCast<IR_Sext>),
			tp(U16, I32, doCast<IR_Zext>),   tp(I16, I32, doCast<IR_Sext>),
			tp(U16, U64, doCast<IR_Zext>),   tp(I16, U64, doCast<IR_Sext>),
			tp(U16, I64, doCast<IR_Zext>),   tp(I16, I64, doCast<IR_Sext>),
			tp(U16, F32, doCast<IR_Uitofp>), tp(I16, F32, doCast<IR_Sitofp>),
			tp(U16, F64, doCast<IR_Uitofp>), tp(I16, F64, doCast<IR_Sitofp>),
			// U32                           I32
			tp(U32, I1,  doIcmp),            tp(I32, I1,  doIcmp),
			tp(U32, U8,  doCast<IR_Trunc>),  tp(I32, U8,  doCast<IR_Trunc>),
			tp(U32, I8,  doCast<IR_Trunc>),  tp(I32, I8,  doCast<IR_Trunc>),
			tp(U32, U16, doCast<IR_Trunc>),  tp(I32, U16, doCast<IR_Trunc>),
			tp(U32, I16, doCast<IR_Trunc>),  tp(I32, I16, doCast<IR_Trunc>),
			tp(U32, I32, nop),               tp(I32, U32, nop),
			tp(U32, U64, doCast<IR_Zext>),   tp(I32, U64, doCast<IR_Sext>),
			tp(U32, I64, doCast<IR_Zext>),   tp(I32, I64, doCast<IR_Sext>),
			tp(U32, F32, doCast<IR_Uitofp>), tp(I32, F32, doCast<IR_Sitofp>),
			tp(U32, F64, doCast<IR_Uitofp>), tp(I32, F64, doCast<IR_Sitofp>),
			// U64                           I64
			tp(U64, I1,  doIcmp),            tp(I64, I1,  doIcmp),
			tp(U64, U8,  doCast<IR_Trunc>),  tp(I64, U8,  doCast<IR_Trunc>),
			tp(U64, I8,  doCast<IR_Trunc>),  tp(I64, I8,  doCast<IR_Trunc>),
			tp(U64, U16, doCast<IR_Trunc>),  tp(I64, U16, doCast<IR_Trunc>),
			tp(U64, I16, doCast<IR_Trunc>),  tp(I64, I16, doCast<IR_Trunc>),
			tp(U64, U32, doCast<IR_Trunc>),  tp(I64, U32, doCast<IR_Trunc>),
			tp(U64, I32, doCast<IR_Trunc>),  tp(I64, I32, doCast<IR_Trunc>),
			tp(U64, I64, nop),               tp(I64, U64, nop),
			tp(U64, F32, doCast<IR_Uitofp>), tp(I64, F32, doCast<IR_Sitofp>),
			tp(U64, F64, doCast<IR_Uitofp>), tp(I64, F64, doCast<IR_Sitofp>),
			// F32                           F64
			tp(F32, I1,  doFcmp),            tp(F64, I1,  doFcmp),
			tp(F32, U8,  doCast<IR_Fptoui>), tp(F64, U8,  doCast<IR_Fptoui>),
			tp(F32, I8,  doCast<IR_Fptosi>), tp(F64, I8,  doCast<IR_Fptosi>),
			tp(F32, U16, doCast<IR_Fptoui>), tp(F64, U16, doCast<IR_Fptoui>),
			tp(F32, I16, doCast<IR_Fptosi>), tp(F64, I16, doCast<IR_Fptosi>),
			tp(F32, U32, doCast<IR_Fptoui>), tp(F64, U32, doCast<IR_Fptoui>),
			tp(F32, I32, doCast<IR_Fptosi>), tp(F64, I32, doCast<IR_Fptosi>),
			tp(F32, U64, doCast<IR_Fptoui>), tp(F64, U64, doCast<IR_Fptoui>),
			tp(F32, F64, doCast<IR_Fpext>),  tp(F64, F32, doCast<IR_Trunc>),
		}, 
		implicitCasts = new PairDict() {
			// I1
			tp(I1, U8,  doCast<IR_Zext>),
			tp(I1, I8,  doCast<IR_Zext>),
			tp(I1, U16, doCast<IR_Zext>),
			tp(I1, I16, doCast<IR_Zext>),
			tp(I1, U32, doCast<IR_Zext>),
			tp(I1, I32, doCast<IR_Zext>),
			tp(I1, U64, doCast<IR_Zext>),
			tp(I1, I64, doCast<IR_Zext>),
			tp(I1, F32, doCast<IR_Sitofp>),
			tp(I1, F64, doCast<IR_Sitofp>),
			// U8                            I8
			tp(U8, I1,  doIcmp),             tp(I8,  I1,  doIcmp),        
			tp(U8, U16, doCast<IR_Zext>),    tp(I8,  U16, doCast<IR_Sext>),
			tp(U8, I16, doCast<IR_Zext>),    tp(I8,  I16, doCast<IR_Sext>),
			tp(U8, U32, doCast<IR_Zext>),    tp(I8,  U32, doCast<IR_Sext>),
			tp(U8, I32, doCast<IR_Zext>),    tp(I8,  I32, doCast<IR_Sext>),
			tp(U8, U64, doCast<IR_Zext>),    tp(I8,  U64, doCast<IR_Sext>),
			tp(U8, I64, doCast<IR_Zext>),    tp(I8,  I64, doCast<IR_Sext>),
			tp(U8, F32, doCast<IR_Uitofp>),  tp(I8,  F32, doCast<IR_Sitofp>),
			tp(U8, F64, doCast<IR_Uitofp>),  tp(I8,  F64, doCast<IR_Sitofp>),
			// U16                           I16
			tp(U16, I1,  doIcmp),            tp(I16, I1,  doIcmp),        
			tp(U16, U32, doCast<IR_Zext>),   tp(I16, U32, doCast<IR_Sext>),
			tp(U16, I32, doCast<IR_Zext>),   tp(I16, I32, doCast<IR_Sext>),
			tp(U16, U64, doCast<IR_Zext>),   tp(I16, U64, doCast<IR_Sext>),
			tp(U16, I64, doCast<IR_Zext>),   tp(I16, I64, doCast<IR_Sext>),
			tp(U16, F32, doCast<IR_Uitofp>), tp(I16, F32, doCast<IR_Sitofp>),
			tp(U16, F64, doCast<IR_Uitofp>), tp(I16, F64, doCast<IR_Sitofp>),
			// U32                           I32
			tp(U32, I1,  doIcmp),            tp(I32, I1,  doIcmp),        
			tp(U32, U64, doCast<IR_Zext>),   tp(I32, U64, doCast<IR_Sext>),
			tp(U32, I64, doCast<IR_Zext>),   tp(I32, I64, doCast<IR_Sext>),
			tp(U32, F32, doCast<IR_Uitofp>), tp(I32, F32, doCast<IR_Sitofp>),
			tp(U32, F64, doCast<IR_Uitofp>), tp(I32, F64, doCast<IR_Sitofp>),
			// F32                           F64
			tp(F32, I1,  doFcmp),            tp(F64, I1,  doFcmp),
			tp(F32, F64, doCast<IR_Fpext>),
		};
	
	public static Dictionary<DataType, Instr>
		adds = new Dictionary<DataType, Instr>() {
			{ U8,  doInstr<IR_Add>  }, { I8,  doInstr<IR_Add>  },
			{ U16, doInstr<IR_Add>  }, { I16, doInstr<IR_Add>  },
			{ U32, doInstr<IR_Add>  }, { I32, doInstr<IR_Add>  },
			{ U64, doInstr<IR_Add>  }, { I64, doInstr<IR_Add>  },
			{ F32, doInstr<IR_Fadd> }, { F64, doInstr<IR_Fadd> },
		},
		subs = new Dictionary<DataType, Instr>() {
			{ U8,  doInstr<IR_Sub>  }, { I8,  doInstr<IR_Sub>  },
			{ U16, doInstr<IR_Sub>  }, { I16, doInstr<IR_Sub>  },
			{ U32, doInstr<IR_Sub>  }, { I32, doInstr<IR_Sub>  },
			{ U64, doInstr<IR_Sub>  }, { I64, doInstr<IR_Sub>  },
			{ F32, doInstr<IR_Fsub> }, { F64, doInstr<IR_Fsub> },
		},
		muls = new Dictionary<DataType, Instr>() {
			{ U8,  doInstr<IR_Mul>  }, { I8,  doInstr<IR_Mul>  },
			{ U16, doInstr<IR_Mul>  }, { I16, doInstr<IR_Mul>  },
			{ U32, doInstr<IR_Mul>  }, { I32, doInstr<IR_Mul>  },
			{ U64, doInstr<IR_Mul>  }, { I64, doInstr<IR_Mul>  },
			{ F32, doInstr<IR_Fmul> }, { F64, doInstr<IR_Fmul> },
		},
		divs = new Dictionary<DataType, Instr>() {
			{ U8,  doInstr<IR_Udiv> }, { I8,  doInstr<IR_Sdiv> },
			{ U16, doInstr<IR_Udiv> }, { I16, doInstr<IR_Sdiv> },
			{ U32, doInstr<IR_Udiv> }, { I32, doInstr<IR_Sdiv> },
			{ U64, doInstr<IR_Udiv> }, { I64, doInstr<IR_Sdiv> },
			{ F32, doInstr<IR_Fdiv> }, { F64, doInstr<IR_Fdiv> },
		},
		mods = new Dictionary<DataType, Instr>() {
			{ U8,  doInstr<IR_Urem> }, { I8,  doInstr<IR_Srem> },
			{ U16, doInstr<IR_Urem> }, { I16, doInstr<IR_Srem> },
			{ U32, doInstr<IR_Urem> }, { I32, doInstr<IR_Srem> },
			{ U64, doInstr<IR_Urem> }, { I64, doInstr<IR_Srem> },
			{ F32, doInstr<IR_Frem> }, { F64, doInstr<IR_Frem> },
		},
		slefts = new Dictionary<DataType, Instr>() {
			{ U8,  doInstr<IR_Shl>  }, { I8,  doInstr<IR_Shl>  },
			{ U16, doInstr<IR_Shl>  }, { I16, doInstr<IR_Shl>  },
			{ U32, doInstr<IR_Shl>  }, { I32, doInstr<IR_Shl>  },
			{ U64, doInstr<IR_Shl>  }, { I64, doInstr<IR_Shl>  },
		},
		srights = new Dictionary<DataType, Instr>() {
			{ U8,  doInstr<IR_Shlr> }, { I8,  doInstr<IR_Shlr> },
			{ U16, doInstr<IR_Shlr> }, { I16, doInstr<IR_Shlr> },
			{ U32, doInstr<IR_Shlr> }, { I32, doInstr<IR_Shlr> },
			{ U64, doInstr<IR_Shlr> }, { I64, doInstr<IR_Shlr> },
		},
		ands = new Dictionary<DataType, Instr>() {
			{ I1,  doInstr<IR_And>  },
			{ U8,  doInstr<IR_And>  }, { I8,  doInstr<IR_And>  },
			{ U16, doInstr<IR_And>  }, { I16, doInstr<IR_And>  },
			{ U32, doInstr<IR_And>  }, { I32, doInstr<IR_And>  },
			{ U64, doInstr<IR_And>  }, { I64, doInstr<IR_And>  },
		},
		ors = new Dictionary<DataType, Instr>() {
			{ I1,  doInstr<IR_Or>   },
			{ U8,  doInstr<IR_Or>   }, { I8,  doInstr<IR_Or>   },
			{ U16, doInstr<IR_Or>   }, { I16, doInstr<IR_Or>   },
			{ U32, doInstr<IR_Or>   }, { I32, doInstr<IR_Or>   },
			{ U64, doInstr<IR_Or>   }, { I64, doInstr<IR_Or>   },
		},
		xors = new Dictionary<DataType, Instr>() {
			{ I1,  doInstr<IR_Xor>  },
			{ U8,  doInstr<IR_Xor>  }, { I8,  doInstr<IR_Xor>  },
			{ U16, doInstr<IR_Xor>  }, { I16, doInstr<IR_Xor>  },
			{ U32, doInstr<IR_Xor>  }, { I32, doInstr<IR_Xor>  },
			{ U64, doInstr<IR_Xor>  }, { I64, doInstr<IR_Xor>  },
		};
}

}