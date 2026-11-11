using SmartArchiver.Core.Interfaces;
namespace SmartArchiver.Core.Models
{
    public class LiteralToken : ILz77Token
    {
        public byte Value { get; }
        public LiteralToken(byte value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return $"Literal('{(char)Value}')";
        }
    }
}