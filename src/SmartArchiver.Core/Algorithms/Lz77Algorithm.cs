using System;
using System.Collections.Generic;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Models;

namespace SmartArchiver.Core.Algorithms
{
    public class Lz77Algorithm
    {
        public byte[] Decode(IEnumerable<ILz77Token> tokens)
        {
            var buffer = new List<byte>();
            foreach(var token in tokens)
            {
                if (token is LiteralToken literal)
                {
                    buffer.Add(literal.Value);
                }
                else if (token is MatchToken match)
                {
                    for (int i = 0; i < match.Length; i++)
                    {
                        int copyIndex = buffer.Count - match.Distance;

                        if (copyIndex < 0)
                        {
                            throw new InvalidOperationException("Помилка дкодування - відстань посилання виходить за межі буфера.");

                        }
                        byte b = buffer[copyIndex];
                        buffer.Add(b);
                    }
                }
            }
            return buffer.ToArray();
        }
    }
}