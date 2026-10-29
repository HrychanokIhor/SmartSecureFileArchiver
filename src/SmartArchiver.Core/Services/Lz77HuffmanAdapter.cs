using System;
using System.IO;
using SmartSecureFileArchiver.Core.Interfaces;
using SmartSecureFileArchiver.Core.Models;

namespace SmartSecureFileArchiver.Core.Services
{
    /// <summary>
    /// Представляє токен для модуля Huffman (або літерал, або збіг LZ77).
    /// </summary>
    public class HuffmanToken
    {
        public bool IsLiteral { get; private set; }
        public byte LiteralValue { get; private set; }
        public ushort MatchLength { get; private set; }
        public ushort MatchDistance { get; private set; }

        private HuffmanToken() { }

        public static HuffmanToken FromLiteral(byte value)
        {
            return new HuffmanToken
            {
                IsLiteral = true,
                LiteralValue = value
            };
        }

        public static HuffmanToken FromMatch(ushort length, ushort distance)
        {
            return new HuffmanToken
            {
                IsLiteral = false,
                MatchLength = length,
                MatchDistance = distance
            };
        }
    }

    public static class Lz77HuffmanAdapter
    {
        public static HuffmanToken ToHuffman(ILz77Token token) => token switch
        {
            LiteralToken literal => HuffmanToken.FromLiteral(literal.Value),
            MatchToken match when match.Length > 0 && match.Length <= ushort.MaxValue
                && match.Distance > 0 && match.Distance <= ushort.MaxValue
                => HuffmanToken.FromMatch((ushort)match.Length, (ushort)match.Distance),
            _ => throw new InvalidDataException("Непідтримуваний LZ77-токен.")
        };

        public static ILz77Token FromHuffman(HuffmanToken token)
        {
            if (token.IsLiteral)
            {
                return new LiteralToken(token.LiteralValue);
            }
            else
            {
                // Увага: конструктор MatchToken приймає спочатку distance, а потім length
                return new MatchToken(token.MatchDistance, token.MatchLength);
            }
        }
    }
}