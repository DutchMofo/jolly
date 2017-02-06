using System;
using System.Collections.Generic;
using System.Diagnostics;
// using System.Diagnostics;
using System.IO;

namespace Jolly
{
	using Cast = Func<Value,Value,Value>;
	
	class ParseException : System.Exception
	{
		public ParseException() { }
	}
	
	static class Extensions
	{
		public static bool TryGetValue(this Dictionary<Lookup.CastPair, Cast> dict, Value a, Value b, out Cast cast)
			=> dict.TryGetValue(new Lookup.CastPair{ _to = b.type, _from = a.type }, out cast);
		
		public static string fill(this string format, params object[] args)
			=> string.Format(format, args);
			
		public static T PopOrDefault<T>(this Stack<T> stack)
			=> (stack.Count == 0) ? default(T) : stack.Pop();
		
		public static T PeekOrDefault<T>(this Stack<T> stack)
			=> (stack.Count == 0) ? default(T) : stack.Peek();
		
		public static void forEach<T>(this IEnumerable<T> list, Action<T> action)
			{ foreach(T i in list) action(i); }
			
		public static void forEach<T>(this List<T> list, Action<T, int> action)
			{ for(int i = 0; i < list.Count; ++i) action(list[i], i); }
			
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
		public override string ToString() => "{1}:{2}".fill(sourceFile, line, column);
	}
	
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
		
		public static void addNote(SourceLocation location, string message)
			=> Console.WriteLine("{0}:{1}: note: {2}".fill(location.line, location.column, message));
			
		public static ParseException unexpected(Token token)
			=> addError(token.location, "Unexpected {0}".fill(Token.TypeToString(token.type, token)));
		
		public static ParseException unexpected(AST_Node node)
			=> addError(node.location, "Unexpected {0}".fill(formatEnum(node.nodeType)));
				
		public static void Main(string[] args)
		{
			string source = File.ReadAllText("Program.jolly");
			var tokens = new Tokenizer().tokenize(source, "Program.jolly");
			
			// Lookup.casts.forEach(i => Console.WriteLine(i.GetHashCode()));
			
			var parseData = new SharedParseData{ tokens = tokens, ast = new List<AST_Node>() };
			
			var globalScope = new SymbolTable(null){ canAllocate = true };
			new ScopeParser(parseData, tokens.Length - 1, globalScope).parseGlobalScope();
			
			var instructions = Analyser.analyse(parseData.ast, globalScope);
			
			instructions.forEach(n => Console.WriteLine(n));
            Debugger.Break();
		}
	}
}