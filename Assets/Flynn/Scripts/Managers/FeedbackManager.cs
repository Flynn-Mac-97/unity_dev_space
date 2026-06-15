using UnityEngine;
using Flynn.Events;

/// <summary>
/// Scene-level manager for VFX feedback. Subscribes to GameEventBus events
/// and drives HitImpactFX, CameraShake, and HitStop for juicy combat feel.
/// One per scene on the MANAGERS GameObject. NOT a singleton.
/// </summary>
public class FeedbackManager : MonoBehaviour
{
    // ── Resource-type colors ────────────────────────────────────────────────

    private static readonly Color StoneColor    = new Color(0.75f, 0.72f, 0.68f);
    private static readonly Color WoodColor     = new Color(0.65f, 0.45f, 0.2f);
    private static readonly Color TechColor     = new Color(0.3f, 0.85f, 0.95f);
    private static readonly Color DefaultColor  = new Color(1f, 0.85f, 0.4f);

    // Track whether the current swing hit something (for air whoosh)
    private bool _swingHitTarget;
    private Vector3 _lastSwingAimPoint;

    // ── Unity messages ──────────────────────────────────────────────────────

    private void OnEnable()
    {
        GameEventBus.Instance.Subscribe<ResourceDamaged>(OnResourceDamaged);
        GameEventBus.Instance.Subscribe<ResourceDepleted>(OnResourceDepleted);
        GameEventBus.Instance.Subscribe<ToolSwingStarted>(OnToolSwingStarted);
        GameEventBus.Instance.Subscribe<ToolHitTarget>(OnToolHitTarget);
        GameEventBus.Instance.Subscribe<ItemPickedUp>(OnItemPickedUp);
        GameEventBus.Instance.Subscribe<RopeGrappleStarted>(OnRopeGrappleStarted);
        GameEventBus.Instance.Subscribe<BatteryLow>(OnBatteryLow);
        GameEventBus.Instance.Subscribe<BatteryEmpty>(OnBatteryEmpty);
    }

    private void OnDisable()
    {
        if (GameEventBus.Instance == null) return;
        GameEventBus.Instance.Unsubscribe<ResourceDamaged>(OnResourceDamaged);
        GameEventBus.Instance.Unsubscribe<ResourceDepleted>(OnResourceDepleted);
        GameEventBus.Instance.Unsubscribe<ToolSwingStarted>(OnToolSwingStarted);
        GameEventBus.Instance.Unsubscribe<ToolHitTarget>(OnToolHitTarget);
        GameEventBus.Instance.Unsubscribe<ItemPickedUp>(OnItemPickedUp);
        GameEventBus.Instance.Unsubscribe<RopeGrappleStarted>(OnRopeGrappleStarted);
        GameEventBus.Instance.Unsubscribe<BatteryLow>(OnBatteryLow);
        GameEventBus.Instance.Unsubscribe<BatteryEmpty>(OnBatteryEmpty);
    }

    // ── Event handlers ──────────────────────────────────────────────────────

    private void OnToolSwingStarted(ToolSwingStarted evt)
    {
        _swingHitTarget = false;
        _lastSwingAimPoint = evt.AimPoint;
        // Defer air whoosh — if a hit event arrives this frame, skip it
        StartCoroutine(DeferredAirWhoosh());
    }

    private System.Collections.IEnumerator DeferredAirWhoosh()
    {
        // Wait one frame so hit events can set _swingHitTarget first
        yield return null;
        if (!_swingHitTarget)
        {
            var fx = HitImpactFX.Instance;
            if (fx != null)
                fx.PlayAirWhoosh(_lastSwingAimPoint, _lastSwingAimPoint - transform.position);
        }
    }

    private void OnResourceDamaged(ResourceDamaged evt)
    {
        _swingHitTarget = true;
        var fx = HitImpactFX.Instance;
        if (fx == null) return;

        Color color = ResourceColor(evt.Node);
        Vector3 dir = evt.Node != null ? evt.Position - transform.position : Vector3.forward;
        float strength = Mathf.Clamp01(evt.Damage / 3f);

        fx.PlayHit(evt.Position, dir, evt.Damage, color, evt.Node?.gameObject);
        HitFlash.Play(evt.Node?.gameObject, 0.1f);
        PlayCameraShake(0.08f + 0.15f * strength, 0.15f);
        PlayHitStop(strength);
    }

    private void OnResourceDepleted(ResourceDepleted evt)
    {
        _swingHitTarget = true;
        var fx = HitImpactFX.Instance;
        if (fx == null) return;

        Color color = ResourceColor(evt.Node);
        Vector3 dir = evt.Node != null ? evt.Position - transform.position : Vector3.forward;

        fx.PlayHit(evt.Position, dir, 0, color, evt.Node?.gameObject);
        HitFlash.Play(evt.Node?.gameObject, 0.15f);
        PlayCameraShake(0.3f, 0.3f);
        PlayHitStop(1f);
    }

    private void OnToolHitTarget(ToolHitTarget evt)
    {
        _swingHitTarget = true;
        var fx = HitImpactFX.Instance;
        if (fx == null) return;

        var hit = evt.Hit;
        float strength = Mathf.Clamp01(hit.Damage / 3f);

        fx.PlaySurfaceHit(hit.HitPoint, hit.HitDirection, hit.Source);
        HitFlash.Play(hit.Source, 0.08f);
        PlayCameraShake(0.06f, 0.1f);
        PlayHitStop(strength * 0.5f);
    }

    private void OnItemPickedUp(ItemPickedUp evt)
    {
        // Future: pickup sparkle
    }

    private void OnRopeGrappleStarted(RopeGrappleStarted evt)
    {
        // Future: grapple launch effect
    }

    private void OnBatteryLow(BatteryLow evt)
    {
        // Future: warning pulse
    }

    private void OnBatteryEmpty(BatteryEmpty evt)
    {
        // Future: screen flash
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Color ResourceColor(ResourceNode node)
    {
        if (node == null) return DefaultColor;
        return node.ResourceType switch
        {
            ResourceType.Stone    => StoneColor,
            ResourceType.Wood     => WoodColor,
            ResourceType.TechTrash => TechColor,
            _ => DefaultColor
        };
    }

    private static void PlayCameraShake(float intensity, float duration)
    {
        var shake = CameraShake.Instance;
        if (shake != null) shake.Shake(intensity, duration);
    }

    private static void PlayHitStop(float strength)
    {
        var stop = HitStop.Instance;
        if (stop != null) stop.Play(strength);
    }
}
