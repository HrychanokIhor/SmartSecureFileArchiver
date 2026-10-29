using SmartSecureFileArchiver.Core.Interfaces;

namespace SmartSecureFileArchiver.Core.Models
{
    /// <summary>
    /// Токен-літерал (окремий байт, який не вдалося стиснути посиланням).
    /// </summary>
    public class LiteralToken : ILz77Token
    {
        public byte Value { get; set; }

        public LiteralToken(byte value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// Токен-збіг (посилання на раніше зустрінуту послідовність у буфері).
    /// </summary>
    public class MatchToken : ILz77Token
    {
        public ushort Distance { get; set; }
        public ushort Length { get; set; }

        public MatchToken(ushort distance, ushort length)
        {
            Distance = distance;
            Length = length;
        }
    }
}