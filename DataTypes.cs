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
		public int size, align, typeID;
		public Flags flags;
		
		public DataType() { this.typeID = lastTypeID++; }
		public DataType(int size, int align, Flags flags)
		{
			this.size = size;
			this.align = align;
			this.typeID = lastTypeID++;
		}
		
		public virtual Value? getDefinition(string name) => null;
		
		public override bool Equals(object obj) => obj == this;
		public override int GetHashCode() => typeID;
		public override string ToString() => name;
	}
	
	class DataType_Reference : DataType
	{
		public DataType referenced;
		
		public DataType_Reference(DataType referenced)
			{ this.referenced = referenced; flags = Flags.BASE_TYPE | Flags.INSTANTIABLE; }
		
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
		public Scope structScope;
		public Dictionary<string, int> memberMap = new Dictionary<string, int>();
		public DataType[] members;
		
		public override Value? getDefinition(string name)
		{
			int index;
			if(memberMap.TryGetValue(name, out index)) {
				return new Value { type = members[index], kind = Value.Kind.VALUE };
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
				
		public override string ToString() => name;
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
		
		public override string ToString() => name;
	}
}