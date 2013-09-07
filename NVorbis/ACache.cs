/****************************************************************************
 * NVorbis                                                                  *
 * Copyright (C) 2012, Andrew Ward <afward@gmail.com>                       *
 *                                                                          *
 * See COPYING for license terms (Ms-PL).                                   *
 *                                                                          *
 ***************************************************************************/
using System;
using System.Collections.Generic;

namespace NVorbis
{
    static class Hashing
    {
        static unsafe void Hash(byte* d, int len, ref uint h)
        {
            for (int i = 0; i < len; i++)
            {
                h += d[i];
                h += (h << 10);
                h ^= (h >> 6);
            }
        }
        unsafe static void Hash(ref uint h, int data)
        {
            byte* d = (byte*)&data;
            Hash(d, sizeof(int), ref h);
        }

        unsafe static int Avalanche(uint h)
        {
            h += (h << 3);
            h ^= (h >> 11);
            h += (h << 15);
            return *((int*)(void*)&h);
        }

        public static int CombineHashCodes(int first, int second)
        {
            uint h = 0;
            Hash(ref h, first);
            Hash(ref h, second);
            return Avalanche(h);
        }
    }

    static class ACache
    {
        static readonly Dictionary<BufferDescriptor, Stack<IBufferStorage>> buffers = new Dictionary<BufferDescriptor, Stack<IBufferStorage>>();

        struct BufferDescriptor : IEquatable<BufferDescriptor> 
        {
            readonly Type Type;
            readonly int Elements;

            public BufferDescriptor(Type type, int elements)
            {
                Type = type;
                Elements = elements;
            }

            public override int GetHashCode()
            {
                return Hashing.CombineHashCodes(Type.GetHashCode(), Elements.GetHashCode());
            }
            public bool Equals(BufferDescriptor other)
            {
                return other.Type == Type && other.Elements == Elements;
            }
            public override bool Equals(object other)
            {
                return other is BufferDescriptor && ((BufferDescriptor) other).Equals(this);
            }
        }

        interface IBufferStorage { }
        struct BufferStorage<T> : IBufferStorage
        {
            public readonly T[] Buffer;

            public BufferStorage(T[] buffer)
            {
                Buffer = buffer;
            }
        }

        internal static T[] Get<T>(int elements)
        {
            return Get<T>(elements, true);
        }

        internal static T[] Get<T>(int elements, bool clearFirst)
        {
            var descriptor = new BufferDescriptor(typeof(T), elements);

            Stack<IBufferStorage> stack;
            if (!buffers.TryGetValue(descriptor, out stack))
                buffers.Add(descriptor, stack = new Stack<IBufferStorage>());

            T[] buffer;
            if (stack.Count == 0)   buffer = new T[elements];
            else                    buffer = ((BufferStorage<T>) stack.Pop()).Buffer;

            if (clearFirst)
                for (int i = 0; i < elements; i++)
                    buffer[i] = default(T);

            return buffer;
        }

        internal static T[][] Get<T>(int firstRankSize, int secondRankSize)
        {
            var temp = Get<T[]>(firstRankSize, false);
            for (int i = 0; i < firstRankSize; i++)
            {
                temp[i] = Get<T>(secondRankSize, true);
            }
            return temp;
        }

        internal static T[][][] Get<T>(int firstRankSize, int secondRankSize, int thirdRankSize)
        {
            var temp = Get<T[][]>(firstRankSize, false);
            for (int i = 0; i < firstRankSize; i++)
            {
                temp[i] = Get<T>(secondRankSize, thirdRankSize);
            }
            return temp;
        }

        internal static void Return<T>(ref T[] buffer)
        {
            var descriptor = new BufferDescriptor(typeof(T), buffer.Length);
            Stack<IBufferStorage> stack;
            if (!buffers.TryGetValue(descriptor, out stack))
                throw new InvalidOperationException("Returning a buffer that's never been taken!");
            stack.Push(new BufferStorage<T>(buffer));
        }

        internal static void Return<T>(ref T[][] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] != null) Return(ref buffer[i]);
            }
            Return<T[]>(ref buffer);
        }

        internal static void Return<T>(ref T[][][] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] != null) Return(ref buffer[i]);
            }
            Return<T[][]>(ref buffer);
        }
    }
}
