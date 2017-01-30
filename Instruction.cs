using System.Collections.Generic;
using System.Linq;

namespace Jolly
{
    using IT = IR.Type;
	using OT = OperatorType;
	
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
	}
		
	class IR_Allocate : IR
	{
		public IR_Allocate(DataType type) { this.type = type; }
		public DataType type;
		public override string ToString() => "allocate " + type;
	}
	
	class IR_Return : IR
	{
		public override string ToString() => "return null";
	}
	
	class IR_Call : IR
	{
		public IR_Call() { instruction = IT.CALL; }
		public DataType[] arguments;
		public override string ToString() => "call ({0})".fill((arguments.Length > 0) ? arguments.Select(t=>t.ToString()).Aggregate((a,b)=>a+", "+b) : "");
	}
	
	class IR_Operator : IR
	{
		static Dictionary<OT, IT> removeThis = new Dictionary<OT, IT>() {
			{ OT.PLUS, IT.ADD }, // TODO: Stop switching between plus/add, write/store
			{ OT.MINUS, IT.SUBTRACT },
			{ OT.MULTIPLY, IT.MULTIPLY },
			{ OT.DIVIDE, IT.DEVIDE },
			{ OT.GET_MEMBER, IT.GET_MEMBER },
			{ OT.ASSIGN, IT.STORE },
			{ OT.REFERENCE, IT.REFERENCE },
			{ OT.DEREFERENCE, IT.DEREFERENCE },
			{ OT.CAST, IT.CAST },
		};
		
		public IR_Operator() { }
		public IR_Operator(AST_Operator op)
		{
			instruction = removeThis[op.operation];
			aType = op.a.dataType;
			bType = op.b?.dataType;
			resultType = op.dataType;
		}
		public DataType aType, bType, resultType;
		public override string ToString() => "{0} = {1} {2}, {3}".fill(resultType, instruction, aType, bType);
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
		public override string ToString() => "define {0} @{1} ({2})".fill(
				(functionType.returns.Length == 1) ? functionType.returns[0].ToString() : "{ " + functionType.returns.Select(m => m?.ToString()).Aggregate((a, b) => a + ", " + b) + " }",
				functionType.name,
				(functionType.arguments.Length > 0) ? functionType.arguments.Select(m => m.ToString()).Aggregate((a, b) => a + ", " + b) : "");
	}
}