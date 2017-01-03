using System;
using System.Collections.Generic;
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
			
		public static T PeekOrDefault<T>(this Stack<T> stack)
			=> (stack.Count == 0) ? default(T) : stack.Peek();
		
		public static void forEach<T>(this IEnumerable<T> list, Action<T> action)
			{ foreach(T i in list) action(i); }
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
	
	struct Message
	{
		public enum MessageType
		{
			WARNING,
			ERROR,
		}
		public string text;
		public MessageType type;
		public SourceLocation location;
	}
	
	class Jolly
	{
		static int /*maxMessageLine = 0, maxMessageColumn = 0,*/ errorCount = 0;
		static List<Message> messages = new List<Message>();
		public static int SIZE_T_BYTES = 8;
		
		public static string formatEnum<T>(T val)
			=> val.ToString().ToLower().Replace('_', ' ');
		
		public static void addError(SourceLocation location, string message)
		{
			++errorCount;
			messages.Add(new Message { location = location, text = message, type = Message.MessageType.ERROR });
		}
		
		public static void addWarning(SourceLocation location, string message)
			=> messages.Add(new Message { location = location, text = message, type = Message.MessageType.WARNING });
		
		public static void unexpected(Token token)
			=> addError(token.location, "Unexpected {0}".fill(token));
		
		public static void unexpected(Node node)
			=> addError(node.location, "Unexpected {0}".fill(node.nodeType));
		
		static void printMessages()
		{
			foreach (var m in messages)
				Console.WriteLine(
					m.location.line.ToString() + ':' +
					m.location.column.ToString() + ": " +
					m.type.ToString().ToLower() + ": " + m.text);
		}
		
		public static void Main(string[] args)
		{
			try {
			string source = File.ReadAllText("Program.jolly");
			var tokens = new Tokenizer().tokenize(source, "Program.jolly");
			
			Console.WriteLine("Tokens:");
			tokens.forEach(Console.WriteLine);
			
			List<Node> program = new List<Node>(tokens.Length / 2);
			new ScopeParser(0, tokens.Length-1, TableFolder.root, tokens, program).parseBlock();
			
			Console.WriteLine("\nNodes:");
			program.forEach(Console.WriteLine);
			
			Analyser.analyse(program);
			
			} catch(ParseException ex) { ex.ToString(); }
			printMessages();
		}
	}
}