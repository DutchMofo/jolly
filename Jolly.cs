using System;
using System.Collections.Generic;
using System.IO;

namespace Jolly
{
    // using TT = Token.Type;
	
	class ParseException : System.Exception
	{
		public ParseException() { }
	}
	
	static class Extensions
	{
		public static void Add<T1, T2>(this IList<Tuple<T1, T2>> list,
				T1 item1, T2 item2)
		{
			list.Add(Tuple.Create(item1, item2));
		}
		
		public static string fill(this string format, params object[] args)
		{
			return string.Format(format, args);
		}
		
		public static T PopOrDefault<T>(this Stack<T> stack)
		{
			if(stack.Count == 0)
				return default(T);
			return stack.Pop();
		}
		
		public static T PeekOrDefault<T>(this Stack<T> stack)
		{
			if(stack.Count == 0)
				return default(T);
			return stack.Peek();
		}
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
		
		public static string formatEnum<T>(T val)
		{
			return val.ToString().ToLower().Replace('_', ' ');
		}
		
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
		{
			addError(token.location, "Unexpected {0}".fill(token));
		}
		
		public static void unexpected(Node node)
		{
			addError(node.location, "Unexpected {0}".fill(node.nType));
		}
		
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
		
		// public static TypeInfo[] baseTypes = new TypeInfo[TT.AUTO - TT.I8+1];
		
		public static void Main(string[] args)
		{
			// for(int _i = 0; _i < baseTypes.Length; ++_i)
			// 	baseTypes[_i] = new TypeInfo() { name = formatEnum(TT.I8+_i) };
			
 			// for(int i = 10; i < 20; ++i)
			// 	baseTypes[i] = baseTypes[i-10];
			
			string source = File.ReadAllText(args[0]);
			var tokens = new Tokenizer().tokenize(source, args[0]);
			
			if(errorCount > 0) {
				printMessages();
				return;
			}
			
			Scope scope = new Scope(new SourceLocation(0, 0, args[0]), Node.NodeType.BLOCK, null, null, SymbolFlag.global);
			List<Node> program = new List<Node>(tokens.Length >> 1);
			
			var parser = new ScopeParser(0, tokens.Length-1, scope, tokens, program);
			parser.parseBlock();
			
			program = new Analyzer().analyze(program);
			
			Console.ReadKey();
		}
	}
}