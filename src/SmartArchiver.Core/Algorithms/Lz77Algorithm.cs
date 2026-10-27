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
        public IEnumerable<ILz77Token> Encode(byte[] input)
        {
            var tokens = new List<ILz77Token>();
            int minMatch = 3;
            int maxMatch = 258;
            int windowSize = 32768;

            var dictionary = new Dictionary<int, List<int>>();

            int i = 0;
            while (i < input.Length)
            {
                int matchDistance = 0;
                int matchLength = 0;

                if (i + minMatch <= input.Length)
                {
                    int currentHash = GetHash(input, i);

                    if (dictionary.ContainsKey(currentHash))
                    {
                        var candidates = dictionary[currentHash];
                        candidates.RemoveAll(c => i - c > windowSize);

                        foreach (var candidateIndex in candidates)
                        {
                            int currentLength = 0;
                            while (currentLength < maxMatch &&
                                i + currentLength < input.Length &&
                                input[candidateIndex + currentLength] == input[i + currentLength])
                            {
                                currentLength++;
                            }

                            if (currentLength > matchLength)
                            {
                                matchLength = currentLength;
                                matchDistance = i - candidateIndex;
                            }
                        }
                    }
                }

                if (matchLength >= minMatch)
                {
                    tokens.Add(new MatchToken(matchDistance, matchLength));

                    for (int j = 0; j < matchLength; j++)
                    {
                        if (i + j + minMatch <= input.Length)
                        {
                            int h = GetHash(input, i + j);
                            if (!dictionary.ContainsKey(h)) dictionary[h] = new List<int>();
                            dictionary[h].Add(i + j);
                        }
                    }
                    i += matchLength;
                }
                else
                {
                    tokens.Add(new LiteralToken(input[i]));

                    if (i + minMatch <= input.Length)
                    {
                        int h = GetHash(input, i);
                        if (!dictionary.ContainsKey(h)) dictionary[h] = new List<int>();
                        dictionary[h].Add(i);
                    }
                    i++;
                }
            }

            return tokens;
        }

        private int GetHash(byte[] input, int index)
        {
            return (input[index] << 16) | (input[index + 1] << 8) | input[index + 2];
        }

    }
    
}