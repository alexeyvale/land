using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Land.Core;
using Land.Core.Parsing.Tree;

namespace PascalPreprocessing.TreePostprocessing
{
	public class RoutineAggregationVisitor : BaseTreeVisitor
	{
		public List<Message> Log { get; set; } = new List<Message>();
		private List<Tuple<Node, List<Node>>> ToAggregate { get; set; } = new List<Tuple<Node, List<Node>>>();

		public override void Visit(Node node)
		{
			switch (node.Type)
			{
				/// Обработка внешних процедур/функций
				case "file":
					var fileAggrerationLists = new Stack<List<Node>>();

					for (var i = 0; i < node.Children.Count; ++i)
					{
						/// Если в настоящий момент собираем части какой-то процедуры, это ещё одна её часть
						if (fileAggrerationLists.Count > 0)
							fileAggrerationLists.Peek().Add(node.Children[i]);

						switch(node.Children[i].Type)
						{
							case "routine":
								if (node.Children[i].Children.Last().Type != "routine_init")
								{
									fileAggrerationLists.Push(new List<Node>());
									ToAggregate.Add(new Tuple<Node, List<Node>>(node.Children[i], fileAggrerationLists.Peek()));
								}
								break;
							case "modifier_headed_part":
								if(node.Children[i].Children[0].Value[0] == "external"
									&& node.Children[i].Children[0].Value[0] == "forward")
									if (fileAggrerationLists.Count > 0)
										fileAggrerationLists.Pop();
								break;
							case "block":
							case "routine_tail":
								if (fileAggrerationLists.Count > 0)
									fileAggrerationLists.Pop();
								break;					
						}

						base.Visit(node.Children[i]);
					}

					/// Отложенное выполнение всех перестановок,
					/// связанных со сбором частей описания метода
					foreach (var routinePartsPair in ToAggregate)
						foreach (var part in routinePartsPair.Item2)
							Aggregate(routinePartsPair.Item1, part);
					break;
				/// Обработка заголовков подпрограмм в интерфейсе
				case "interface_declarations":
					List<Node> interfaceAggregationList = null;

					for (var i = 0; i < node.Children.Count; ++i)
					{
						switch (node.Children[i].Type)
						{
							case "routine":
								interfaceAggregationList = new List<Node>();
								ToAggregate.Add(new Tuple<Node, List<Node>>(node.Children[i], interfaceAggregationList));
								break;
							case "const":
							case "type":
							case "var":
							case "attribute":
								interfaceAggregationList = null;
								break;
							default:
								if (interfaceAggregationList != null)
									interfaceAggregationList.Add(node.Children[i]);
								break;
						}

						base.Visit(node.Children[i]);
					}

					break;
				case "section":
					List<Node> sectionAggregationList = null;

					for (var i = 0; i < node.Children.Count; ++i)
					{
						switch (node.Children[i].Type)
						{
							case "method":
								sectionAggregationList = null;

								if (node.Children[i].Children.Last().Type != "routine_init")
								{
									sectionAggregationList = new List<Node>();
									ToAggregate.Add(new Tuple<Node, List<Node>>(node.Children[i], sectionAggregationList));
								}
								break;
							case "property":
							case "field":
							case "class_member":
								sectionAggregationList = null;
								break;
							default:
								if (sectionAggregationList != null)
									sectionAggregationList.Add(node.Children[i]);
								break;
						}

						base.Visit(node.Children[i]);
					}

					break;
				default:
					base.Visit(node);
					break;
			}
		}

		private void Aggregate(Node routine, Node child)
		{
			child.Parent.Children.Remove(child);
			routine.AddLastChild(child);
		}
	}
}
