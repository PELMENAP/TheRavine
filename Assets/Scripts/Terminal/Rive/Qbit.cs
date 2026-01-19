public struct Qbit
{
    public float Alpha;
    public float Beta;
    
    public Qbit(int initialState)
    {
        Alpha = initialState == 0 ? 1f : 0f;
        Beta = initialState == 0 ? 0f : 1f;
    }
    
    public void Hadamard()
    {
        float a = Alpha, b = Beta;
        const float invSqrt2 = 0.7071067f;
        Alpha = (a + b) * invSqrt2;
        Beta = (a - b) * invSqrt2;
    }
    
    public int Measure()
    {
        float p0 = Alpha * Alpha;
        
        if (UnityEngine.Random.value < p0)
        {
            Alpha = 1f;
            Beta = 0f;
            return 0;
        }
        else
        {
            Alpha = 0f;
            Beta = 1f;
            return 1;
        }
    }
    
    public void Phase(int angleDegrees)
    {
        float angleRad = angleDegrees * UnityEngine.Mathf.Deg2Rad;
        float cos = UnityEngine.Mathf.Cos(angleRad);
        float sin = UnityEngine.Mathf.Sin(angleRad);
        
        float realBeta = Beta * cos;
        float imagBeta = Beta * sin;
        
        Beta = UnityEngine.Mathf.Sqrt(realBeta * realBeta + imagBeta * imagBeta);
        if (realBeta < 0) Beta = -Beta;
    }
}

public struct Complex
{
    public float Re;
    public float Im;

    public Complex(float re, float im = 0f)
    {
        Re = re;
        Im = im;
    }

    public readonly float MagnitudeSq => Re * Re + Im * Im;

    public static Complex operator +(Complex a, Complex b)
        => new(a.Re + b.Re, a.Im + b.Im);

    public static Complex operator -(Complex a, Complex b)
        => new(a.Re - b.Re, a.Im - b.Im);

    public static Complex operator *(Complex a, Complex b)
        => new(
            a.Re * b.Re - a.Im * b.Im,
            a.Re * b.Im + a.Im * b.Re
        );

    public static Complex FromPhase(float radians)
        => new(UnityEngine.Mathf.Cos(radians), UnityEngine.Mathf.Sin(radians));
}


public class QRegister
{
    public readonly int QubitCount;
    public readonly int StateCount;

    private Complex[] amplitudes;

    public QRegister(int qubitCount, int initialState = 0)
    {
        QubitCount = qubitCount;
        StateCount = 1 << qubitCount;

        amplitudes = new Complex[StateCount];

        for (int i = 0; i < StateCount; i++)
            amplitudes[i] = new Complex(0);

        amplitudes[initialState] = new Complex(1);
    }

    public void Hadamard(int qubit)
    {
        int bit = 1 << qubit;
        const float invSqrt2 = 0.7071067f;

        for (int i = 0; i < StateCount; i++)
        {
            if ((i & bit) == 0)
            {
                int j = i | bit;

                Complex a = amplitudes[i];
                Complex b = amplitudes[j];

                amplitudes[i] = new Complex(
                    (a.Re + b.Re) * invSqrt2,
                    (a.Im + b.Im) * invSqrt2
                );

                amplitudes[j] = new Complex(
                    (a.Re - b.Re) * invSqrt2,
                    (a.Im - b.Im) * invSqrt2
                );
            }
        }
    }

    public void Phase(int qubit, float angleDegrees)
    {
        float radians = angleDegrees * UnityEngine.Mathf.Deg2Rad;
        Complex phase = Complex.FromPhase(radians);

        int bit = 1 << qubit;

        for (int i = 0; i < StateCount; i++)
        {
            if ((i & bit) != 0)
                amplitudes[i] = amplitudes[i] * phase;
        }
    }

    public int Measure()
    {
        float total = 0f;
        for (int i = 0; i < StateCount; i++)
            total += amplitudes[i].MagnitudeSq;

        float r = UnityEngine.Random.value * total;
        float acc = 0f;

        for (int i = 0; i < StateCount; i++)
        {
            acc += amplitudes[i].MagnitudeSq;
            if (r <= acc)
            {
                CollapseTo(i);
                return i;
            }
        }

        return 0;
    }

    private void CollapseTo(int state)
    {
        for (int i = 0; i < StateCount; i++)
            amplitudes[i] = new Complex(0);

        amplitudes[state] = new Complex(1);
    }

    public void ControlledPhase(int controlQubit, int targetQubit, int angleDegrees)
    {
        int controlMask = 1 << controlQubit;
        int targetMask = 1 << targetQubit;
        float radians = angleDegrees * UnityEngine.Mathf.Deg2Rad;
        Complex phase = Complex.FromPhase(radians);

        for (int i = 0; i < StateCount; i++)
        {
            if ((i & controlMask) != 0 && (i & targetMask) != 0)
            {
                amplitudes[i] = amplitudes[i] * phase;
            }
        }
    }

    public void QuantumFourierTransform()
    {
        int n = QubitCount;
        for (int targetQubit = 0; targetQubit < n; targetQubit++)
        {
            Hadamard(targetQubit);
            
            for (int controlQubit = targetQubit + 1; controlQubit < n; controlQubit++)
            {
                float angle = 360.0f / (1 << (controlQubit - targetQubit + 1));
                ControlledPhase(controlQubit, targetQubit, (int)angle);
            }
        }
    }

    public void ApplyModularExponentiationOracle(int a, int N, int xNumQubits)
    {
        int yNumQubits = QubitCount - xNumQubits;
        int xMask = (1 << xNumQubits) - 1;
        int yShift = xNumQubits;
        Complex[] newAmplitudes = new Complex[StateCount];
        for (int k = 0; k < StateCount; k++) newAmplitudes[k] = new Complex(0);

        for (int i = 0; i < StateCount; i++)
        {
            if (amplitudes[i].MagnitudeSq == 0) continue;

            int x = i & xMask;
            int y = i >> yShift;
            int newY = (int)((long)y * ModPow(a, x, N) % N);
            int newState = (newY << yShift) | x;
            newAmplitudes[newState] += amplitudes[i];
        }
        amplitudes = newAmplitudes;
    }
    

    public void PauliX(int qubit)
    {
        int bit = 1 << qubit;
        for (int i = 0; i < StateCount; i++)
        {
            if ((i & bit) == 0)
            {
                int j = i | bit;
                (amplitudes[j], amplitudes[i]) = (amplitudes[i], amplitudes[j]);
            }
        }
    }

    public void CNOT(int controlQubit, int targetQubit)
    {
        int controlMask = 1 << controlQubit;
        int targetMask = 1 << targetQubit;
        for (int i = 0; i < StateCount; i++)
        {
            if ((i & controlMask) != 0 && (i & targetMask) == 0)
            {
                int j = i | targetMask;
                (amplitudes[j], amplitudes[i]) = (amplitudes[i], amplitudes[j]);
            }
        }
    }

    private static int ModPow(int baseVal, int exp, int mod)
    {
        int result = 1;
        baseVal %= mod;
        while (exp > 0)
        {
            if ((exp & 1) != 0)
            {
                result = (int)((long)result * baseVal % mod);
            }
            baseVal = (int)((long)baseVal * baseVal % mod);
            exp >>= 1;
        }
        return result;
    }

    public void QuantumFourierTransform(int startQubit = 0, int numQubits = -1)
    {
        if (numQubits == -1) numQubits = QubitCount;
        int endQubit = startQubit + numQubits;
        for (int targetQubit = startQubit; targetQubit < endQubit; targetQubit++)
        {
            Hadamard(targetQubit);
            for (int controlQubit = targetQubit + 1; controlQubit < endQubit; controlQubit++)
            {
                float angle = 360.0f / (1 << (controlQubit - targetQubit + 1));
                ControlledPhase(controlQubit, targetQubit, (int)angle);
            }
        }
    }

    public void InverseQuantumFourierTransform(int startQubit = 0, int numQubits = -1)
    {
        if (numQubits == -1) numQubits = QubitCount;
        int endQubit = startQubit + numQubits;
        for (int targetQubit = startQubit; targetQubit < endQubit; targetQubit++)
        {
            for (int controlQubit = targetQubit + 1; controlQubit < endQubit; controlQubit++)
            {
                float angle = -360.0f / (1 << (controlQubit - targetQubit + 1));
                ControlledPhase(controlQubit, targetQubit, (int)angle);
            }
            Hadamard(targetQubit);
        }
    }
}