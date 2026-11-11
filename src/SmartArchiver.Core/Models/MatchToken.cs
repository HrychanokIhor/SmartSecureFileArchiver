using SmartArchiver.Core.Interfaces;

namespace SmartArchiver.Core.Models
{
    public class MatchToken : ILz77Token
    {
        public int Distance { get; }
        public int Length { get; }

        public MatchToken(int distance, int length)
        {
            Distance = distance;
            Length = length;
        }
        public override string ToString()
        {
            return $"Match(Dist: {Distance}, Len: {Length})";
        }
    }
}