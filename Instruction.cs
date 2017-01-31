using System.Collections.Generic;
using System.Linq;

namespace Jolly
{
    using IT = IR.Type;
	
	class IR
	{
		public enum Type
		{
			UNDEFINED,
			DEFINE_STRUCT,
			DEFINE_FUNCTION,
			
			GET_MEMBER,
			
			REFERENCE,
			DEREFERENCE,
			
			ALLOCATE,
			LOAD,
			STORE,
			CAST,
			CALL,
			ADD,
			SUBTRACT,
			MULTIPLY,
			DEVIDE,
			RETURN,
		}
		public IT instruction;
		public Value result;
	}
	
	class IR_Cast : IR
	{
		public Value type, _value;
	}
	
	class IR_STORE : IR
	{
		public Value location, _value;
	}
	
	class IR_Allocate : IR
	{
		public IR_Allocate(DataType type) { this.type = type; }
		public DataType type;
	}
	
	class IR_Return : IR
	{
		public Value[] values;
	}
	
	class IR_Call : IR
	{
		public IR_Call() { instruction = IT.CALL; }
		public Value[] arguments;
	}
		
	// class InstructionStruct : Instruction
	// {
	// 	public DataTypeStruct structType;
	// 	public override string ToString() => "@{0} = struct {{ {1} }}".fill(structType.name, structType.members.Select(m => m.ToString()).Aggregate((a, b) => a + ", " + b));
	// }
	
	class IR_Function : IR
	{
		public IR_Function(DataType_Function functionType) { this.functionType = functionType; }
		public DataType_Function functionType;
	}
}