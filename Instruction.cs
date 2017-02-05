using System.Linq;

namespace Jolly
{
	class IR
	{
		public Value result;
	}
	
	// TODO: Expand this to actual instructions
	class IR_Cast : IR
	{
		public Value type, _value;
		public override string ToString() => "    %{0} = cast {1}, {2}".fill(result.tempID, type, _value);
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
	
	class IR_Bitcast : IR
	{
		public Value from;
		public override string ToString() => "    %{0} = bitcast {1} to {2}".fill(result.tempID, from, result.type);
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
	
	class IR_Fptosi : IR
	{
		public Value _int;
		public DataType _float;
		public override string ToString() => "    %{0} = fptosi float %3 to i32".fill(result, _int, _float);
	}
	
	class IR_Sitofp : IR
	{
		public Value _float;
		public DataType _int;
		public override string ToString() => "    %{0} = fptosi float %3 to i32".fill(result, _float, _int);
	}
}