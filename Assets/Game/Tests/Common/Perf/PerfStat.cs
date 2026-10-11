namespace Game.Tests.Common.Perf
{
    /// <summary>Statistics of one measured source (SPEC-71 §4.2, §5.3 step 5).</summary>
    public readonly struct PerfStat
    {
        public readonly string Name; public readonly double Median, P95, Max, Mean; public readonly int Samples; public readonly string Unit;
        public PerfStat(string n, double med, double p95, double max, double mean, int s, string u) { Name = n; Median = med; P95 = p95; Max = max; Mean = mean; Samples = s; Unit = u; }

        /// <summary>Median = mean of the two middle values when the count is even; p95 = nearest rank ceil(0.95 n).</summary>
        public static PerfStat From(string name, string unit, double[] samples)
        {
            int n = samples.Length;
            if (n == 0) return new PerfStat(name, double.NaN, double.NaN, double.NaN, double.NaN, 0, unit);
            var s = (double[])samples.Clone();
            System.Array.Sort(s);
            double median = n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) * 0.5;
            int rank = (int)System.Math.Ceiling(0.95 * n);
            double p95 = s[System.Math.Max(1, rank) - 1];
            double sum = 0; foreach (var v in s) sum += v;
            return new PerfStat(name, median, p95, s[n - 1], sum / n, n, unit);
        }
    }
}
