using System.Collections.Generic;
using System.Diagnostics;

namespace Jolly
{
	class DataType
	{
		public enum Flags
		{
			NONE         = 0,
			BASE_TYPE    = 1<<0,
			INSTANTIABLE = 1<<1,
		}
		
		static int lastTypeID;
		static Dictionary<DataType, DataType>
			allReferenceTypes = new Dictionary<DataType, DataType>();
			
		public static void makeUnique(ref DataType dataType)
		{
			DataType found;
			Debug.Assert(dataType != null);
			if(!allReferenceTypes.TryGetValue(dataType, out found)) {
				allReferenceTypes.Add( dataType, dataType );
			} else {
				dataType = found;
			}
		}
		
		public string name; // TODO: Remove someday
		public int size, typeID;
		public Flags flags;
		public byte align;
		
		public DataType() { this.typeID = lastTypeID++; }
		public DataType(int size, byte align, Flags flags)
		{
			this.size = size;
			this.flags = flags;
			this.align = align;
			this.typeID = lastTypeID++;
		}
		
		public virtual Value? getMember(AST_Node node, string name, List<IR> instruction) { return null; }
		
		public override bool Equals(object obj) => obj == this;
		public override int GetHashCode() => typeID;
		public override string ToString() => name;
	}
	
	class DataType_Reference : DataType
	{
		public DataType referenced;
		public byte depth; // How many pointers are there "int**" == 2, "int*" == 1
		
		public DataType_Reference(DataType referenced)
		{
			this.referenced = referenced;
			this.flags = Flags.BASE_TYPE | Flags.INSTANTIABLE;
			this.depth = (byte)(((referenced as DataType_Reference)?.depth ?? 1) + 1);
		}
		
		public override bool Equals(object obj)
		{
			var refr = obj as DataType_Reference;
			if(refr != null)
				return refr.referenced == referenced;
			return false;
		}
		public override int GetHashCode()
			=> referenced.GetHashCode() << 3;
		
		public override string ToString() => referenced + "*";
	}
	
	// class DataTypeArray : DataType
	// {
	// 	public DataType collectionType;
	// 	public int length;
				
	// 	public override bool Equals(object obj)
	// 	{
	// 		var arr = obj as DataTypeArray;
	// 		if(arr != null)
	// 			return arr.length == length && arr.collectionType == collectionType;
	// 		return false;
	// 	}
	// 	public override int GetHashCode()
	// 		=> collectionType.GetHashCode() & length;
			
	// 	public override string ToString() => collectionType + "[]";
	// }
	
	class DataType_Struct : DataType
	{
		public DataType_Struct() { flags = Flags.INSTANTIABLE; }
		public Dictionary<string, int> memberMap = new Dictionary<string, int>();
		public DataType[] members;
		public DataType_Struct inherits;
		
		public override Value? getMember(AST_Node node, string name, List<IR> instructions)
		{
			int index;
			DataType_Struct iterator = this;
			while(iterator != null)
			{
				if(iterator.memberMap.TryGetValue(name, out index))
				{
					Value _struct = node.result;
					if(iterator != this) {
						_struct = Analyser.newResult(new Value{ type = new DataType_Reference(iterator), kind = Value.Kind.STATIC_TYPE });
						makeUnique(ref _struct.type);
						
						instructions.Add(new IR_Bitcast{ from = node.result, result = _struct });
					}
					
					var result = new Value { type = iterator.members[index], kind = Value.Kind.VALUE };
					result.type = new DataType_Reference(result.type);
					makeUnique(ref result.type);
					
					instructions.Add(new IR_GetMember{ _struct = _struct, index = index + (iterator.inherits == null ? 0 : 1), result = result = Analyser.newResult(result) });
					return result;
				}
				iterator = iterator.inherits;
			}
			return null;
		}
		
		public bool addDefinition(string name)
		{
			if(memberMap.ContainsKey(name)) {
				return false;
			}
			memberMap[name] = memberMap.Count;
			return true;
		}
		
		public void finishDefinition(string name, DataType type)
			=> members[ memberMap[name] ] = type;
				
		public override string ToString() => '%'+name;
	}
	
	class DataType_Function : DataType
	{
		public DataType[] returns, arguments;
		
		public override bool Equals(object obj)
		{
			var arr = obj as DataType_Function;
			if(arr != null) {
				return !(returns.any((r, i) => arr.returns[i] != r) || arguments.any((a, i) => arr.arguments[i] != a));
			}
			return false;
		}
		public override int GetHashCode()
		{
			int hash = 0;
			arguments.forEach(a => hash ^= a.GetHashCode());
			returns.forEach(a => hash ^= a.GetHashCode());
			return hash;
		}
		
		public override string ToString() => '%'+name;
	}
}