namespace Guesser
{   
    public enum Tokens
    { 
      EOF = 0, maxParseToken = int.MaxValue 
    }

    public abstract class ScanBuff
    {
        public const int EOF = -1;
        public abstract int Pos { get; set; }
        public abstract int Read();
    }
}