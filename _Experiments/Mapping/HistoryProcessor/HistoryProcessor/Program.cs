using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace HistoryProcessor
{
	public enum EntityType { Method, Property, Class }

	public class EntityChange
	{
		public EntityType EntityType { get; set; }

		public bool Modifiers { get; set; }
		public bool Type { get; set; }
		public bool Name { get; set; }

		public bool ArgsNumber { get; set; }
		public bool ArgsType { get; set; }
		public bool ArgsName { get; set; }

		public EntityChange() { }

		public EntityChange(string statLine)
		{
			var elements = statLine.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			this.Modifiers = elements[1][0] == '1';
			this.Type = elements[1][1] == '1';

			switch (elements[0])
			{
				case "M":
					this.Name = elements[1][2] == '1';
					this.ArgsNumber = elements[2][0] == '1';
					this.ArgsType = elements[2][1] == '1';
					this.ArgsName = elements[2][2] == '1';

					this.EntityType = EntityType.Method;
					break;
				case "P":
					this.Name = elements[1][2] == '1';

					this.EntityType = EntityType.Property;
					break;
				case "C":
					this.EntityType = EntityType.Class;			
					break;
			}
		}
	}

	public class Commit
	{
		public List<EntityChange> Changes { get; set; } = new List<EntityChange>();
	}

	public class Program
	{
		public static List<Commit> History { get; set; } = new List<Commit>();

		public static void Main(string[] args)
		{
			var lines = File.ReadAllLines("../../../../history.txt");
			Commit currentCommit = null;

			foreach(var line in lines.Skip(1).Where(l=>l.Length > 0))
			{
				if (line.StartsWith("*"))
				{
					var splitted = line.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

					if(splitted.Length > 1)
					{
						var skipped = int.Parse(splitted[1].TrimStart('+'));

						for (var i = 0; i < skipped; ++i)
							History.Add(new Commit());
					}

					currentCommit = new Commit();
					History.Add(currentCommit);
				}
				else
				{
					currentCommit.Changes.Add(new EntityChange(line));
				}
			}

			// History = History.Skip(30).ToList();

			Console.WriteLine(History.Count);
			Console.WriteLine(History.Sum(c => c.Changes.Count(ch => ch.EntityType == EntityType.Method && ch.Modifiers)));
			Console.WriteLine(History.Sum(c => c.Changes.Count(ch => ch.EntityType == EntityType.Method && ch.Type)));
			Console.WriteLine(History.Sum(c => c.Changes.Count(ch => ch.EntityType == EntityType.Method && ch.Name)));
			Console.WriteLine(History.Sum(c => c.Changes.Count(ch => ch.EntityType == EntityType.Method && ch.ArgsNumber)));
			Console.WriteLine(History.Sum(c => c.Changes.Count(ch => ch.EntityType == EntityType.Method && ch.ArgsType)));
			Console.WriteLine(History.Sum(c => c.Changes.Count(ch => ch.EntityType == EntityType.Method && ch.ArgsName)));
			Console.ReadLine();
		}
	}
}
