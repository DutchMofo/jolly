using System;
using System.Collections.Generic;
using System.Diagnostics;
// using System.Diagnostics;
using System.IO;

namespace Jolly
{
	class ParseException : System.Exception
	{
		public ParseException() { }
	}
	
	static class Extensions
	{
		public static void Add<T1, T2>(this IList<Tuple<T1, T2>> list, T1 item1, T2 item2)
			=> list.Add(Tuple.Create(item1, item2));
			
		public static string fill(this string format, params object[] args)
			=> string.Format(format, args);
			
		public static T PopOrDefault<T>(this Stack<T> stack)
			=> (stack.Count == 0) ? default(T) : stack.Pop();
			
		public static ExpressionParser.Op? PopOrNull(this Stack<ExpressionParser.Op> stack)
		{
			if(stack.Count == 0)
				return null;
			return stack.Pop();
		}
			
		public static T PeekOrDefault<T>(this Stack<T> stack)
			=> (stack.Count == 0) ? default(T) : stack.Peek();
		
		public static void forEach<T>(this IEnumerable<T> list, Action<T> action)
			{ foreach(T i in list) action(i); }
			
		public static bool any<T>(this T[] list, Func<T, int, bool> action)
			{ for(int i = 0; i < list.Length; i += 1) if(action(list[i], i)) return true; return false; }
	}
	
	struct SourceLocation
	{
		public int line, column;
		public string sourceFile;
		public SourceLocation(int line, int column, string sourceFile)
		{
			this.sourceFile = sourceFile;
			this.column = column;
			this.line = line;
		}
	}
	
/*	// Hacky way of checking for double items in a list
	class List<T> : System.Collections.Generic.List<T>
	{
		HashSet<T> unique = new HashSet<T>();
		new public void Add(T item)
		{
			if(unique.Contains(item)) Debugger.Break(); else unique.Add(item);
			base.Add(item);
		}
		
		new public void AddRange(IEnumerable<T> items)
		{
			foreach(var item in items)
				if(unique.Contains(item)) Debugger.Break(); else unique.Add(item);
			base.AddRange(items);
		}
		
		new public void Insert(int index, T item)
		{
			if(unique.Contains(item)) Debugger.Break(); else unique.Add(item);
			base.Insert(index, item);
		}
	}*/
	
	class Jolly
	{
		static int errorCount = 0;
		
		public static string formatEnum<T>(T val)
			=> val.ToString().ToLower().Replace('_', ' ');
		
		public static ParseException addError(SourceLocation location, string message)
		{
			errorCount += 1;
			Console.WriteLine("{0}:{1}: error: {2}".fill(location.line, location.column, message));
			return new ParseException();
		}
		
		public static void addWarning(SourceLocation location, string message)
			=> Console.WriteLine("{0}:{1}: warning: {2}".fill(location.line, location.column, message));
			
		public static ParseException unexpected(Token token)
			=> addError(token.location, "Unexpected {0}".fill(Token.TypeToString(token.type, token)));
		
		public static ParseException unexpected(AST_Node node)
			=> addError(node.location, "Unexpected {0}".fill(formatEnum(node.nodeType)));
				
		public static void Main(string[] args)
		{
			string source = File.ReadAllText("Program.jolly");
			var tokens = new Tokenizer().tokenize(source, "Program.jolly");
			
			var parseData = new SharedParseData{ tokens = tokens, ast = new List<AST_Node>() };
			
			var globalScope = new SymbolTable(null){ canAllocate = true };
			new ScopeParser(parseData, tokens.Length - 1, globalScope).parseGlobalScope();
			
			var instructions = Analyser.analyse(parseData.ast, globalScope);
			
			instructions.forEach(n => Console.WriteLine(n));
            Debugger.Break();
		}
	}
}