using System.Collections.Generic;
using System.Diagnostics;

namespace Jolly
{
	class DataType : SymbolTable
	{
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
		public bool isBaseType;
		
		public DataType() { this.typeID = lastTypeID++; }
		public DataType(int size, int align)
		{
			this.size = size;
			this.align = align;
			this.isBaseType = true;
			this.typeID = lastTypeID++;
		}
		
		public override Symbol? getDefinition(string name) => null;
		
		public override bool Equals(object obj) => obj == this;
		public override int GetHashCode() => typeID;
		
		public override string ToString() => name;
	}
	
	class DataTypeReference : DataType
	{
		public DataType referenced;
		
		public DataTypeReference(DataType referenced)
			{ this.referenced = referenced; this.isBaseType = true; }
		
		public override bool Equals(object obj)
		{
			var refr = obj as DataTypeReference;
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
	
	class DataTypeStruct : DataType
	{
		public Scope structScope;
		public Dictionary<string, int> memberMap = new Dictionary<string, int>();
		public DataType[] members;
		
		public override Symbol? getDefinition(string name)
		{
			int index;
			if(memberMap.TryGetValue(name, out index)) {
				return new Symbol { dataType = members[index], typeKind = TypeKind.VALUE };
			}
			return null;
		}
		
		public override bool addDefinition(string name)
		{
			if(memberMap.ContainsKey(name)) {
				return false;
			}
			memberMap[name] = memberMap.Count;
			return true;
		}
		
		public override void finishDefinition(string name, DataType type)
		{
			members[ memberMap[name] ] = type;
		}
				
		public override string ToString() => name;
	}
	
	class DataTypeFunction : DataType
	{
		public DataType[] returns, arguments;
		
		public override bool Equals(object obj)
		{
			var arr = obj as DataTypeFunction;
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