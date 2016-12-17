# jolly
Attempting to make a programming language

```c
namespace Jolly.Uncertain;

auto n1 = 1;		// int
auto n3 = .0;		// float
auto n5 = "";		// string

enum ExampleType : ubyte
{
	ONE = 10,
	TWO,	// 11
	THREE	// 12
}

//Extend example
string getName(this Example example) #inline
{
	return example.name;
}

string[] texts = {
	"z", "y", "x", "w", "c", "b", "a",
};

int main()
{
	int[10] array = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };	// Static array
	int[] slice = array[:];								// Array slice
	string text = "Hello";
	
	int* sliceData = slice.data;
		sliceData = &slice[0];
	
	Example example = {
		type: ExampleType.THREE,
		name: "Example",
	};
	
	auto type = example.getName();
	type = getName(example);
	
	for(int i = 0..10) { // Range
		printf("%d, ", i); // 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 
	}
	
	switch(1)
	{
	case 9, 10:
		// Foo();
		fall;
	default:
		// Bar();
	}
	
	
	return 0;
}
```