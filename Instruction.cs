using System.Linq;

namespace Jolly
{
	class IR
	{
		public Value result;
	}
		
	class IR_Br : IR
	{
		public Value condition;
		public int trueLabelId, falseLabelId;
		public override string ToString() => "    br {0}, _{1}, _{2}".fill(condition, trueLabelId, falseLabelId);
	}
	
	class IR_Goto : IR
	{
		public int labelId;
		public override string ToString() => "    goto _{0}".fill(labelId);
	}
	
	class IR_Label : IR
	{
		public int id;
		public override string ToString() => "_{0}:".fill(id);
	}
	
	struct PhiBranch
	{
		public int fromId;
		public Value _value;
		public override string ToString() => "[{0}, {1}]".fill(_value, fromId);
	}
	
	class IR_Phi : IR
	{
		public PhiBranch[] branches;
		public override string ToString() => "    %{0} = phi {1} {2}".fill(result.tempID, result.type, branches.implode(", "));
	}
	
	class IR_Store : IR
	{
		public Value location, _value;
		public override string ToString() => "    store {0}, {1}".fill(_value, location);
	}
	
	class IR_GetMember : IR
	{
		public Value _struct;
		public int index;
		public override string ToString() => "    %{0} = get_member {1}, i32 {2}".fill(result.tempID, _struct, index);
	}
	
	class IR_Load : IR
	{
		public Value location;
		public override string ToString() => "    %{0} = load {1}".fill(result.tempID, location);
	}
	
	class IR_Allocate : IR
	{
		public DataType type;
		public override string ToString() => "    %{0} = allocate {1}".fill(result.tempID, type);
	}
	
	class IR_Return : IR
	{
		public Value[] values;
		public override string ToString() => "    ret {0}".fill(values?.implode(", "));
	}
	
	class IR_Call : IR
	{
		public Value function;
		public Value[] arguments;
		public override string ToString() => "    call @{0}({1})".fill(
			(function.kind == Value.Kind.STATIC_FUNCTION) ? function.type.name : function.ToString(),
			arguments?.implode(", "));
	}
	
	class IR_Struct : IR
	{
		public DataType_Struct _struct;
		public DataType[] members;
		public override string ToString() => "{0} = struct {{ {1} }}".fill(_struct,
			(members.Length > 0) ? members.Select(m=>m.ToString()).Aggregate((a,b)=>a+", "+b) : "");
	}
	
	class IR_Function : IR
	{
		public IR_Function(DataType_Function functionType) { this.functionType = functionType; }
		public DataType_Function functionType;
		public override string ToString() => "define {0} @{1}({2})".fill(
			functionType.returns.Select(r=>r.ToString()).Aggregate((a,b)=>a+", "+b),
			functionType.name,
			functionType.arguments?.implode(", "));
	}
	
	abstract class IR_Cast : IR
	{
		public Value _from;
		public DataType _to;
		
		public virtual string opStr() => "";
		public override string ToString() => "    %{0} = {1} {2}, {3}".fill(result.tempID, opStr(), _from, _to);
	}
	
	abstract class IR_Instr : IR
	{
		public Value a, b;
		
		public virtual string opStr() => "";
		public override string ToString() => "    %{0} = {1} {2}, {3}".fill(result.tempID, opStr(), a, b);
	}
	
	class IR_Add : IR_Instr
	{
		public override string opStr() => "add";
	}
	
	class IR_Fadd : IR_Instr
	{
		public override string opStr() => "fadd";
	}
	
	class IR_Sub : IR_Instr
	{
		public override string opStr() => "sub";
	}
	
	class IR_Fsub : IR_Instr
	{
		public override string opStr() => "fsub";
	}
	
	class IR_Mul : IR_Instr
	{
		public override string opStr() => "mul";
	}
	
	class IR_Fmul : IR_Instr
	{
		public override string opStr() => "mul";
	}
	
	class IR_Udiv : IR_Instr
	{
		public override string opStr() => "udiv";
	}
	
	class IR_Sdiv : IR_Instr
	{
		public override string opStr() => "sdiv";
	}
	
	class IR_Fdiv : IR_Instr
	{
		public override string opStr() => "fdiv";
	}
	
	class IR_Urem : IR_Instr
	{
		public override string opStr() => "urem";
	}
	
	class IR_Srem : IR_Instr
	{
		public override string opStr() => "srem";
	}
	
	class IR_Frem : IR_Instr
	{
		public override string opStr() => "frem";
	}
	
	class IR_Shl : IR_Instr
	{
		public override string opStr() => "shl";
	}
	
	class IR_Shlr : IR_Instr
	{
		public override string opStr() => "shlr";
	}
	
	class IR_Ashr : IR_Instr
	{
		public override string opStr() => "ashr";
	}
	
	class IR_And : IR_Instr
	{
		public override string opStr() => "and";
	}
	
	class IR_Or : IR_Instr
	{
		public override string opStr() => "or";
	}
	
	class IR_Xor : IR_Instr
	{
		public override string opStr() => "xor";
	}
	
	// 	The ‘icmp‘ compares op1 and op2 according to the condition code given as cond. The comparison
	// performed always yields either an i1 or vector of i1 result, as follows:
	//   eq: yields true if the operands are equal, false otherwise. No sign interpretation is necessary or performed.
	//   ne: yields true if the operands are unequal, false otherwise. No sign interpretation is necessary or performed.
	//   ugt: interprets the operands as unsigned values and yields true if op1 is greater than op2.
	//   uge: interprets the operands as unsigned values and yields true if op1 is greater than or equal to op2.
	//   ult: interprets the operands as unsigned values and yields true if op1 is less than op2.
	//   ule: interprets the operands as unsigned values and yields true if op1 is less than or equal to op2.
	//   sgt: interprets the operands as signed values and yields true if op1 is greater than op2.
	//   sge: interprets the operands as signed values and yields true if op1 is greater than or equal to op2.
	//   slt: interprets the operands as signed values and yields true if op1 is less than op2.
	//   sle: interprets the operands as signed values and yields true if op1 is less than or equal to op2.
	// If the operands are pointer typed, the pointer values are compared as if they were integers.
	// If the operands are integer vectors, then they are compared element by element.
	// The result is an i1 vector with the same number of elements as the values being compared.
	// Otherwise, the result is an i1.
	class IR_Icmp : IR_Instr
	{
		public enum Compare
		{
			eq,  ne,
			ugt, uge,
			ult, ule,
			sgt, sge,
			slt, sle,
		}
		public Compare compare;
		public override string ToString() => "    %{0} = icmp {1} {2}, {3}".fill(result.tempID, compare, a, b);
	}
	
	// The ‘fcmp‘ instruction compares op1 and op2 according to the condition code given as cond.
	// If the operands are vectors, then the vectors are compared element by element.
	// Each comparison performed always yields an i1 result, as follows:
    // false: always yields false, regardless of operands.
    // oeq: yields true if both operands are not a QNAN and op1 is equal to op2.
    // ogt: yields true if both operands are not a QNAN and op1 is greater than op2.
    // oge: yields true if both operands are not a QNAN and op1 is greater than or equal to op2.
    // olt: yields true if both operands are not a QNAN and op1 is less than op2.
    // ole: yields true if both operands are not a QNAN and op1 is less than or equal to op2.
    // one: yields true if both operands are not a QNAN and op1 is not equal to op2.
    // ord: yields true if both operands are not a QNAN.
    // ueq: yields true if either operand is a QNAN or op1 is equal to op2.
    // ugt: yields true if either operand is a QNAN or op1 is greater than op2.
    // uge: yields true if either operand is a QNAN or op1 is greater than or equal to op2.
    // ult: yields true if either operand is a QNAN or op1 is less than op2.
    // ule: yields true if either operand is a QNAN or op1 is less than or equal to op2.
    // une: yields true if either operand is a QNAN or op1 is not equal to op2.
    // uno: yields true if either operand is a QNAN.
    // true: always yields true, regardless of operands.
	// The fcmp instruction can also optionally take any number of fast-math flags,
	// which are optimization hints to enable otherwise unsafe floating point optimizations.
	// Any set of fast-math flags are legal on an fcmp instruction, but the only flags that have any
	// effect on its semantics are those that allow assumptions to be made about the values of input arguments;
	// namely nnan, ninf, and nsz. See Fast-Math Flags for more information.
	class IR_Fcmp : IR_Instr
	{
		public enum Compare
		{
			oeq, ueq,
			ogt, ugt,
			oge, uge,
			olt, ult,
			ole, ule,
			one, une,
			ord, uno,
		}
		public Compare compare;
		public override string ToString() => "    %{0} = fcmp {1} {2}, {3}".fill(result.tempID, compare, a, b);
	}
	
	// The ‘trunc‘ instruction truncates the high order bits in value and converts the remaining bits to ty2.
	// Since the source size must be larger than the destination size, trunc cannot be a no-op cast.
	// It will always truncate bits.
	class IR_Trunc : IR_Cast
	{
		public override string opStr() => "trunc";
	}
	
	// The ‘zext‘ instruction fills the high order bits of the value with zero bits until it reaches the size of the destination type, ty2.
	// When zero extending from i1, the result will always be either 0 or 1.
	class IR_Zext : IR_Cast
	{
		public override string opStr() => "zext";
	}
	
	// The ‘sext‘ instruction takes a value to cast, and a type to cast it to.
	// Both types must be of integer types, or vectors of the same number of integers.
	// The bit size of the value must be smaller than the bit size of the destination type, ty2.
	class IR_Sext : IR_Cast
	{
		public override string opStr() => "sext";
	}
	
	// The ‘fptrunc‘ instruction casts a value from a larger floating point type to a smaller floating point type.
	// If the value cannot fit (i.e. overflows) within the destination type, ty2, then the results are undefined.
	// If the cast produces an inexact result, how rounding is performed (e.g. truncation, also known as round to zero) is undefined
	class IR_Fptrunc : IR_Cast
	{
		public override string opStr() => "fptrunc";
	}
	
	// The ‘fpext‘ instruction extends the value from a smaller floating point type to a larger floating point type.
	// The fpext cannot be used to make a no-op cast because it always changes bits.
	// Use bitcast to make a no-op cast for a floating point cast.
	class IR_Fpext : IR_Cast
	{
		public override string opStr() => "fpext";
	}
	
	// The ‘fptoui‘ instruction converts its floating point operand into the nearest (rounding towards zero) unsigned integer value.
	// If the value cannot fit in ty2, the results are undefined.
	class IR_Fptoui : IR_Cast
	{
		public override string opStr() => "fptoui";
	}
	
	// The ‘fptosi‘ instruction converts its floating point operand into the nearest (rounding towards zero) signed integer value.
	// If the value cannot fit in ty2, the results are undefined.
	class IR_Fptosi : IR_Cast
	{
		public override string opStr() => "fptosi";
	}
	
	// The ‘uitofp‘ instruction interprets its operand as an unsigned integer quantity and converts it to the corresponding floating point value.
	// If the value cannot fit in the floating point value, the results are undefined.
	class IR_Uitofp : IR_Cast
	{
		public override string opStr() => "uitofp";
	}
	
	// The ‘sitofp‘ instruction interprets its operand as a signed integer quantity and converts it to the corresponding floating point value.
	// If the value cannot fit in the floating point value, the results are undefined.
	class IR_Sitofp : IR_Cast
	{
		public override string opStr() => "sitofp";
	}
	
	// The ‘ptrtoint‘ instruction converts value to integer type ty2 by interpreting the pointer value as an integer and 
	// either truncating or zero extending that value to the size of the integer type.
	// If value is smaller than ty2 then a zero extension is done. If value is larger than ty2 then a truncation is done.
	// If they are the same size, then nothing is done (no-op cast) other than a type change.
	class IR_Ptrtoint : IR_Cast
	{
		public override string opStr() => "ptrtoint";
	}
	
	// The ‘inttoptr‘ instruction converts value to type ty2 by applying either a zero extension or a truncation depending
	// on the size of the integer value. If value is larger than the size of a pointer then a truncation is done.
	// If value is smaller than the size of a pointer then a zero extension is done. If they are the same size, nothing is done (no-op cast).
	class IR_Inttoptr : IR_Cast
	{
		public override string opStr() => "inttoptr";
	}
	
	// The ‘bitcast‘ instruction converts value to type ty2. It is always a no-op cast because no bits change with this conversion.
	// The conversion is done as if the value had been stored to memory and read back as type ty2. Pointer (or vector of pointers)
	// types may only be converted to other pointer (or vector of pointers) types with the same address space through this instruction.
	// To convert pointers to other types, use the inttoptr or ptrtoint instructions first.
	class IR_Bitcast : IR_Cast
	{
		public override string opStr() => "bitcast";
	}
}