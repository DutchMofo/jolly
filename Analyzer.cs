using System;
using System.Collections.Generic;

namespace Jolly
{
using NT = Node.NodeType;
using TT = Token.Type;

class Analyzer
{
	Action<Node>[] validators = new Action<Node>[(int)NT.COUNT];
	List<Node> output = new List<Node>();
	
	public Analyzer()
	{
		validators[(int)NT.OPERATOR] = analyzeOperator;
		validators[(int)NT.FUNCTION_CALL] = analyzeFunctionCall;
		validators[(int)NT.USERTYPE] = analyzeUserType;
	}
	
	public void analyzeUserType(Node n)
	{
		var userType = (Symbol)n;
		var type = userType.parentScope.getSymbol(userType.name);
		if(type == null) {
			Jolly.addError(new SourceLocation(), "Not a type");
			throw new ParseException();
		}
	}
	
	public void analyzeOperator(Node n)
	{
		var op = (Operator)n;
		if(op.operation == TT.GET_MEMBER)
		{
			if(op.a.nType == NT.NAME)
			{
				Symbol name = (Symbol)op.a;
				Scope theParent = op.a.parentScope, theScope;
				while((theScope = theParent.getSymbol(name.name) as Scope) == null) {
					if((theParent = theParent.parentScope) == null) {
						Jolly.addError(new SourceLocation(), "Member does not exist");
						throw new ParseException();
					}
				}
				op.b.parentScope = theScope;
			}
			else if(op.a.nType == NT.RESULT)
			{
				var theScope = op.a.type as Scope;
				if(theScope == null) {
					Jolly.addError(new SourceLocation(), "Has no members");
					throw new ParseException();
				}
				op.b.parentScope = theScope;
			}
			else
			{
				throw new ParseException();
			}
		} // op.operation == TT.GET_MEMBER
		else if(op.operation == TT.ASSIGN)
		{
			if(op.a.nType == NT.NAME ||
				op.a.nType == NT.VARIABLE) {
				
			}
			
			
			
		}
		else
		{
			if(op.a.nType != NT.BASETYPE || op.b.nType != NT.BASETYPE)
			{
				Jolly.addError(new SourceLocation(), "");
				throw new ParseException();
			}
			
		}
	}
	
	public void analyzeFunctionCall(Node n)
	{
		var call = (Function_call)n;
		Function func = call.parentScope.getFunction(call.functionName);
		
		if(func == null) {
			Jolly.addError(new SourceLocation(), "Not a function");
			throw new ParseException();
		}
		
	}
	
	public List<Node> analyze(List<Node> program)
	{
		foreach(Node node in program)
		{
			if(validators[(int)node.nType] != null) {
				validators[(int)node.nType](node);
			}
		}
		return output;
	}
}

}