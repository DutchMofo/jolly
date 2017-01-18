namespace Jolly
{
    using System.Linq;
    using IT = Instruction.Type;
	
	class Instruction
	{
		public enum Type
		{
			UNDEFINED,
			DEFINE_STRUCT,
			DEFINE_FUNCTION,
			
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
	}
	
	class InstructionOperator : Instruction
	{
		public InstructionOperator(NodeOperator op) { aType = op.a.dataType; bType = op.b.dataType; resultType = op.result.dataType; }
		public DataType aType, bType, resultType;
		public override string ToString() => "{0} = {1} {2}, {3}".fill(resultType, instruction, aType, bType);
	}
	
	class InstructionDefineStruct : Instruction
	{
		public DataTypeStruct structType;
		public override string ToString() => "@name struct {{ {0} }}".fill(structType.members.Select(m => m.ToString()).Aggregate((a, b) => a + ", " + b));
	}
	
	class InstructionDefineFunction : Instruction
	{
		public DataTypeFunction functionType;
		public override string ToString() => "define {0} @name ({1})".fill(
				functionType.returns.Select(m => m.ToString()).Aggregate((a, b) => a + ", " + b),
				functionType.arguments.Select(m => m.ToString()).Aggregate((a, b) => a + ", " + b));
	}
}