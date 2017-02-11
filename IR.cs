using System;
using System.Collections.Generic;

namespace Jolly
{
	using StaticExec = Func<object, object, object>;
	using NT = AST_Node.Type;

    enum ValueKind : byte
	{
		UNDEFINED = 0,
		STATIC_TYPE,
		STATIC_VALUE,
		STATIC_FUNCTION,
		ADDRES,
		VALUE,
	};
	
	class IR
	{
		public DataType  dType;
		public ValueKind dKind;
		public NT irType;
		
		public static IR cast<T>(IR from, DataType to, StaticExec exec) where T : IR_Cast, new()
		{
			if(exec != null &&
			   @from.dKind == ValueKind.STATIC_VALUE) {
				return new IR_Literal{ dType = to, data = exec(((IR_Literal)@from).data, 0) };
			}
			return new T{ @from = @from, dType = to };
		}
		
		public static IR operation<T>(IR a, IR b, StaticExec exec) where T : IR_Operation, new()
		{ 
			if(exec != null &&
			   a.dKind == ValueKind.STATIC_VALUE &&
			   b.dKind == ValueKind.STATIC_VALUE) {
				return new IR_Literal{ dType = a.dType, data = exec(((IR_Literal)a).data, ((IR_Literal)a).data) };
			}
			return new T{ a = a, b = b };
		}
		
		public static IR getMember(IR _struct, DataType result, int index)
		{
			return new IR_GetMember{ _struct = _struct, index = index, dType = result };
		}
		
		public override string ToString() => "{0} {1} {2}".fill(dType, dKind, irType);
	}
	
	class IR_If : IR
	{
		public IR_If() { irType = NT.IF; }
		public IR condition;
		public IRList ifBlock, elseBlock;
	}
	
	class IR_GetMember : IR
	{
		public IR_GetMember() {
			dKind = ValueKind.ADDRES;
			irType = NT.GET_MEMBER;
		}
		public IR _struct;
		public int index;
	}
	
	class IR_Struct : IR
	{
		public IR_Struct() {
			dKind = ValueKind.STATIC_TYPE;
			irType = NT.STRUCT;
		}
		public DataType _struct;
	}
	
	class IR_Logic : IR
	{
		public IR a, b;
		public IRList block;
	}
	
	class IR_Ternary : IR
	{
		public IR_Ternary() { irType = NT.TERNARY; }
		public IR condition, a, b;
		public IRList trueBlock, falseBlock;
	}
	
	class IR_Literal : IR
	{
		public IR_Literal() {
			irType = NT.LITERAL;
			dKind = ValueKind.STATIC_VALUE;
		}
		public object data;
	}
	
	class IR_Allocate : IR
	{
		public IR_Allocate() {
			irType = NT.ALLOCATE;
			dKind = ValueKind.ADDRES;
		}
	}
	
	class IR_Reference : IR
	{
		public IR_Reference() { irType = NT.REFERENCE; }
		public IR target;
	}
	
	class IR_Dereference : IR
	{
		public IR_Dereference() { irType = NT.DEREFERENCE; }
		public IR target;
	}
	
	class IR_Read : IR
	{
		public IR_Read() { irType = NT.READ; }
		public IR target;
	}
	
	abstract class IR_Cast : IR
	{
		public IR from;
	}
	
	class IR_Extend : IR_Cast {
		public IR_Extend() : base() { irType = NT.EXTEND; }
	}
	class IR_Truncate : IR_Cast {
		public IR_Truncate() : base() { irType = NT.TRUNCATE; }
	}
	class IR_Reinterpret : IR_Cast {
		public IR_Reinterpret() : base() { irType = NT.REINTERPRET; }
	}
	class IR_FloatToInt : IR_Cast {
		public IR_FloatToInt() : base() { irType = NT.FLOAT_TO_INT; }
	}
	class IR_IntToFloat : IR_Cast {
		public IR_IntToFloat() : base() { irType = NT.INT_TO_FLOAT; }
	}
	
	abstract class IR_Operation : IR
	{
		public IR_Operation() { dKind = ValueKind.VALUE; }
		public IR a, b;
	}
	
	class IR_Assign : IR_Operation {
		public IR_Assign() : base() { irType = NT.ASSIGN; }
	}
	class IR_Add : IR_Operation {
		public IR_Add() : base() { irType = NT.ADD; }
	}
	class IR_Subtract : IR_Operation {
		public IR_Subtract() : base() { irType = NT.SUBTRACT; }
	}
	class IR_Multiply : IR_Operation {
		public IR_Multiply() : base() { irType = NT.MULTIPLY; }
	}
	class IR_Divide : IR_Operation {
		public IR_Divide() : base() { irType = NT.DIVIDE; }
	}
	class IR_Modulo : IR_Operation {
		public IR_Modulo() : base() { irType = NT.MODULO; }
	}
	class IR_Xor : IR_Operation {
		public IR_Xor() : base() { irType = NT.BIT_XOR; }
	}
}