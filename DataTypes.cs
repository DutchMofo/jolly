using System.Collections.Generic;
using System.Diagnostics;

namespace Jolly
{
	class DataType
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
		
		public DataType() { this.typeID = lastTypeID++; }
		public DataType(int size, int align) { this.size = size; this.align = align; this.typeID = lastTypeID++; }
		
		public virtual DataType getMember(string name) => null;
		public virtual DataType getChild(string name) => null;
		
		public override bool Equals(object obj) => obj == this;
		public override int GetHashCode() => typeID;
		
		public override string ToString() => name;
	}
	
	class DataTypeReference : DataType
	{
		public DataType referenced;
		
		public DataTypeReference(DataType referenced)
			{ this.referenced = referenced; }
		
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
	
	class DataTypeArray : DataType
	{
		public DataType collectionType;
		public int length;
				
		public override bool Equals(object obj)
		{
			var arr = obj as DataTypeArray;
			if(arr != null)
				return arr.length == length && arr.collectionType == collectionType;
			return false;
		}
		public override int GetHashCode()
			=> collectionType.GetHashCode() & length;
			
		public override string ToString() => collectionType + "[]";
	}
	
	class DataTypeStruct : DataType
	{
		public TableFolder structScope;
		public Dictionary<string, int> memberMap = new Dictionary<string, int>();
		public DataType[] members;
		
		public override DataType getMember(string name)
		{
			int index;
			if(memberMap.TryGetValue(name, out index))
				return members[index];
			return null;
		}
		
		public override string ToString() => name;
	}
	
	class DataTypeFunction : DataType
	{
		public DataTypeFunction(DataType[] returns, DataType[] arguments)
		{
			Debug.Assert(returns.Length > 0);
			this.arguments = arguments;
			this.returns = returns;
		}
		
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