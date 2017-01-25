# Jolly
Attempting to make a compiler for a made up programming language.

```c++
namespace Jolly.Uncertain;

auto n1 = 1;  // int
auto n3 = .0; // float
auto n5 = ""; // string

struct Foo
{
	FooType type;
	string name; 
}

enum FooType : ubyte
{
	ONE = 10,
	TWO,
	THREE,
}

bool, Foo* bar()
{
	return false, null;
}

int addThreeInts(int a, int b, int c)
{
	return a + b + c;
}

int main()
{
	// An array is a collection containing a static amount of items,
	// indexing operations are bounds checked.
	int[10] array = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
	
	// A slice contains a reference to data and a count,
	// indexing operations are bounds checked.
	int[:] slice = array[1:-1];
	
	// Raw pointer, indexing operations are not bounds checked (No count).
	int* sliceData = &slice[0];
	
	// Initializing struct with object (??? Not sure what is's called).
	Example example = {
		type = FooType.THREE,
		name = "Example name",
	};
	
	// Normal function call.
	addThreeInts(3, 2, 1); // 5
	
	// Curry-ing and partial application.
	auto curry_1 = curry(addThreeInts); // Not really usefull
	auto curry_2 = curry(addThreeInts)(3);
	auto curry_3 = curry(addThreeInts)(3, 2);
	
	// Calling curry-ed functions.
	curry_1(3, 2, 1); // 5
	curry_2(2, 1);    // 5
	curry_3(1);       // 5
	
	Example example_2;
	// Assign to the type and name at the same time
	example_2.(type, name) = FooType.ONE, "The name";
	
	if(true) {
		// ...
	}
	
	// Initialize if condition.
	if(auto (status, foo) = bar(); status) {
		// ...
	}
	
	// Initialize switch condition.
	switch(auto (status, foo) = bar(); status) {
		case true: // ...
		case false: // ...
	}
	
	// Ranges, maybe later enumarators.
	for(int i in 0..10) { // Range
		printf("%d, ", i); // 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 
	}
		
	return 0;
}
```