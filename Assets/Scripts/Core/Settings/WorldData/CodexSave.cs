using MemoryPack;

namespace TheRavine.Base
{
    [MemoryPackable]
    public partial class CodexSave
    {
        public int version;
        public int axisCount;
        public int nextLawId;
        public int[] lawIds;
        public int[] lawAuthors;
        public float[] lawSupports;
        public float[] lawCreated;
        public float[] lawDeltas;
        public float[] opposition;
        public float[] priceWeights;
        public float[] priceCovariance;
        public float[] priceMeanTheta;
        public float priceMeanFlux;
        public int priceSamples;

        public CodexSave Clone() => new()
        {
            version = version,
            axisCount = axisCount,
            nextLawId = nextLawId,
            lawIds = lawIds != null ? (int[])lawIds.Clone() : null,
            lawAuthors = lawAuthors != null ? (int[])lawAuthors.Clone() : null,
            lawSupports = lawSupports != null ? (float[])lawSupports.Clone() : null,
            lawCreated = lawCreated != null ? (float[])lawCreated.Clone() : null,
            lawDeltas = lawDeltas != null ? (float[])lawDeltas.Clone() : null,
            opposition = opposition != null ? (float[])opposition.Clone() : null,
            priceWeights = priceWeights != null ? (float[])priceWeights.Clone() : null,
            priceCovariance = priceCovariance != null ? (float[])priceCovariance.Clone() : null,
            priceMeanTheta = priceMeanTheta != null ? (float[])priceMeanTheta.Clone() : null,
            priceMeanFlux = priceMeanFlux,
            priceSamples = priceSamples,
        };
    }
}
