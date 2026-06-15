using UnityEngine;

public enum BillboardMode { Full, YOnly, PitchOnly, SpriteBillboard }

public interface IBillboardTarget
{
    Transform BillboardTransform { get; }
    BillboardMode Mode { get; }
}
