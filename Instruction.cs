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
		public override string ToString() => "    ret {0}".fill((values?.Length == 0) ? "" : values.Select(v=>v.ToString()).Aggregate((a,b)=>a+", "+b));
	}
	
	class IR_Call : IR
	{
		public Value function;
		public Value[] arguments;
		public override string ToString() => "    call @{0}({1})".fill(
			(function.kind == Value.Kind.STATIC_FUNCTION) ? function.type.name : function.ToString(),
			(arguments?.Length == 0) ? "" : arguments.Select(v=>v.ToString()).Aggregate((a,b)=>a+", "+b));
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
			(functionType.arguments.Length != 0) ? functionType.arguments.Select(r=>r.ToString()).Aggregate((a,b)=>a+", "+b) : "");
	}
	
	class IR_Add : IR
	{
		public Value a, b;
		public override string ToString() => "    %{0} = add {1}, {2}".fill(result, a, b);
	}
	
	abstract class IR_Cast : IR
	{
		public Value _from;
		public DataType _to;
	}
	
	// The ‘trunc‘ instruction truncates the high order bits in value and converts the remaining bits to ty2.
	// Since the source size must be larger than the destination size, trunc cannot be a no-op cast.
	// It will always truncate bits.
	class IR_Trunc : IR_Cast
	{
		public override string ToString() => "    %{0} = trunc {1} to {2}".fill(result.tempID, _from, _to);
	}
	
	// The ‘zext‘ instruction fills the high order bits of the value with zero bits until it reaches the size of the destination type, ty2.
	// When zero extending from i1, the result will always be either 0 or 1.
	class IR_Zext : IR_Cast
	{
		public override string ToString() => "    %{0} = zext {1} to {2}".fill(result.tempID, _from, _to);
	}
	
	// The ‘sext‘ instruction takes a value to cast, and a type to cast it to.
	// Both types must be of integer types, or vectors of the same number of integers.
	// The bit size of the value must be smaller than the bit size of the destination type, ty2.
	class IR_Sext : IR_Cast
	{
		public override string ToString() => "    %{0} = sext {1} to {2}".fill(result.tempID, _from, _to);
	}
	
	// The ‘fptrunc‘ instruction casts a value from a larger floating point type to a smaller floating point type.
	// If the value cannot fit (i.e. overflows) within the destination type, ty2, then the results are undefined.
	// If the cast produces an inexact result, how rounding is performed (e.g. truncation, also known as round to zero) is undefined
	class IR_Fptrunc : IR_Cast
	{
		public override string ToString() => "    %{0} = fptrunc {1} to {2}".fill(result.tempID, _from, _to);
	}
	
	// The ‘fpext‘ instruction extends the value from a smaller floating point type to a larger floating point type.
	// The fpext cannot be used to make a no-op cast because it always changes bits.
	// Use bitcast to make a no-op cast for a floating point cast.
	class IR_Fpext : IR_Cast
	{
		public override string ToString() => "    %{0} = fpext {1} to {2}".fill(result.tempID, _from, _to);
	}
	
	// The ‘fptoui‘ instruction converts its floating point operand into the nearest (rounding towards zero) unsigned integer value.
	// If the value cannot fit in ty2, the results are undefined.
	class IR_Fptoui : IR_Cast
	{
		public override string ToString() => "    %{0} = fptoui {1} to {2}".fill(result.tempID, _from, _to);
	}
	
	// The ‘fptosi‘ instruction converts its floating point operand into the nearest (rounding towards zero) signed integer value.
	// If the value cannot fit in ty2, the results are undefined.
	class IR_Fptosi : IR_Cast
	{
		public override string ToString() => "    %{0} = fptosi {1} to {2}".fill(result.tempID, _from, _to);
	}
	
	// The ‘uitofp‘ instruction interprets its operand as an unsigned integer quantity and converts it to the corresponding floating point value.
	// If the value cannot fit in the floating point value, the results are undefined.
	class IR_Uitofp : IR_Cast
	{
		public override string ToString() => "    %{0} = uitofp {1} to {2}".fill(result.tempID, _from, _to);
	}
	
	// The ‘sitofp‘ instruction interprets its operand as a signed integer quantity and converts it to the corresponding floating point value.
	// If the value cannot fit in the floating point value, the results are undefined.
	class IR_Sitofp : IR_Cast
	{
		public override string ToString() => "    %{0} = sitofp {1} to {2}".fill(result.tempID, _from, _to);
	}
	
	// The ‘ptrtoint‘ instruction converts value to integer type ty2 by interpreting the pointer value as an integer and 
	// either truncating or zero extending that value to the size of the integer type.
	// If value is smaller than ty2 then a zero extension is done. If value is larger than ty2 then a truncation is done.
	// If they are the same size, then nothing is done (no-op cast) other than a type change.
	class IR_Ptrtoint : IR_Cast
	{
		public override string ToString() => "    %{0} = ptrtoint {1} to {2}".fill(result.tempID, _from, _to);
	}
	
	// The ‘inttoptr‘ instruction converts value to type ty2 by applying either a zero extension or a truncation depending
	// on the size of the integer value. If value is larger than the size of a pointer then a truncation is done.
	// If value is smaller than the size of a pointer then a zero extension is done. If they are the same size, nothing is done (no-op cast).
	class IR_Inttoptr : IR_Cast
	{
		public override string ToString() => "    %{0} = inttoptr {1} to {2}".fill(result.tempID, _from, _to);
	}
	
	// The ‘bitcast‘ instruction converts value to type ty2. It is always a no-op cast because no bits change with this conversion.
	// The conversion is done as if the value had been stored to memory and read back as type ty2. Pointer (or vector of pointers)
	// types may only be converted to other pointer (or vector of pointers) types with the same address space through this instruction.
	// To convert pointers to other types, use the inttoptr or ptrtoint instructions first.
	class IR_Bitcast : IR_Cast
	{
		public override string ToString() => "    %{0} = bitcast {1} to {2}".fill(result, _from, result.type);
	}
}