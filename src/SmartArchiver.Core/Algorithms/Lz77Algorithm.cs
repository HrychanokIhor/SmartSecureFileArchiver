using System;
using System.Collections.Generic;
using SmartArchiver.Core.Interfaces;
using SmartArchiver.Core.Models;

namespace SmartArchiver.Core.Algorithms
{
    public class Lz77Algorithm
    {
        private int _windowSize = 32768;
        private int _minMatch = 3;
        private int _maxMatch = 258;

        public int WindowSize
        {
            get => _windowSize;
            set
            {
                if (value < 1 || value > 32768) 
                    throw new ArgumentOutOfRangeException(nameof(WindowSize), "WindowSize має бути від 1 до 32768");
                _windowSize = value;
            }
        }

        public int MinMatch
        {
            get => _minMatch;
            set
            {
                if (value < 3 || value > _maxMatch) 
                    throw new ArgumentOutOfRangeException(nameof(MinMatch), $"MinMatch має бути від 3 до MaxMatch ({_maxMatch})");
                _minMatch = value;
            }
        }

        public int MaxMatch
        {
            get => _maxMatch;
            set
            {
                if (value < _minMatch || value > 258) 
                    throw new ArgumentOutOfRangeException(nameof(MaxMatch), $"MaxMatch має бути від MinMatch ({_minMatch}) до 258");
                _maxMatch = value;
            }
        }

        // Обов'язкова валідація параметрів перед запуском кодування
        private void ValidateParameters()
        {
            if (_minMatch < 3 || _maxMatch > 258 || _minMatch > _maxMatch)
                throw new ArgumentOutOfRangeException(nameof(MinMatch), "MinMatch має бути в межах від 3 до MaxMatch (<= 258)");
            if (_windowSize < 1 || _windowSize > 32768)
                throw new ArgumentOutOfRangeException(nameof(WindowSize), "WindowSize має бути від 1 до 32768");
        }

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
                            throw new InvalidOperationException("Помилка декодування - відстань посилання виходить за межі буфера.");
                        
                        buffer.Add(buffer[copyIndex]);
                    }
                }
            }
            return buffer.ToArray();
        }

        public IEnumerable<ILz77Token> Encode(byte[] input)
        {
            ValidateParameters(); // Перевірка параметрів на вході

            var tokens = new List<ILz77Token>();
            var dictionary = new Dictionary<int, List<int>>();
            int i = 0;

            while (i < input.Length)
            {
                int matchDistance = 0;
                int matchLength = 0;

                if (i + MinMatch <= input.Length)
                {
                    int currentHash = GetHash(input, i);
                    if (dictionary.ContainsKey(currentHash))
                    {
                        var candidates = dictionary[currentHash];
                        candidates.RemoveAll(c => i - c > WindowSize);

                        foreach (var candidateIndex in candidates)
                        {
                            int currentLength = 0;
                            while (currentLength < MaxMatch &&
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

                if (matchLength >= MinMatch)
                {
                    tokens.Add(new MatchToken(matchDistance, matchLength));
                    for (int j = 0; j < matchLength; j++)
                    {
                        if (i + j + MinMatch <= input.Length)
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
                    if (i + MinMatch <= input.Length)
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

        public IEnumerable<ILz77Token> EncodeLazy(byte[] input)
        {
            ValidateParameters(); // Перевірка параметрів на вході

            var tokens = new List<ILz77Token>();
            var dictionary = new Dictionary<int, List<int>>();
            int i = 0;

            while (i < input.Length)
            {
                var match1 = FindLongestMatch(input, i, dictionary, WindowSize, MinMatch, MaxMatch);

                if (match1.Length >= MinMatch && i + 1 < input.Length)
                {
                    var match2 = FindLongestMatch(input, i + 1, dictionary, WindowSize, MinMatch, MaxMatch);

                    if (match2.Length > match1.Length)
                    {
                        tokens.Add(new LiteralToken(input[i]));
                        AddHash(input, i, dictionary, MinMatch);
                        i++;
                        continue; 
                    }
                }

                if (match1.Length >= MinMatch)
                {
                    tokens.Add(new MatchToken(match1.Distance, match1.Length));
                    for (int j = 0; j < match1.Length; j++)
                    {
                        AddHash(input, i + j, dictionary, MinMatch);
                    }
                    i += match1.Length;
                }
                else
                {
                    tokens.Add(new LiteralToken(input[i]));
                    AddHash(input, i, dictionary, MinMatch);
                    i++;
                }
            }
            return tokens;
        }

        private (int Distance, int Length) FindLongestMatch(byte[] input, int index, Dictionary<int, List<int>> dict, int windowSize, int minMatch, int maxMatch)
        {
            int bestLength = 0;
            int bestDist = 0;

            if (index + minMatch > input.Length) return (0, 0);

            int h = GetHash(input, index);
            if (dict.ContainsKey(h))
            {
                var candidates = dict[h];
                candidates.RemoveAll(c => index - c > windowSize);

                foreach (var candidateIndex in candidates)
                {
                    int currentLength = 0;
                    while (currentLength < maxMatch &&
                        index + currentLength < input.Length &&
                        input[candidateIndex + currentLength] == input[index + currentLength])
                    {
                        currentLength++;
                    }

                    if (currentLength > bestLength)
                    {
                        bestLength = currentLength;
                        bestDist = index - candidateIndex;
                    }
                }
            }
            return (bestDist, bestLength);
        }

        private void AddHash(byte[] input, int index, Dictionary<int, List<int>> dict, int minMatch)
        {
            if (index + minMatch <= input.Length)
            {
                int h = GetHash(input, index);
                if (!dict.ContainsKey(h)) dict[h] = new List<int>();
                dict[h].Add(index);
            }
        }

        private int GetHash(byte[] input, int index)
        {
            return (input[index] << 16) | (input[index + 1] << 8) | input[index + 2];
        }
    }
}