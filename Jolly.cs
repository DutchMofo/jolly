using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
// using System.Net;
// using System.Net.Sockets;
// using Mono.Unix;

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
	
	// static class DebugLog
	// {
	// 	static Socket _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
		
	// 	static DebugLog()
	// 	{
	// 		_socket.Connect(new UnixEndPoint("/tmp/jolly"));
	// 	}
		
	// 	public static void Log(object item)
	// 	{
	// 		_socket.Send(System.Text.Encoding.UTF8.GetBytes(item.ToString()));
	// 	}
	// }
	
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
		
		public override string ToString()
			=> "{0}:{1}: {2}: {3}".fill(location.line, location.column, type.ToString().ToLower(), text);
	}
	
	class Jolly
	{
		static int /*maxMessageLine = 0, maxMessageColumn = 0,*/ errorCount = 0;
		static List<Message> messages = new List<Message>();
		public static int SIZE_T_BYTES = 8;
		
		public static string formatEnum<T>(T val)
			=> val.ToString().ToLower().Replace('_', ' ');
		
		public static ParseException addError(SourceLocation location, string message)
		{
			++errorCount;
			messages.Add(new Message { location = location, text = message, type = Message.MessageType.ERROR });
			return new ParseException();
		}
		
		public static void addWarning(SourceLocation location, string message)
			=> messages.Add(new Message { location = location, text = message, type = Message.MessageType.WARNING });
			
		
		public static ParseException unexpected(Token token)
			=> addError(token.location, "Unexpected {0}".fill(token));
		
		public static ParseException unexpected(Node node)
			=> addError(node.location, "Unexpected {0}".fill(node.nodeType));
		
		static void printMessages()
		{
			Console.WriteLine();
			messages.forEach(m => Console.WriteLine(m));
		}
		
		public static void Main(string[] args)
		{
			List<Node> program;
			string source = File.ReadAllText("Program.jolly");
			var tokens = new Tokenizer().tokenize(source, "Program.jolly");
			
			// Console.WriteLine("Tokens:");
			// tokens.forEach(Console.WriteLine);
						
			program = new List<Node>(tokens.Length / 2);
			new ScopeParser(0, tokens.Length-1, TableFolder.root, tokens, program).parseBlock();
			
			program = Analyser.analyse(program);
			
			Console.WriteLine("Nodes:");
			program.forEach(Console.WriteLine);
			Console.WriteLine("");
			
			Console.WriteLine("/");
			TableFolder.root.PrintTree("", 0);
			
			printMessages();
			Debugger.Break();
		}
	}
}