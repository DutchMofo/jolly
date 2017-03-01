namespace dev
{

class S {
	public bool isGeneric;
	public bool isStatic;
	
	public S type;
}

class ST : S {
	
}

	
}

/*


1(4) {
	2(2) { }
	
	3(4) {
		4(4) { }
	}
}
5(6) {
	6(6) { }
}

1 : 0 {
	2;
	3;
}
2 : 1 { }
3 : 1 {
	4;
}
4 : 3 { }
5 : 0 {
	6;
}
6 : 5 { }


*/