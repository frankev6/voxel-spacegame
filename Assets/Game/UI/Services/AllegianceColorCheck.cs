using UnityEngine;

namespace Game.UI.Services
{
    /// <summary>SPEC-60 §5.12: minimum CIE76 distance after full-severity colour-vision simulation.</summary>
    public static class AllegianceColorCheck
    {
        // Machado, Oliveira, Fernandes (2009), severity 1.0; rows multiply linear sRGB.
        // Author's supplementary table: https://www.inf.ufrgs.br/~oliveira/pubs_files/CVD_Simulation/CVD_Simulation.html
        public static float Run(Color ally, Color enemy)
        {
            var a = new Vector3(Linear(ally.r), Linear(ally.g), Linear(ally.b));
            var b = new Vector3(Linear(enemy.r), Linear(enemy.g), Linear(enemy.b));
            float minimum = float.PositiveInfinity;
            for (int mode = 0; mode < 3; mode++)
                minimum = Mathf.Min(minimum, Vector3.Distance(Lab(Simulate(a, mode)), Lab(Simulate(b, mode))));
            return minimum;
        }
        static Vector3 Simulate(Vector3 c, int mode)
        {
            Vector3 result = mode switch
            {
                0 => new Vector3(.152286f*c.x + 1.052583f*c.y - .204868f*c.z,
                    .114503f*c.x + .786281f*c.y + .099216f*c.z,
                    -.003882f*c.x - .048116f*c.y + 1.051998f*c.z),
                1 => new Vector3(.367322f*c.x + .860646f*c.y - .227968f*c.z,
                    .280085f*c.x + .672501f*c.y + .047413f*c.z,
                    -.011820f*c.x + .042940f*c.y + .968881f*c.z),
                _ => new Vector3(1.255528f*c.x - .076749f*c.y - .178779f*c.z,
                    -.078411f*c.x + .930809f*c.y + .147602f*c.z,
                    .004733f*c.x + .691367f*c.y + .303900f*c.z)
            };
            return new Vector3(Mathf.Clamp01(result.x), Mathf.Clamp01(result.y), Mathf.Clamp01(result.z));
        }
        static Vector3 Lab(Vector3 c)
        {
            // Linear sRGB -> CIE XYZ, D65 reference white (2-degree observer), then CIELAB.
            float x = LabCurve((.4124564f*c.x + .3575761f*c.y + .1804375f*c.z) / .95047f);
            float y = LabCurve(.2126729f*c.x + .7151522f*c.y + .0721750f*c.z);
            float z = LabCurve((.0193339f*c.x + .1191920f*c.y + .9503041f*c.z) / 1.08883f);
            return new Vector3(116*y - 16, 500*(x-y), 200*(y-z));
        }
        static float LabCurve(float value) => value > 216f/24389f ? Mathf.Pow(value, 1f/3f) : (24389f/27f*value + 16)/116;
        static float Linear(float value)
        {
            value = Mathf.Clamp01(value);
            return value <= .04045f ? value/12.92f : Mathf.Pow((value + .055f)/1.055f, 2.4f);
        }
    }
}
