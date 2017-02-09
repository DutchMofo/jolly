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
		
		static int lastTypeID = 40;
		public static Dictionary<DataType, DataType>
			allReferenceTypes = new Dictionary<DataType, DataType>();
			
		public static void makeUnique(ref DataType dataType)
		{
			DataType found;
			Debug.Assert(dataType != null);
			if(!allReferenceTypes.TryGetValue(dataType, out found)) {
				dataType.typeID = ++lastTypeID;
				allReferenceTypes.Add( dataType, dataType );
			} else {
				dataType = found;
			}
		}
		
		public int size, typeID;
		public string name;
		public Flags flags;
		public byte align;
		
		public virtual Value? getMember(Value i, string name, List<IR> instruction) => null;
		public virtual Value? implicitCast(Value i, DataType to, List<IR> instructions) => null;
		public virtual Value? subscript(Value i, Value subscript, List<IR> instructions) => null;
		
		public override string ToString() => name;
	}
	
	class DataType_Reference : DataType
	{
		public DataType referenced;
		public byte depth; // How many pointers are there "int**" == 2, "int*" == 1, TODO: Do I even use this?
		
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
		
	class DataType_Enum : DataType
	{
		public DataType_Enum() { flags = Flags.INSTANTIABLE; }
		
		public DataType inherits = Lookup.I32;
		public override string ToString() => inherits.ToString();
	}
	
	class DataType_Struct : DataType
	{
		public DataType_Struct() { flags = Flags.INSTANTIABLE; }
		public Dictionary<string, int> memberMap = new Dictionary<string, int>();
		public DataType[] members;
		public DataType_Struct inherits;
		
		public override Value? getMember(Value i, string name, List<IR> instructions)
		{
			int index;
			DataType_Struct iterator = this;
			while(iterator != null)
			{
				if(iterator.memberMap.TryGetValue(name, out index))
				{
					Value _struct = i;
					if(iterator != this) {
						DataType reference = new DataType_Reference(iterator);
						makeUnique(ref reference);
						_struct = Lookup.doCast<IR_Bitcast>(i, reference);
					}
					return Lookup.getMember(_struct, index + (iterator.inherits == null ? 0 : 1), iterator.members[index]);
				}
				iterator = iterator.inherits;
			}
			return null;
		}
		
		public override Value? implicitCast(Value i, DataType to, List<IR> instructions)
		{
			if(!(to is DataType_Reference)) {
				return null;
			}
			DataType_Struct iterator = this;
			while(iterator != null)
			{
				if(iterator == to) {
					DataType reference = new DataType_Reference(iterator);
					makeUnique(ref reference);
					return Lookup.doCast<IR_Bitcast>(i, reference);
				}
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
	
	class DataType_Array : DataType
	{
		public DataType_Array(DataType collectionType) { this.collectionType = collectionType; }
		
		public DataType collectionType;
		public int count = 10;
				
		public override bool Equals(object obj)
		{
			var arr = obj as DataType_Array;
			if(arr != null) return arr.count == count && arr.collectionType == collectionType;
			return false;
		}
		public override int GetHashCode()
			=> collectionType.GetHashCode() << 7 & count;
		
		public override Value? getMember(Value i, string name, List<IR> instructions)
		{
			if(name == "count") return Lookup.doCast<IR_Bitcast>(i, Lookup.I64);
			if(name == "data") {
				DataType reference = new DataType_Reference(collectionType);
				makeUnique(ref reference);
				return Lookup.getMember(i, 1, reference);
			}
			return null;
		}
		public override Value? implicitCast(Value i, DataType to, List<IR> instructions) => null;
		public override Value? subscript(Value i, Value subscript, List<IR> instructions) => null;
		
		public override string ToString() => "struct {{ i64, [{0} x {1}] }}".fill(count, collectionType);
	}
}