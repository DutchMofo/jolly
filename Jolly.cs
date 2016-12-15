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
		static int maxMessageLine = 0, maxMessageColumn = 0, errorCount = 0;
		static List<Message> messages = new List<Message>();
		public static TableFolder symbolTable = new TableFolder(null);
		public static int SIZE_T_BYTES = 8;
		
		public static string formatEnum<T>(T val)
			=>  val.ToString().ToLower().Replace('_', ' ');
		
		public static void addError(SourceLocation location, string message)
		{
			++errorCount;
			if (location.column > maxMessageColumn) maxMessageColumn = location.column;
			if (location.line > maxMessageLine) maxMessageLine = location.line;
			messages.Add(new Message {
				location = location,
				text = message,
				type = Message.MessageType.ERROR,
			});
		}
		
		public static void addWarning(SourceLocation location, string message)
		{
			if (location.column > maxMessageColumn) maxMessageColumn = location.column;
			if (location.line > maxMessageLine) maxMessageLine = location.line;
			messages.Add(new Message {
				location = location,
				text = message,
				type = Message.MessageType.WARNING,
			});
		}
		
		public static void unexpected(Token token)
			=> addError(token.location, "Unexpected {0}".fill(token));
		
		public static void unexpected(Node node)
			=> addError(node.location, "Unexpected {0}".fill(node.nType));
		
		
		static void printMessages()
		{
			int columnDigits = (int)Math.Log10((double)maxMessageColumn) + 1,
				lineDigits = (int)Math.Log10((double)maxMessageLine) + 1;
			
			foreach (var m in messages)
				Console.WriteLine(
					m.location.line.ToString().PadLeft(lineDigits, '0') + ':' +
					m.location.column.ToString().PadLeft(columnDigits, '0') + ": " +
					m.type.ToString().ToLower() + ": " + m.text);
			
			Console.ReadKey();
		}
		
		public static void Main(string[] args)
		{
			string source = File.ReadAllText(args[0]);
			var tokens = new Tokenizer().tokenize(source, args[0]);
			
			if(errorCount > 0) {
				printMessages();
				return;
			}
			
			List<Node> program = new List<Node>(tokens.Length / 2);
			var parser = new ScopeParser(0, tokens.Length-1, symbolTable, tokens, program);
			parser.parseBlock();
			
			// Calculate the size off all types
			symbolTable.calculateSize();
			
			// program = new Analyzer().analyze(program);
			
			Console.ReadKey();
		}
	}
}