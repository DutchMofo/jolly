using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Jolly
{
	class DataType
	{
		public enum Flags
		{
			NONE         = 0,
			BASE_TYPE    = 1<<0,
			INSTANTIABLE = 1<<1,
			SIGNED       = 1<<2,
		}
		
		static int lastTypeID = 40;
		public static Dictionary<DataType, DataType>
			allReferenceTypes = new Dictionary<DataType, DataType>();
			
		public static void makeUnique(DataType dataType) => makeUnique(ref dataType);
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
		
		public virtual IR getMember(IR i, int index, IRList list) => null;
		public virtual IR getMember(IR i, string name, IRList list) => null;
		public virtual IR implicitCast(IR i, DataType to, IRList list) => null;
		public virtual IR subscript(IR i, IR subscript, IRList list) => null;
		
		public virtual IR operator_assign(IR other) => null;
		
		public override string ToString() => name;
	}
	
	class DataType_Tuple : DataType
	{
		public DataType_Tuple(int memberCount) { members = new DataType[memberCount]; }
		public DataType[] members;
		
		public override int GetHashCode() => members.Length == 0 ? 0 : members.Select(m=>m.GetHashCode()).Aggregate((a,b)=>a << 7 & b);
		public override bool Equals(object obj)
		{
			var other = obj as DataType_Tuple;
			if(other == null) return false;
			return  other.members.Length == members.Length && other.members.all((m,i)=>m.Equals(members[i]));
		}
		
		public override IR getMember(IR i, int index, IRList list)
		{
			if(index >= members.Length) return null;
			return list.Add(IR.getMember(i, members[index], index));
		}
		
		public override string ToString() => '('+members?.implode(", ")+')';
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
		
		public override IR getMember(IR i, string name, IRList list)
		{
			int index;
			DataType_Struct iterator = this;
			while(iterator != null)
			{
				if(iterator.memberMap.TryGetValue(name, out index))
				{
					IR _struct = i;
					if(iterator != this) {
						_struct = list.Add(IR.cast<IR_Reinterpret>(i, iterator, null));
					}
					return list.Add(IR.getMember(_struct, iterator.members[index], index + (iterator.inherits == null ? 0 : 1)));
				}
				iterator = iterator.inherits;
			}
			return null;
		}
		
		public override IR implicitCast(IR i, DataType to, IRList list)
		{
			if(!(to is DataType_Reference)) {
				return null;
			}
			DataType_Struct iterator = this;
			while(iterator != null)
			{
				if(iterator == to) {
					return list.Add(IR.cast<IR_Reinterpret>(i, to, null));
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
				return returns.Length == arr.returns.Length && arguments.Length == arr.arguments.Length &&
					returns.all((r, i) => arr.returns[i] == r) && arguments.all((a, i) => arr.arguments[i] == a);
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
		
		public override IR getMember(IR i, string name, IRList list)
		{
			if(name == "count") return list.Add(IR.getMember(i, Lookup.I64, 0));
			if(name == "data")  return list.Add(IR.getMember(i, collectionType, 1));
			return null;
		}
		
		public override IR subscript(IR i, IR subscript, IRList list) => null;
		public override string ToString() => "struct {{ i64, [{0} x {1}] }}".fill(count, collectionType);
	}
}