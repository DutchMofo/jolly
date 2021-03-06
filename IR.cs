using System;

namespace Jolly
{
	using StaticExec = Func<object, object, object>;
	using NT = AST_Node.Type;

    enum ValueKind : byte
	{
		UNDEFINED       = 0,
		STATIC_TYPE     = 1<<0,
		STATIC_VALUE    = 1<<1,
		STATIC_FUNCTION = 1<<2,
		ADDRES          = 1<<3,
		VALUE           = 1<<4,
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
			return new T{ a = a, b = b, dType = a.dType };
		}
		
		public static IR getMember(IR _struct, DataType result, int index)
		{
			return new IR_GetMember{ _struct = _struct, index = index, dType = result };
		}
		
		public override string ToString() => "{0} {1} {2}".fill(dType, dKind, irType);
	}
	
	class IR_Call : IR
	{
		public IR_Call() { irType = NT.FUNCTION_CALL; dKind = ValueKind.VALUE; }
		public IR[] arguments;		
		public IR target;
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
		
	class IR_Function : IR
	{
		public IR_Function() {
			dKind = ValueKind.STATIC_FUNCTION;
			irType = NT.FUNCTION;
		}
		public IRList block;
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
		public bool initialized;
		public int references;
	}
	
	class IR_Reference : IR
	{
		public IR_Reference() { irType = NT.REFERENCE; }
		public IR target;
	}
	
	class IR_Dereference : IR
	{
		public IR_Dereference() {
		   irType = NT.DEREFERENCE;
		   dKind = ValueKind.ADDRES;
		}
		public IR target;
	}
	
	class IR_Return : IR
	{
		public IR_Return() { irType = NT.RETURN; }
		public IR value;
	}
	
	class IR_Read : IR
	{
		public IR_Read() {
			irType = NT.READ;
			dKind = ValueKind.VALUE;
		}
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
	
	class IR_Substript : IR_Operation {
		public IR_Substript() : base() { irType = NT.SUBSCRIPT; }
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
	class IR_BitXor : IR_Operation {
		public IR_BitXor() : base() { irType = NT.BIT_XOR; }
	}
	class IR_BitOr : IR_Operation {
		public IR_BitOr() : base() { irType = NT.BIT_OR; }
	}
	class IR_BitAnd : IR_Operation {
		public IR_BitAnd() : base() { irType = NT.BIT_AND; }
	}
	class IR_LShift : IR_Operation {
		public IR_LShift() : base() { irType = NT.SHIFT_LEFT; }
	}
	class IR_RShift : IR_Operation {
		public IR_RShift() : base() { irType = NT.SHIFT_RIGHT; }
	}
}
