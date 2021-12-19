using System;
using System.Collections.Generic;
using System.Linq;

using Land.Core.Specification;
using Land.Core.Parsing.Tree;

namespace Land.Core.Lexing
{
	public class CustomBlockNode
	{
		public SegmentLocation Location { get; set; }

		public Node Start { get; set; }

		public Node End { get; set; }

		public int StartOffset => Location.Start.Offset;

		public int EndOffset => Location.End.Offset;

		public CustomBlockNode Parent;

		public List<CustomBlockNode> Children = new List<CustomBlockNode>();
	}

	public class CustomBlockDefinition
	{
		public string StartLexemPrefix { get; set; } = "";

		public string StartLexemSuffix { get; set; } = "";

		public string EndLexemPrefix { get; set; } = "";

		public string EndLexemSuffix { get; set; } = "";

		public string BaseToken { get; set; }

		public bool IsStartLexem(string lexem) => 
			lexem.StartsWith(StartLexemPrefix) && lexem.EndsWith(StartLexemSuffix);

		public bool IsEndLexem(string lexem) => 
			lexem.StartsWith(EndLexemPrefix) && lexem.EndsWith(EndLexemSuffix);

		public string GetName(string blockStart) => blockStart
			.Substring(StartLexemPrefix.Length)
			.Substring(0, blockStart.Length - StartLexemPrefix.Length - StartLexemSuffix.Length)
			.Trim();
	}

	public class PairAwareState
	{
		public Stack<PairSymbol> PairStack { get; set; }

		public Direction CurrentTokenDirection { get; set; }

		public PairSymbol OpenedPair { get; set; }
	}

	public enum Direction { Forward, Up, Down };

	public class ComplexTokenStream: TokenStream, IGrammarProvided
	{
		public Grammar GrammarObject { get; private set; }

		private List<Message> Log { get; set; }

		/// <summary>
		/// Стек открытых на момент прочтения последнего токена пар
		/// </summary>
		private Stack<PairSymbol> PairStack { get; set; } = new Stack<PairSymbol>();

		/// <summary>
		/// Пара, левой границей которой является текущий токен
		/// </summary>
		private PairSymbol OpenedPair { get; set; }

		/// <summary>
		/// Направление перехода по вложенностям, которое задаётся текущим токеном
		/// </summary>
		public Direction CurrentTokenDirection { get; private set; }

		public List<CustomBlockNode> CustomBlocks { get; set; }

		public Stack<CustomBlockNode> CustomBlockStack { get; set; }

		/// <summary>
		/// Описание пользовательского блока, задаваемое грамматикой
		/// </summary>
		private CustomBlockDefinition CustomBlockDefinition { get; set; }

		public ComplexTokenStream(Grammar grammar, ILexer lexer, string text, List<Message> log): base(lexer, text)
		{
			GrammarObject = grammar;
			Log = log;

			BuildCustomBlockDefinition();
		}

		private void BuildCustomBlockDefinition()
		{
			var customBlockBase = GrammarObject.Options
				.GetSymbols(CustomBlockOption.GROUP_NAME, CustomBlockOption.BASETOKEN).FirstOrDefault();

			if (!String.IsNullOrEmpty(customBlockBase))
			{
				CustomBlockDefinition = new CustomBlockDefinition { BaseToken = customBlockBase };

				var start = GrammarObject.Options.GetParams(CustomBlockOption.GROUP_NAME, CustomBlockOption.START);

				if (start.Count > 0)
					CustomBlockDefinition.StartLexemPrefix = (string)start[0];

				if (start.Count > 1)
					CustomBlockDefinition.StartLexemSuffix = (string)start[1];

				var end = GrammarObject.Options.GetParams(CustomBlockOption.GROUP_NAME, CustomBlockOption.END);

				if (end.Count > 0)
					CustomBlockDefinition.EndLexemPrefix = (string)end[0];

				if (end.Count > 1)
					CustomBlockDefinition.EndLexemSuffix = (string)end[1];

				CustomBlocks = new List<CustomBlockNode>();
				CustomBlockStack = new Stack<CustomBlockNode>();
			}
		}

		public PairAwareState GetPairsState()
		{
			return new PairAwareState()
			{
				CurrentTokenDirection = CurrentTokenDirection,
				PairStack = new Stack<PairSymbol>(PairStack.Reverse()),
				OpenedPair = OpenedPair
			};
		}

		public int GetPairsCount()
		{
			return PairStack.Count;
		}

		public IToken MoveTo(int idx, PairAwareState state)
		{
			var token = base.MoveTo(idx);

			if (token != null)
			{
				CurrentTokenDirection = state.CurrentTokenDirection;
				PairStack = state.PairStack;
				OpenedPair = state.OpenedPair;
			}

			return token;
		}

		public override IToken GetNextToken()
		{
			switch(CurrentTokenDirection)
			{
				case Direction.Down:
					PairStack.Push(OpenedPair);
					OpenedPair = null;
					break;
				case Direction.Up:
					PairStack.Pop();
					break;
			}

			CurrentTokenDirection = Direction.Forward;

			var token = base.GetNextToken();

			if(CustomBlockDefinition != null
				&& CustomBlockDefinition.BaseToken == token.Name)
			{
				if(CustomBlockDefinition.IsStartLexem(token.Text))
				{
					var newBlock = new CustomBlockNode
					{
						Location = new SegmentLocation { Start = token.Location.Start },

						/// Можно не типизировать, типизируем при вставке блоков в дерево
						Start = new Node(Grammar.CUSTOM_BLOCK_START_TOKEN_NAME)			
					};

					newBlock.Start.SetLocation(token.Location.Start, token.Location.End);
					newBlock.Start.SetValue(CustomBlockDefinition.GetName(token.Text));

					CustomBlockStack.Push(newBlock);
				}
				else if (CustomBlockDefinition.IsEndLexem(token.Text))
				{
					/// Отлавливаем ситуацию, когда количество закрытий блока не совпадает с количеством открытий
					if (CustomBlockStack.Count == 0)
					{
						Log.Add(Message.Error(
							$"Неожиданная закрывающая конструкция {this.Developerify(token)} для пользовательского блока",
							token.Location.Start,
							addInfo: new Dictionary<MessageAddInfoKey, object>
							{
								{ MessageAddInfoKey.UnexpectedToken, token.Name },
								{ MessageAddInfoKey.UnexpectedLexeme, token.Text }
							}
						));
					}
					else
					{
						var currentBlock = CustomBlockStack.Pop();

						currentBlock.End = new Node(Grammar.CUSTOM_BLOCK_END_TOKEN_NAME);
						currentBlock.End.SetLocation(token.Location.Start, token.Location.End);
						currentBlock.Location.End = token.Location.End;

						CustomBlocks.Add(currentBlock);

						/// Добавляем произвольный блок в дерево ПБ
						if(CustomBlockStack.Any())
						{
							CustomBlockStack.Peek().Children.Add(currentBlock);
							currentBlock.Parent = CustomBlockStack.Peek();
						}
					}
				}
			}

			/// Предполагается, что токен может быть началом ровно одной пары, или концом ровно одной пары,
			/// или одновременно началом и концом ровно одной пары
			var closed = GrammarObject.Pairs.FirstOrDefault(p => p.Value.Right.Contains(token.Name));

			/// Если текущий токен закрывает некоторую конструкцию
			if (closed.Value != null)
			{
				/// и при этом не открывает её же
				if (!closed.Value.Left.Contains(token.Name))
				{
					/// проверяем, есть ли на стеке то, что можно этой конструкцией закрыть
					if (PairStack.Count == 0)
					{
						Log.Add(Message.Error(
							$"Отсутствует открывающая конструкция для парной закрывающей {this.Developerify(token)}",
							token.Location.Start,
							addInfo: new Dictionary<MessageAddInfoKey, object>
							{
								{ MessageAddInfoKey.UnexpectedToken, token.Name },
								{ MessageAddInfoKey.UnexpectedLexeme, token.Text }
							}
						));

						return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
					}
					else if (PairStack.Peek() != closed.Value)
					{
						Log.Add(Message.Error(
							$"Неожиданная закрывающая конструкция {this.Developerify(token)}, ожидается {String.Join("или ", PairStack.Peek().Right.Select(e => this.Developerify(e)))} для открывающей конструкции {String.Join("или ", PairStack.Peek().Left.Select(e => this.Developerify(e)))}",
							token.Location.Start,
							addInfo: new Dictionary<MessageAddInfoKey, object>
							{
								{ MessageAddInfoKey.UnexpectedToken, token.Name },
								{ MessageAddInfoKey.UnexpectedLexeme, token.Text },
								{ MessageAddInfoKey.ExpectedTokens, PairStack.Peek().Right.ToList() }
							}
						));

						return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
					}
					else
					{
						CurrentTokenDirection = Direction.Up;
					}
				}
				/// иначе, если текущий токен одновременно открывающий и закрывающий
				else
				{
					/// и есть что закрыть, закрываем
					if (PairStack.Count > 0 && PairStack.Peek() == closed.Value)
						CurrentTokenDirection = Direction.Up;
					else
					{
						CurrentTokenDirection = Direction.Down;
						OpenedPair = closed.Value;
					}
				}
			}
			else
			{
				var opened = GrammarObject.Pairs.FirstOrDefault(p => p.Value.Left.Contains(token.Name));

				if (opened.Value != null)
				{
					CurrentTokenDirection = Direction.Down;
					OpenedPair = opened.Value;
				}
			}

			return token;
		}

		/// <summary>
		/// Получение следующего токена, находящегося на заданном уровне вложенности пар
		/// </summary>
		public IToken GetNextToken(int level, out List<IToken> skipped)
		{
			skipped = new List<IToken>();

			while (true)
			{
				var next = GetNextToken();

				/// Возвращаем следующий токен, если перешли на искомый уровень
				/// или готовимся сделать шаг в направлении, отличном от разрешённого
				if (PairStack.Count == level
					|| next.Name == Grammar.EOF_TOKEN_NAME
					|| next.Name == Grammar.ERROR_TOKEN_NAME)
					return next;
				else
					skipped.Add(next);
			}
		}
	}
}
