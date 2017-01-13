using System.Collections.Generic;

namespace Jolly
{
	class DataType
	{
		static int last_type_id;
		static Dictionary<DataType, DataType>
			allReferenceTypes = new Dictionary<DataType, DataType>();
		
		public string name; // TODO: Remove someday
		public int size, align, type_id;
		
		public virtual DataType getMember() => null;
	}
	
	class DataTypeReference : DataType
	{
		public override bool Equals(object obj) => obj == this;
		public override int GetHashCode() => 0;
		
		public DataType referenced;
		public byte depth;
	}
	
	class DataTypeArray : DataType
	{
		public override bool Equals(object obj) => obj == this;
		public override int GetHashCode() => 0;
		
		public DataType collectionType, countType;
		public int size;
	}
	
	class DataTypeStruct : DataType
	{
		public override bool Equals(object obj) => obj == this;
		public override int GetHashCode() => 0;
		
		public DataTypeStruct(TableFolder memberTable) 
			=> this.memberTable = memberTable;
		
		public TableFolder memberTable;
		public DataType[] members;
	}
}