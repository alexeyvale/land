namespace ICSharpCode.Decompiler.CSharp.Syntax
{
	public abstract class AstNode : AbstractAnnotatable, IFreezable, INode, ICloneable
	{
		[NotNull]
        public readonly IList<ATNState> states = new List<ATNState>();
        
		const uint frozenBit = 1u << Role.RoleIndexBits;
		
		private bool HasFlag => _index < _dynamicTransformFlags.Length || !_checkLength;
		
		private static bool s_preWin7 = (Environment.OSVersion.Version.Major < 6 || (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor < 1)); 
	}
}