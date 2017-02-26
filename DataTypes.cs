using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Jolly
{
	class DataType
	{
		[System.Flags]
		public enum Flags
		{
			NONE         = 0,
			INTEGER      = 1<<0,
			INSTANTIABLE = 1<<1,
			SIGNED       = 1<<2,
			UNFINISHED   = 1<<3,
		}
		
		static int lastTypeID = 40;
		public static Dictionary<DataType, DataType>
			allReferenceTypes = new Dictionary<DataType, DataType>();
			
		public static DataType makeUnique(DataType dataType) { makeUnique(ref dataType); return dataType; }
		public static void makeUnique(ref DataType dataType)
		{
			DataType found;
			Debug.Assert(dataType != null);
			if(!allReferenceTypes.TryGetValue(dataType, out found)) {
				dataType.typeID = (lastTypeID += 1);
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
		public DataType_Tuple(int memberCount) { members = new DataType[memberCount]; flags = Flags.INSTANTIABLE; }
		public DataType[] members;
		
		public override int GetHashCode() => members.Length == 0 ? 0 : members.Select(m=>m.GetHashCode()).Aggregate((a,b)=>a << 7 & b);
		public override bool Equals(object obj)
		{
			var other = obj as DataType_Tuple;
			if(other == null) return false;
			return other.members.Length == members.Length && other.members.all((m, i) => m == members[i]);
		}
		
		public override IR getMember(IR i, int index, IRList list)
		{
			if(index >= members.Length) {
				throw Jolly.addError(new SourceLocation(), "Out of index bounds");
			}
			return list.Add(IR.getMember(i, members[index], index));
		}
		
		public override string ToString() => '('+members?.implode(", ")+')';
	}
	
	class DataType_Reference : DataType
	{
		public DataType referenced;
		
		public DataType_Reference(DataType referenced)
		{
			this.referenced = referenced;
			this.flags = Flags.INSTANTIABLE;
		}
		
		public override bool Equals(object obj)
		{
			var refr = obj as DataType_Reference;
			if(refr != null)
				return refr.referenced == referenced;
			return false;
		}
		
		public override IR subscript(IR i, IR index, IRList list)
		{
			if(index.dType != Lookup.I32) {
				throw Jolly.addError(new SourceLocation(), "Only int can be used as subscript");
			}
			return list.Add(IR.operation<IR_Substript>(i, index, null));
		}
		
		public override int GetHashCode() => referenced.GetHashCode() << 3;
	}
	
	class DataType_Enum : DataType
	{
		public DataType_Enum() { flags = Flags.INSTANTIABLE; }
		
		public DataType inherits = Lookup.I32;
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
	}
	
	class DataType_Function : DataType
	{
		public DataType[] arguments;
		public DataType returns;
		
		public override bool Equals(object obj)
		{
			var arr = obj as DataType_Function;
			if(arr != null) {
				return arguments.Length == arr.arguments.Length &&
					returns == arr.returns && arguments.all((a, i) => arr.arguments[i] == a);
			}
			return false;
		}
		
		public override int GetHashCode()
		{
			int hash = returns.GetHashCode();
			arguments.forEach(a => hash ^= a.GetHashCode());
			return hash;
		}
	}
	
	class DataType_Array_Data : DataType
	{
		public DataType_Array_Data(DataType collectionType) { this.collectionType = collectionType; }
		
		public DataType collectionType;
		public int count = 10;
				
		public override bool Equals(object obj)
		{
			var arr = obj as DataType_Array_Data;
			if(arr != null) return arr.count == count && arr.collectionType == collectionType;
			return false;
		}
		
		public override int GetHashCode() => collectionType.GetHashCode() << 7 ^ count;
		
		public override IR subscript(IR i, IR index, IRList list)
		{
			if(index.dType != Lookup.I32) {
				throw Jolly.addError(new SourceLocation(), "Only int can be used as subscript");
			}
			return list.Add(IR.operation<IR_Substript>(i, index, null));
		}
	}
}