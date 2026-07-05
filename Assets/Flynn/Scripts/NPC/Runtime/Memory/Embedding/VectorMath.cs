using System;

using Flynn.Core;
using Flynn.UI.Core;

namespace Flynn.Npc.Memory
{
    // Vector helpers for brute-force semantic recall. Embeddings from all-MiniLM
    // are not guaranteed unit-length, so Cosine normalizes; CosineNormalized is
    // the fast path when both inputs are already unit vectors.
    public static class VectorMath
    {
        // Cosine similarity in [-1, 1]. Returns 0 for null/length-mismatch/zero
        // vectors so a bad embedding can never dominate the top-k.
        public static float Cosine(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length == 0) return 0f;
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += (double)a[i] * b[i];
                na  += (double)a[i] * a[i];
                nb  += (double)b[i] * b[i];
            }
            if (na <= 0 || nb <= 0) return 0f;
            return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
        }
    }
}
