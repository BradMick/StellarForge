using UnityEngine;

//Seedable 3D Perlin noise (Ken Perlin's improved noise). Fully deterministic: the same seed
//and sample position always return the same value — planetary terrain depends on that so
//neighboring tiles at any LOD compute identical heights along shared borders
public class PerlinNoise3D
{
    private readonly int[] perm = new int[512];

    public PerlinNoise3D(int _seed)
    {
        int[] source = new int[256];
        for (int i = 0; i < 256; i++)
            source[i] = i;

        //Fisher-Yates shuffle of the permutation table from the seed
        System.Random rng = new System.Random(_seed);
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = source[i];
            source[i] = source[j];
            source[j] = tmp;
        }

        for (int i = 0; i < 512; i++)
            perm[i] = source[i & 255];
    }

    //Copies the permutation table (512 ints) for GPU upload — the compute shader mirror
    //of this class consumes the same tables so CPU and GPU noise agree
    public void CopyTo(int[] _destination, int _offset)
    {
        System.Array.Copy(perm, 0, _destination, _offset, 512);
    }

    //Noise value in roughly [-1, 1]
    public float Sample(Vector3 _p)
    {
        int X = Mathf.FloorToInt(_p.x) & 255;
        int Y = Mathf.FloorToInt(_p.y) & 255;
        int Z = Mathf.FloorToInt(_p.z) & 255;

        float x = _p.x - Mathf.Floor(_p.x);
        float y = _p.y - Mathf.Floor(_p.y);
        float z = _p.z - Mathf.Floor(_p.z);

        float u = Fade(x);
        float v = Fade(y);
        float w = Fade(z);

        int A = perm[X] + Y, AA = perm[A] + Z, AB = perm[A + 1] + Z;
        int B = perm[X + 1] + Y, BA = perm[B] + Z, BB = perm[B + 1] + Z;

        return Lerp(w,
            Lerp(v, Lerp(u, Grad(perm[AA], x, y, z),
                            Grad(perm[BA], x - 1, y, z)),
                    Lerp(u, Grad(perm[AB], x, y - 1, z),
                            Grad(perm[BB], x - 1, y - 1, z))),
            Lerp(v, Lerp(u, Grad(perm[AA + 1], x, y, z - 1),
                            Grad(perm[BA + 1], x - 1, y, z - 1)),
                    Lerp(u, Grad(perm[AB + 1], x, y - 1, z - 1),
                            Grad(perm[BB + 1], x - 1, y - 1, z - 1))));
    }

    private static float Fade(float _t)
    {
        return _t * _t * _t * (_t * (_t * 6.0f - 15.0f) + 10.0f);
    }

    private static float Lerp(float _t, float _a, float _b)
    {
        return _a + _t * (_b - _a);
    }

    private static float Grad(int _hash, float _x, float _y, float _z)
    {
        int h = _hash & 15;
        float u = h < 8 ? _x : _y;
        float v = h < 4 ? _y : h == 12 || h == 14 ? _x : _z;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }
}
