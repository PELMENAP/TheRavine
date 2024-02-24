using UnityEngine;
using Accord.Statistics.Distributions.Multivariate;

public class DistributionCache
{
    private static MultivariateNormalDistribution cachedDistribution;
    private static object lockObject = new object();
    public static MultivariateNormalDistribution GetCachedDistribution()
    {
        lock (lockObject)
        {
            if (cachedDistribution == null)
            {
                double[,] covariance = {
                    { 10, 0 },
                    { 0, 10 }
                };
                cachedDistribution = new MultivariateNormalDistribution(new double[] { 0, 0 }, covariance);
            }
            return cachedDistribution;
        }
    }

    // Метод для сброса кеша, если нужно изменить параметры распределения
    public static void ResetCache()
    {
        lock (lockObject)
        {
            cachedDistribution = null;
        }
    }
}
