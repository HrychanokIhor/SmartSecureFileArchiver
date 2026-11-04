using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Models;
using SmartArchiver.Core.Services.Huffman; 

namespace SmartArchiver.Core.Services
{
    public static class Lz77HuffmanAdapter
    {
        public static IReadOnlyList<HuffmanToken> ToHuffman(IEnumerable<ILz77Token> tokens)
        {
            return tokens.Select(token => token switch
            {
                LiteralToken literal => HuffmanToken.FromLiteral(literal.Value),
                MatchToken match when match.Length > 0 && match.Length <= ushort.MaxValue
                                   && match.Distance > 0 && match.Distance <= ushort.MaxValue
                    => HuffmanToken.FromMatch((ushort)match.Length, (ushort)match.Distance),
                _ => throw new InvalidDataException("Непідтримуваний або завеликий LZ77-токен.")
            }).ToList(); // Одразу конвертуємо в список для Lz77HuffmanCodec
        }

        public static IEnumerable<ILz77Token> FromHuffman(IEnumerable<HuffmanToken> huffmanTokens)
        {
            return huffmanTokens.Select(token =>
            {
                if (token.Literal.HasValue)
                {
                    return (ILz77Token)new LiteralToken(token.Literal.Value);
                }
                
                return new MatchToken(token.Distance, token.Length);
            });
        }
    }
}