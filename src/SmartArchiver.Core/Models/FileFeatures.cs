namespace SmartArchiver.Core.Models
{
    public class FileFeatures
    {
        public double Entropy { get; set; }
        public double[] Histogram32 { get; set; } = new double[32];
        public double PrintableAsciiRatio { get; set; }
        public double AvgNewlineDistance { get; set; }
        public double[] Autocorrelation16 { get; set; } = new double[16];
        public double BigramEntropy { get; set; }

        public double[] ToVector()
        {
            var vector = new List<double>
            {
                Entropy,
                PrintableAsciiRatio,
                AvgNewlineDistance,
                BigramEntropy
            };
            vector.AddRange(Histogram32);
            vector.AddRange(Autocorrelation16);
            return vector.ToArray();
        }
    }
}
