using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Land.Core.Specification;
using Land.Markup.CoreExtension;

namespace Land.Control
{
	public class LanguageSettings
	{
		public bool UseHorizontalContext { get; private set; }

		public LanguageSettings(SymbolOptionsManager opts)
		{
			UseHorizontalContext = opts.IsSet(MarkupOption.USEHORIZONTAAL);
		}
	}
}
