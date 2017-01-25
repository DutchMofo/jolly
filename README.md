# Jolly
Attempting to make a compiler for a made up programming language.

```c++
// The rest of the file is in the Jolly.Uncertain namespace
namespace Jolly.Uncertain;

auto n1 = 1;  // i32
auto n3 = .0; // float
auto n5 = ""; // string

struct Foo
{
	FooType type;
	string name;
	i32* someCounter;
}

enum FooType : ubyte
{
	ONE = 10,
	TWO,
	THREE,
}

// Multiple return values
bool, Foo* bar()
{
	return false, null;
}

i32 addThreei32s(i32 a, i32 b, i32 c)
{
	return a + b + c;
}

i32 main()
{
	// Unicode variable names
	f32 π = 3.14159265359;
	// Cast f32 to i32
	i32 three = (i32: π);
	
	u8* data = new u8[100];
	// Defer a statement so it get run at the end of the scope
	defer delete data;
	
	// An array is a collection containing a static amount of items,
	// indexing operations are bounds checked.
	i32[10] i32_array = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
	
	// A slice contains a reference to data and a count,
	// indexing operations are bounds checked.
	i32[:] slice = i32_array[1:-1];
	
	// Raw pointer, indexing operations are not bounds checked (No count).
	i32* sliceData = &slice[0];
	
	// Initializing struct with object (??? Not sure what is's called).
	Example example = {
		type = FooType.THREE,
		name = "Example name",
	};
	
	// Normal function call.
	addThreei32s(3, 2, 1); // 5
	
	// Curry-ing and partial application.
	auto curry_1 = curry(addThreei32s); // Not really usefull
	auto curry_2 = curry(addThreei32s)(3);
	auto curry_3 = curry(addThreei32s)(3, 2);
	
	// Calling curry-ed functions.
	curry_1(3, 2, 1); // 5
	curry_2(2, 1);    // 5
	curry_3(1);       // 5
	
	Example example_2;
	// Assign to the type and name members at the same time
	example_2.(type, name) = example.(type, name);
	
	{
		auto (status, foo) = bar();
		i32* someCounter = foo?.someCounter;
	}
	
	// Initialize if condition.
	if(auto (status, foo) = bar(); status) {
		// ...
	}
	
	// Initialize switch condition.
	switch(auto (status, foo) = bar(); status) {
		case true:  // ...
		case false: // ...
	}
	
	// Ranges, maybe later enumarators.
	for(i32 i in 0..10) { // Range
		pri32f("%d, ", i); // 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 
	}
		
	return 0;
}
```