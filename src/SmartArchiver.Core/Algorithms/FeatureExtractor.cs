using System;
using System.Collections.Generic;
using SmartSecureFileArchiver.Core.Models;

namespace SmartArchiver.Core.Algorithms
{
    public class FeatureExtractor
    {
        public FileFeatures Extract(byte[] data)
        {
            var features = new FileFeatures();

            if (data == null || data.Length == 0)
                return features;

            int len = data.Length;

            // 1. Ентропія Шеннона по байтах
            int[] counts = new int[256];
            for (int i = 0; i < len; i++) counts[data[i]]++;

            double entropy = 0;
            for (int i = 0; i < 256; i++)
            {
                if (counts[i] > 0)
                {
                    double p = (double)counts[i] / len;
                    entropy -= p * Math.Log(p, 2);
                }
            }
            features.Entropy = entropy;

            // 2. Гістограма 32 кошики (buckets)
            for (int i = 0; i < 256; i++)
            {
                int bucket = i / 8;
                features.Histogram32[bucket] += (double)counts[i] / len;
            }

            // 3. Частка друкованих ASCII-символів (32-126 + \t, \n, \r)
            int printable = 0;
            int newlineCount = 0;
            for (int i = 0; i < len; i++)
            {
                byte b = data[i];
                if ((b >= 32 && b <= 126) || b == 9 || b == 10 || b == 13)
                    printable++;

                if (b == 10) // '\n'
                    newlineCount++;
            }
            features.PrintableAsciiRatio = (double)printable / len;

            // 4. Середня відстань між символами кінця рядка (\n)
            features.AvgNewlineDistance = newlineCount > 0 ? (double)len / newlineCount : len;

            // 5. Автокореляція на лагах 1..16
            double mean = 0;
            for (int i = 0; i < len; i++) mean += data[i];
            mean /= len;

            for (int lag = 1; lag <= 16; lag++)
            {
                if (len <= lag) break;
                double num = 0;
                double den = 0;
                for (int i = 0; i < len - lag; i++)
                {
                    double diff1 = data[i] - mean;
                    double diff2 = data[i + lag] - mean;
                    num += diff1 * diff2;
                }
                for (int i = 0; i < len; i++)
                {
                    double diff = data[i] - mean;
                    den += diff * diff;
                }
                features.Autocorrelation16[lag - 1] = den != 0 ? num / den : 0;
            }

            // 6. Ентропія біграм
            if (len > 1)
            {
                var bigramCounts = new Dictionary<int, int>();
                for (int i = 0; i < len - 1; i++)
                {
                    int key = (data[i] << 8) | data[i + 1];
                    if (!bigramCounts.ContainsKey(key)) bigramCounts[key] = 0;
                    bigramCounts[key]++;
                }

                double bigramEntropy = 0;
                int totalBigrams = len - 1;
                foreach (var kvp in bigramCounts)
                {
                    double p = (double)kvp.Value / totalBigrams;
                    bigramEntropy -= p * Math.Log(p, 2);
                }
                features.BigramEntropy = bigramEntropy;
            }

            return features;
        }
    }
}