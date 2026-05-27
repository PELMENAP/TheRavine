using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace TheRavine.Collections
{
    public unsafe struct Dense3DBitGrid : IDisposable
    {
        private const int BitsPerWord = 64;
        private NativeArray<ulong> _words;
        public readonly int SizeX;
        public readonly int SizeY;
        public readonly int SizeZ;
        public readonly int PlaneSize;
        public readonly int Capacity;
        public readonly int WordCount;
        private readonly ulong _lastWordMask;

        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _words.IsCreated;
        }

        public NativeArray<ulong> Words
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _words;
        }

        public Dense3DBitGrid(
            int sizeX,
            int sizeY,
            int sizeZ,
            Allocator allocator,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            if ((uint)sizeX > 64 || sizeX <= 0)
                throw new ArgumentOutOfRangeException(nameof(sizeX));

            if ((uint)sizeY > 64 || sizeY <= 0)
                throw new ArgumentOutOfRangeException(nameof(sizeY));

            if ((uint)sizeZ > 64 || sizeZ <= 0)
                throw new ArgumentOutOfRangeException(nameof(sizeZ));

            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;

            PlaneSize = sizeX * sizeY;
            Capacity = PlaneSize * sizeZ;

            WordCount = (Capacity + BitsPerWord - 1) >> 6;

            int overflowBits = (WordCount << 6) - Capacity;

            _lastWordMask = overflowBits > 0
                ? ulong.MaxValue >> overflowBits
                : ulong.MaxValue;

            _words = new NativeArray<ulong>(WordCount, allocator, options);
        }

        public bool this[int x, int y, int z]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => ContainsUnchecked(x, y, z);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => SetUnchecked(x, y, z, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int ToIndex(int x, int y, int z)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if ((uint)x >= SizeX)
                throw new ArgumentOutOfRangeException(nameof(x));

            if ((uint)y >= SizeY)
                throw new ArgumentOutOfRangeException(nameof(y));

            if ((uint)z >= SizeZ)
                throw new ArgumentOutOfRangeException(nameof(z));
#endif

            return x + SizeX * y + PlaneSize * z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void ToCoord(int index, out int x, out int y, out int z)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if ((uint)index >= Capacity)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif

            z = index / PlaneSize;
            index -= z * PlaneSize;

            y = index / SizeX;
            x = index - y * SizeX;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Contains(int x, int y, int z)
        {
            return ContainsByIndex(ToIndex(x, y, z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool ContainsUnchecked(int x, int y, int z)
        {
            int index = x + SizeX * y + PlaneSize * z;
            return ContainsByIndexUnchecked(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool ContainsByIndex(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if ((uint)index >= Capacity)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif

            return ContainsByIndexUnchecked(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool ContainsByIndexUnchecked(int index)
        {
            int wordIndex = index >> 6;
            ulong mask = 1UL << (index & 63);

            return (_words[wordIndex] & mask) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(int x, int y, int z)
        {
            return AddByIndex(ToIndex(x, y, z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddUnchecked(int x, int y, int z)
        {
            int index = x + SizeX * y + PlaneSize * z;
            return AddByIndexUnchecked(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddByIndex(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if ((uint)index >= Capacity)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif

            return AddByIndexUnchecked(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddByIndexUnchecked(int index)
        {
            int wordIndex = index >> 6;
            ulong mask = 1UL << (index & 63);

            ulong word = _words[wordIndex];

            if ((word & mask) != 0)
                return false;

            _words[wordIndex] = word | mask;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(int x, int y, int z)
        {
            return RemoveByIndex(ToIndex(x, y, z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveUnchecked(int x, int y, int z)
        {
            int index = x + SizeX * y + PlaneSize * z;
            return RemoveByIndexUnchecked(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveByIndex(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if ((uint)index >= Capacity)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif

            return RemoveByIndexUnchecked(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveByIndexUnchecked(int index)
        {
            int wordIndex = index >> 6;
            ulong mask = 1UL << (index & 63);

            ulong word = _words[wordIndex];

            if ((word & mask) == 0)
                return false;

            _words[wordIndex] = word & ~mask;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int x, int y, int z, bool value)
        {
            SetByIndex(ToIndex(x, y, z), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUnchecked(int x, int y, int z, bool value)
        {
            int index = x + SizeX * y + PlaneSize * z;
            SetByIndexUnchecked(index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetByIndex(int index, bool value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if ((uint)index >= Capacity)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif

            SetByIndexUnchecked(index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetByIndexUnchecked(int index, bool value)
        {
            int wordIndex = index >> 6;
            ulong mask = 1UL << (index & 63);

            if (value)
                _words[wordIndex] |= mask;
            else
                _words[wordIndex] &= ~mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flip(int x, int y, int z)
        {
            FlipByIndex(ToIndex(x, y, z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FlipByIndex(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if ((uint)index >= Capacity)
                throw new ArgumentOutOfRangeException(nameof(index));
#endif

            int wordIndex = index >> 6;
            ulong mask = 1UL << (index & 63);

            _words[wordIndex] ^= mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            UnsafeUtility.MemClear(
                _words.GetUnsafePtr(),
                WordCount * sizeof(ulong));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Fill()
        {
            ulong* ptr = (ulong*)_words.GetUnsafePtr();

            for (int i = 0; i < WordCount; i++)
                ptr[i] = ulong.MaxValue;

            ptr[WordCount - 1] &= _lastWordMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAll(bool value)
        {
            if (value)
                Fill();
            else
                Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Any()
        {
            ulong* ptr = (ulong*)_words.GetUnsafeReadOnlyPtr();

            for (int i = 0; i < WordCount; i++)
            {
                if (ptr[i] != 0)
                    return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool None()
        {
            ulong* ptr = (ulong*)_words.GetUnsafeReadOnlyPtr();

            for (int i = 0; i < WordCount; i++)
            {
                if (ptr[i] != 0)
                    return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int CountBits()
        {
            ulong* ptr = (ulong*)_words.GetUnsafeReadOnlyPtr();

            int count = 0;
            int last = WordCount - 1;

            for (int i = 0; i < last; i++)
                count += math.countbits(ptr[i]);

            ulong lastWord = ptr[last] & _lastWordMask;

            return count + math.countbits(lastWord);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Union(in Dense3DBitGrid other)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            ValidateCompatibility(other);
#endif

            ulong* dst = (ulong*)_words.GetUnsafePtr();
            ulong* src = (ulong*)other._words.GetUnsafeReadOnlyPtr();

            for (int i = 0; i < WordCount; i++)
                dst[i] |= src[i];

            dst[WordCount - 1] &= _lastWordMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Intersect(in Dense3DBitGrid other)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            ValidateCompatibility(other);
#endif

            ulong* dst = (ulong*)_words.GetUnsafePtr();
            ulong* src = (ulong*)other._words.GetUnsafeReadOnlyPtr();

            for (int i = 0; i < WordCount; i++)
                dst[i] &= src[i];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Xor(in Dense3DBitGrid other)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            ValidateCompatibility(other);
#endif

            ulong* dst = (ulong*)_words.GetUnsafePtr();
            ulong* src = (ulong*)other._words.GetUnsafeReadOnlyPtr();

            for (int i = 0; i < WordCount; i++)
                dst[i] ^= src[i];

            dst[WordCount - 1] &= _lastWordMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Except(in Dense3DBitGrid other)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            ValidateCompatibility(other);
#endif

            ulong* dst = (ulong*)_words.GetUnsafePtr();
            ulong* src = (ulong*)other._words.GetUnsafeReadOnlyPtr();

            for (int i = 0; i < WordCount; i++)
                dst[i] &= ~src[i];
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly void ValidateCompatibility(in Dense3DBitGrid other)
        {
            if (Capacity != other.Capacity)
                throw new InvalidOperationException("BitGrid sizes mismatch");
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastEnumerator GetFastEnumerator()
        {
            return new FastEnumerator(this);
        }

        public void Dispose()
        {
            if (_words.IsCreated)
                _words.Dispose();
        }

        public ref struct FastEnumerator
        {
            private readonly ulong* _words;
            private readonly int _wordCount;
            private readonly int _capacity;

            private int _wordIndex;
            private ulong _currentWord;

            public int Current;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public FastEnumerator(Dense3DBitGrid grid)
            {
                _words = (ulong*)grid._words.GetUnsafeReadOnlyPtr();
                _wordCount = grid.WordCount;
                _capacity = grid.Capacity;

                _wordIndex = -1;
                _currentWord = 0;
                Current = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                while (true)
                {
                    if (_currentWord != 0)
                    {
                        int bit = math.tzcnt(_currentWord);

                        Current = (_wordIndex << 6) + bit;

                        _currentWord &= _currentWord - 1;

                        return Current < _capacity;
                    }

                    _wordIndex++;

                    if (_wordIndex >= _wordCount)
                        return false;

                    _currentWord = _words[_wordIndex];
                }
            }
        }
    }
}