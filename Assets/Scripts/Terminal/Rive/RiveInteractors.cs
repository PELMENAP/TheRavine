using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public interface IInteractor
    {
        string Name { get; }
        string Description { get; }
        UniTask<int> SendAsync(int value);
        void Reset();
    }

    public class DigitalLockInteractor : IInteractor
    {
        public string Name => "lock_alpha";
        public string Description => "Цифровой замок. Угадайте 3-значный код (000-999)";

        private readonly int _secretCode;
        private int _attemptCount;

        public DigitalLockInteractor(int secretCode)
        {
            _secretCode = secretCode;
            _attemptCount = 0;
        }

        public async UniTask<int> SendAsync(int value)
        {
            await UniTask.Yield();
            _attemptCount++;

            if (value == _secretCode)
                return 1000 + _attemptCount;

            int matches = CountMatches(value, _secretCode);
            return matches;
        }

        private int CountMatches(int input, int secret)
        {
            var inputStr = input.ToString("D3");
            var secretStr = secret.ToString("D3");

            int matches = 0;
            for (int i = 0; i < 3; i++)
            {
                if (inputStr[i] == secretStr[i])
                    matches++;
            }

            return matches;
        }

        public void Reset()
        {
            _attemptCount = 0;
        }
    }

    public class SequenceValidatorInteractor : IInteractor
    {
        public string Name => "seq_validator";
        public string Description => "Валидатор последовательности. Найдите правило и введите следующее число";

        private readonly List<int> _sequence = new() { 2, 4, 8, 16, 32 };
        private int _currentIndex;

        public async UniTask<int> SendAsync(int value)
        {
            await UniTask.Yield();

            if (_currentIndex >= _sequence.Count)
            {
                int expected = _sequence[_sequence.Count - 1] * 2;
                if (value == expected)
                {
                    _sequence.Add(value);
                    return 1000;
                }
                return -1;
            }

            if (value == _sequence[_currentIndex])
            {
                _currentIndex++;
                if (_currentIndex >= _sequence.Count)
                    return 100;
                return _currentIndex;
            }

            return -1;
        }

        public void Reset()
        {
            _currentIndex = 0;
        }
    }

    public class ChecksumInteractor : IInteractor
    {
        public string Name => "checksum";
        public string Description => "Генератор контрольной суммы. Отправьте число, получите его хеш";

        private int _lastValue;

        public async UniTask<int> SendAsync(int value)
        {
            await UniTask.Yield();
            _lastValue = value;

            int hash = 0;
            int temp = Math.Abs(value);
            
            while (temp > 0)
            {
                hash = (hash * 31 + temp % 10) % 10000;
                temp /= 10;
            }

            return hash;
        }

        public void Reset()
        {
            _lastValue = 0;
        }
    }

    public class CollatzInteractor : IInteractor
    {
        public string Name => "collatz";
        public string Description => "Проблема Коллатца. Отправьте число, получите следующее в последовательности";

        public async UniTask<int> SendAsync(int value)
        {
            await UniTask.Yield();

            if (value <= 0)
                return -1;

            if (value == 1)
                return 1;

            if (value % 2 == 0)
                return value / 2;
            else
                return 3 * value + 1;
        }

        public void Reset() { }
    }

    public class InteractorRegistry
    {
        private readonly Dictionary<string, IInteractor> _interactors = new();
        private readonly IRavineLogger _logger;

        public InteractorRegistry(IRavineLogger logger)
        {
            _logger = logger;
        }

        public void Register(IInteractor interactor)
        {
            if (_interactors.ContainsKey(interactor.Name))
            {
                _logger.LogWarning($"Interactor '{interactor.Name}' already registered");
                return;
            }

            _interactors[interactor.Name] = interactor;
            _logger.LogInfo($"Registered interactor: {interactor.Name}");
        }

        public void Unregister(string name)
        {
            _interactors.Remove(name);
        }

        public async UniTask<int> SendToInteractorAsync(string name, int value)
        {
            if (!_interactors.TryGetValue(name, out var interactor))
            {
                throw new RiveRuntimeException($"Interactor '{name}' not found");
            }

            return await interactor.SendAsync(value);
        }

        public bool Exists(string name) => _interactors.ContainsKey(name);

        public IInteractor Get(string name) => _interactors.TryGetValue(name, out var i) ? i : null;

        public IReadOnlyCollection<string> GetRegisteredNames() => _interactors.Keys;

        public void ResetAll()
        {
            foreach (var interactor in _interactors.Values)
            {
                interactor.Reset();
            }
        }
    }
}