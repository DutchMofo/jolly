using System.Collections.Generic;

namespace Jolly
{
using NT = Node.NodeType;
using TT = Token.Type;

static class Analyser
{
	static void analyseOperator(Operator n)
	{
		switch(n.operation)
		{
		case TT.MULTIPLY: break;
		case TT.GET_MEMBER: break;
		}
	}
	
	public static void analyse(List<Node> program)
	{
		foreach(Node node in program)
		{
			switch(node.nodeType)
			{
			case NT.FUNCTION: break;
			case NT.OPERATOR: analyseOperator((Operator)node); break;
			}
		}
	}
}

}