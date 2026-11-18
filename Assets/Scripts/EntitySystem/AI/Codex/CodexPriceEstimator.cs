using System;
using Unity.Mathematics;

public sealed class CodexPriceEstimator
{
    private readonly int _axes;
    private readonly float _forgetting;
    private readonly int _minSamples;
    private readonly float _minExcitation;
    private readonly float _initCov;
    private readonly float[] _prior;
    private readonly float[] _w;
    private readonly float[] _p;
    private readonly float[] _meanTheta;
    private readonly float[] _x;
    private readonly float[] _px;
    private float _meanFlux;
    private int _samples;
    private int _rejected;

    public int Axes => _axes;
    public int Samples => _samples;
    public int Rejected => _rejected;
    public bool UsesPrior => _samples < _minSamples;
    public ReadOnlySpan<float> Prices => UsesPrior ? _prior : _w;
    public ReadOnlySpan<float> Prior => _prior;

    public CodexPriceEstimator(int axes, float forgetting, int minSamples, float minExcitation, float initCov, ReadOnlySpan<float> prior)
    {
        _axes = math.max(axes, 1);
        _forgetting = math.clamp(forgetting, 0.5f, 1f);
        _minSamples = math.max(minSamples, 1);
        _minExcitation = math.max(minExcitation, 0f);
        _initCov = math.max(initCov, 1e-6f);
        _prior = new float[_axes];
        _w = new float[_axes];
        _p = new float[_axes * _axes];
        _meanTheta = new float[_axes];
        _x = new float[_axes];
        _px = new float[_axes];
        for (int k = 0; k < _axes && k < prior.Length; k++) _prior[k] = math.isfinite(prior[k]) ? prior[k] : 0f;
        Reset();
    }

    public void Reset()
    {
        Array.Copy(_prior, _w, _axes);
        ResetCovariance();
        Array.Clear(_meanTheta, 0, _axes);
        _meanFlux = 0f;
        _samples = 0;
        _rejected = 0;
    }

    private void ResetCovariance()
    {
        Array.Clear(_p, 0, _p.Length);
        for (int k = 0; k < _axes; k++) _p[k * _axes + k] = _initCov;
    }

    public bool Add(ReadOnlySpan<float> theta, float flux)
    {
        if (theta.Length != _axes || !math.isfinite(flux))
        {
            _rejected++;
            return false;
        }

        for (int k = 0; k < _axes; k++)
        {
            if (math.isfinite(theta[k])) continue;
            _rejected++;
            return false;
        }

        float alpha = math.max(1f / (_samples + 1), 1f - _forgetting);
        float excitation = 0f;
        for (int k = 0; k < _axes; k++)
        {
            _x[k] = theta[k] - _meanTheta[k];
            excitation += _x[k] * _x[k];
        }
        float y = flux - _meanFlux;

        if (_samples == 0)
        {
            for (int k = 0; k < _axes; k++) _meanTheta[k] = theta[k];
            _meanFlux = flux;
            _samples = 1;
            return true;
        }

        for (int k = 0; k < _axes; k++) _meanTheta[k] += alpha * _x[k];
        _meanFlux += alpha * y;
        _samples++;

        if (excitation > _minExcitation) Update(y);
        return true;
    }

    private void Update(float y)
    {
        float quad = 0f;
        for (int i = 0; i < _axes; i++)
        {
            float s = 0f;
            int row = i * _axes;
            for (int j = 0; j < _axes; j++) s += _p[row + j] * _x[j];
            _px[i] = s;
            quad += s * _x[i];
        }

        float denom = _forgetting + quad;
        float predicted = 0f;
        for (int k = 0; k < _axes; k++) predicted += _w[k] * _x[k];
        float error = y - predicted;

        float inv = 1f / denom;
        float invLambda = 1f / _forgetting;
        for (int i = 0; i < _axes; i++)
        {
            float gain = _px[i] * inv;
            _w[i] += gain * error;
            int row = i * _axes;
            for (int j = 0; j < _axes; j++)
                _p[row + j] = (_p[row + j] - gain * _px[j]) * invLambda;
        }

        float trace = 0f;
        bool finite = true;
        for (int k = 0; k < _axes; k++)
        {
            trace += _p[k * _axes + k];
            finite &= math.isfinite(_w[k]) && math.isfinite(_p[k * _axes + k]);
        }

        if (!finite || !(trace > 0f))
        {
            ResetCovariance();
            for (int k = 0; k < _axes; k++) if (!math.isfinite(_w[k])) _w[k] = _prior[k];
            return;
        }

        float cap = _initCov * _axes;
        if (trace > cap)
        {
            float scale = cap / trace;
            for (int i = 0; i < _p.Length; i++) _p[i] *= scale;
        }
    }

    public void CopyState(Span<float> w, Span<float> p, Span<float> meanTheta, out float meanFlux, out int samples)
    {
        _w.AsSpan().CopyTo(w);
        _p.AsSpan().CopyTo(p);
        _meanTheta.AsSpan().CopyTo(meanTheta);
        meanFlux = _meanFlux;
        samples = _samples;
    }

    public bool TryRestore(ReadOnlySpan<float> w, ReadOnlySpan<float> p, ReadOnlySpan<float> meanTheta, float meanFlux, int samples)
    {
        if (w.Length != _axes || p.Length != _p.Length || meanTheta.Length != _axes) return false;
        if (samples < 0 || !math.isfinite(meanFlux)) return false;
        for (int i = 0; i < w.Length; i++) if (!math.isfinite(w[i])) return false;
        for (int i = 0; i < p.Length; i++) if (!math.isfinite(p[i])) return false;
        for (int i = 0; i < meanTheta.Length; i++) if (!math.isfinite(meanTheta[i])) return false;

        w.CopyTo(_w);
        p.CopyTo(_p);
        meanTheta.CopyTo(_meanTheta);
        _meanFlux = meanFlux;
        _samples = samples;
        return true;
    }
}
