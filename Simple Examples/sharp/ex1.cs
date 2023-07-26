using BaseTest;

#define SYMBOL_A
#define SYMBOL_B

namespace DirectivesTest
{
	public class TestClass: BaseTestClass
	{       
		#if SYMBOL_A
		
		public int? TestMethodA(int a, int b)
		{
			return a+b;
		}
		
		#elif SYMBOL_B
		
		public int? TestMethodB(int a, int b)
		{
			return a+b;
		}
		
		#else
		
		public int? TestMethodC(int a, int b)
		{
			return a+b;
		}
		
		#endif
	}
}