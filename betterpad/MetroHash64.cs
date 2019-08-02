
using System;

namespace MetroHash
{

    public partial class MetroHash
    {
        //---------------------------------------------------------------------------//
        private const ulong K0_64_1  = 0xC83A91E1;
        private const ulong K1_64_1  = 0x8648DBDB;
        private const ulong K2_64_1  = 0x7BDEC03B;
        private const ulong K3_64_1  = 0x2F5870A5;

        //---------------------------------------------------------------------------//
        public static void Hash64_1(byte[] lKey, uint lStartOffset, uint lLength, uint lSeed, out ulong lHash)
        {
            uint lKeyIndex = lStartOffset;
            uint lKeyEnd = lKeyIndex + lLength;

            if (lKey.Length < lKeyEnd)
            {
                throw new IndexOutOfRangeException("Given Key for hashing is not of expected length");
            }

            unsafe
            {
                fixed (byte* ptr = lKey)
                {
                    Hash64_1(ptr, lStartOffset, lLength, lSeed, out lHash);
                    //lOutput = BitConverter.GetBytes(lHash);
                }
            }
        }

        public static unsafe void Hash64_1(byte* lKey, uint lStartOffset, uint lLength, uint lSeed, out ulong lHash)
        {
            uint lKeyIndex = lStartOffset;
            uint lKeyEnd = lKeyIndex + lLength;

            lHash = ((lSeed + K2_64_1) * K0_64_1) + lLength;

            if (lLength >= 32)
            {
                ulong[] lV = { lHash, lHash, lHash, lHash };

                do
                {
                    lV[0] += (*(ulong *)(lKey + lKeyIndex)) * K0_64_1; lKeyIndex += 8; lV[0] = RotateRight(lV[0],29) + lV[2];
                    lV[1] += (*(ulong *)(lKey + lKeyIndex)) * K1_64_1; lKeyIndex += 8; lV[1] = RotateRight(lV[1],29) + lV[3];
                    lV[2] += (*(ulong *)(lKey + lKeyIndex)) * K2_64_1; lKeyIndex += 8; lV[2] = RotateRight(lV[2],29) + lV[0];
                    lV[3] += (*(ulong *)(lKey + lKeyIndex)) * K3_64_1; lKeyIndex += 8; lV[3] = RotateRight(lV[3],29) + lV[1];
                }
                while (lKeyIndex <= (lKeyEnd - 32));

                lV[2] ^= RotateRight(((lV[0] + lV[3]) * K0_64_1) + lV[1], 33) * K1_64_1;
                lV[3] ^= RotateRight(((lV[1] + lV[2]) * K1_64_1) + lV[0], 33) * K0_64_1;
                lV[0] ^= RotateRight(((lV[0] + lV[2]) * K0_64_1) + lV[3], 33) * K1_64_1;
                lV[1] ^= RotateRight(((lV[1] + lV[3]) * K1_64_1) + lV[2], 33) * K0_64_1;
                lHash += lV[0] ^ lV[1];
            }

            if ((lKeyEnd - lKeyIndex) >= 16)
            {
                ulong lV0 = lHash + ((*(UInt64 *)(lKey + lKeyIndex)) * K0_64_1); lKeyIndex += 8; lV0 = RotateRight(lV0,33) * K1_64_1;
                ulong lV1 = lHash + ((*(UInt64 *)(lKey + lKeyIndex)) * K1_64_1); lKeyIndex += 8; lV1 = RotateRight(lV1,33) * K2_64_1;
                lV0 ^= RotateRight(lV0 * K0_64_1, 35) + lV1;
                lV1 ^= RotateRight(lV1 * K3_64_1, 35) + lV0;
                lHash += lV1;
            }

            if ((lKeyEnd - lKeyIndex) >= 8)
            {
                lHash += (*(UInt64 *)(lKey + lKeyIndex)) * K3_64_1; lKeyIndex += 8;
                lHash ^= RotateRight(lHash, 33) * K1_64_1;

            }

            if ((lKeyEnd - lKeyIndex) >= 4)
            {
                lHash += (*(UInt32*)(lKey + lKeyIndex)) * K3_64_1; lKeyIndex += 4;
                lHash ^= RotateRight(lHash, 15) * K1_64_1;
            }

            if ((lKeyEnd - lKeyIndex) >= 2)
            {
                lHash += (*(UInt16*)(lKey + lKeyIndex)) * K3_64_1; lKeyIndex += 2;
                lHash ^= RotateRight(lHash, 13) * K1_64_1;
            }

            if ((lKeyEnd - lKeyIndex) >= 1)
            {
                lHash += (*(byte*)(lKey + lKeyIndex)) * K3_64_1;
                lHash ^= RotateRight(lHash, 25) * K1_64_1;
            }

            lHash ^= RotateRight(lHash, 33);
            lHash *= K0_64_1;
            lHash ^= RotateRight(lHash, 33);
        }

        //---------------------------------------------------------------------------//
        private const ulong K0_64_2  = 0xD6D018F5;
        private const ulong K1_64_2  = 0xA2AA033B;
        private const ulong K2_64_2  = 0x62992FC1;
        private const ulong K3_64_2  = 0x30BC5B29;

        //---------------------------------------------------------------------------//
        public static void Hash64_2(byte[] lKey, uint lStartOffset, uint lLength, uint lSeed, out byte[] lOutput)
        {
            uint lKeyIndex = lStartOffset;
            uint lKeyEnd = lKeyIndex + lLength;

            if (lKey.Length < lKeyEnd)
            {
                throw new IndexOutOfRangeException("Given Key for hashing is not of expected length");
            }

            ulong lHash = ((lSeed + K2_64_2) * K0_64_2) + lLength;

            if (lLength >= 32)
            {
                ulong[] lV = { lHash, lHash, lHash, lHash };

                do
                {
                    lV[0] += Read_u64(lKey, lKeyIndex) * K0_64_2; lKeyIndex += 8; lV[0] = RotateRight(lV[0],29) + lV[2];
                    lV[1] += Read_u64(lKey, lKeyIndex) * K1_64_2; lKeyIndex += 8; lV[1] = RotateRight(lV[1],29) + lV[3];
                    lV[2] += Read_u64(lKey, lKeyIndex) * K2_64_2; lKeyIndex += 8; lV[2] = RotateRight(lV[2],29) + lV[0];
                    lV[3] += Read_u64(lKey, lKeyIndex) * K3_64_2; lKeyIndex += 8; lV[3] = RotateRight(lV[3],29) + lV[1];
                }
                while (lKeyIndex <= (lKeyEnd - 32));

                lV[2] ^= RotateRight(((lV[0] + lV[3]) * K0_64_2) + lV[1], 30) * K1_64_2;
                lV[3] ^= RotateRight(((lV[1] + lV[2]) * K1_64_2) + lV[0], 30) * K0_64_2;
                lV[0] ^= RotateRight(((lV[0] + lV[2]) * K0_64_2) + lV[3], 30) * K1_64_2;
                lV[1] ^= RotateRight(((lV[1] + lV[3]) * K1_64_2) + lV[2], 30) * K0_64_2;
                lHash += lV[0] ^ lV[1];
            }

            if ((lKeyEnd - lKeyIndex) >= 16)
            {
                ulong lV0 = lHash + (Read_u64(lKey, lKeyIndex) * K2_64_2); lKeyIndex += 8; lV0 = RotateRight(lV0, 29) * K3_64_2;
                ulong lV1 = lHash + (Read_u64(lKey, lKeyIndex) * K2_64_2); lKeyIndex += 8; lV1 = RotateRight(lV1, 29) * K3_64_2;
                lV0 ^= RotateRight(lV0 * K0_64_2, 34) + lV1;
                lV1 ^= RotateRight(lV1 * K3_64_2, 34) + lV0;
                lHash += lV1;
            }

            if ((lKeyEnd - lKeyIndex) >= 8)
            {
                lHash += Read_u64(lKey, lKeyIndex) * K3_64_2; lKeyIndex += 8;
                lHash ^= RotateRight(lHash, 36) * K1_64_2;
            }

            if ((lKeyEnd - lKeyIndex) >= 4)
            {
                lHash += Read_u32(lKey, lKeyIndex) * K3_64_2; lKeyIndex += 4;
                lHash ^= RotateRight(lHash, 15) * K1_64_2;
            }

            if ((lKeyEnd - lKeyIndex) >= 2)
            {
                lHash += Read_u16(lKey, lKeyIndex) * K3_64_2; lKeyIndex += 2;
                lHash ^= RotateRight(lHash, 15) * K1_64_2;
            }

            if ((lKeyEnd - lKeyIndex) >= 1)
            {
                lHash += Read_u8(lKey, lKeyIndex) * K3_64_2;
                lHash ^= RotateRight(lHash, 23) * K1_64_2;
            }

            lHash ^= RotateRight(lHash, 28);
            lHash *= K0_64_2;
            lHash ^= RotateRight(lHash, 29);

            lOutput = BitConverter.GetBytes(lHash);
        }

        //---------------------------------------------------------------------------//
        /* rotate right idiom recognized by compiler*/
        private static ulong RotateRight(ulong lV, uint lK)
        {
            int lSignedK = (int)lK;
            return (lV >> lSignedK) | (lV << (64 - lSignedK));
        }

        //---------------------------------------------------------------------------//
        // unaligned reads, fast and safe on Nehalem and later microarchitectures
        private static ulong Read_u64(byte[] lData, uint lOffset)
        {
            return BitConverter.ToUInt64(lData, (int)lOffset);
        }

        //---------------------------------------------------------------------------//
        private static ulong Read_u32(byte[] lData, uint lOffset)
        {
            return BitConverter.ToUInt32(lData, (int)lOffset);
        }

        //---------------------------------------------------------------------------//
        private static ulong Read_u16(byte[] lData, uint lOffset)
        {
            return BitConverter.ToUInt16(lData, (int)lOffset);
        }

        //---------------------------------------------------------------------------//
        private static ulong Read_u8(byte[] lData, uint lOffset)
        {
            return lData[lOffset];
        }
    }
}
