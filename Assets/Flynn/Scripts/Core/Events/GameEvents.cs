using UnityEngine;

using Flynn.Core;
using Flynn.Interactables;
using Flynn.Player;
using Flynn.Resources;

namespace Flynn.Events
{
    // ── Player movement events ───────────────────────────────────────────────

    public readonly struct PlayerMoved
    {
        public readonly Vector3 Position;
        public readonly float Speed;
        public PlayerMoved(Vector3 position, float speed) { Position = position; Speed = speed; }
    }

    public readonly struct PlayerJumped
    {
        public readonly Vector3 Position;
        public PlayerJumped(Vector3 position) { Position = position; }
    }

    public readonly struct PlayerLanded
    {
        public readonly Vector3 Position;
        public PlayerLanded(Vector3 position) { Position = position; }
    }

    // ── Tool events ─────────────────────────────────────────────────────────

    public readonly struct ToolSwingStarted
    {
        public readonly ToolType ToolType;
        public readonly Vector3 AimPoint;
        public ToolSwingStarted(ToolType toolType, Vector3 aimPoint) { ToolType = toolType; AimPoint = aimPoint; }
    }

    public readonly struct ToolHitTarget
    {
        public readonly ToolHitContext Hit;
        public ToolHitTarget(ToolHitContext hit) { Hit = hit; }
    }

    public readonly struct ToolThrowStarted
    {
        public readonly Vector3 Target;
        public ToolThrowStarted(Vector3 target) { Target = target; }
    }

    public readonly struct ToolThrowReleased
    {
        public readonly Vector3 Target;
        public readonly float Charge;
        public ToolThrowReleased(Vector3 target, float charge) { Target = target; Charge = charge; }
    }

    public readonly struct ToolReturned { }

    // ── Resource events ─────────────────────────────────────────────────────

    public readonly struct ResourceHit
    {
        public readonly ResourceNode Node;
        public readonly Vector2 HitDirection;
        public readonly int Damage;
        public ResourceHit(ResourceNode node, Vector2 hitDirection, int damage = 1)
        {
            Node = node; HitDirection = hitDirection; Damage = damage;
        }
    }

    public readonly struct ResourceHovered
    {
        public readonly ResourceNode Node;
        public ResourceHovered(ResourceNode node) { Node = node; }
    }

    public readonly struct ResourceUnhovered
    {
        public readonly ResourceNode Node;
        public ResourceUnhovered(ResourceNode node) { Node = node; }
    }

    public readonly struct ResourceDamaged
    {
        public readonly ResourceNode Node;
        public readonly int Damage;
        public readonly int RemainingHp;
        public readonly Vector3 Position;
        public ResourceDamaged(ResourceNode node, int damage, int remainingHp)
        {
            Node = node; Damage = damage; RemainingHp = remainingHp;
            Position = node != null ? node.transform.position : Vector3.zero;
        }
    }

    public readonly struct ResourceDepleted
    {
        public readonly ResourceNode Node;
        public readonly Vector3 Position;
        public ResourceDepleted(ResourceNode node)
        {
            Node = node;
            Position = node != null ? node.transform.position : Vector3.zero;
        }
    }

    public readonly struct ResourceDropSpawned
    {
        public readonly ItemDefinition Item;
        public readonly int Count;
        public readonly Vector3 Position;
        public ResourceDropSpawned(ItemDefinition item, int count, Vector3 position)
        {
            Item = item; Count = count; Position = position;
        }
    }

    // ── Inventory events ─────────────────────────────────────────────────────

    public readonly struct ItemPickedUp
    {
        public readonly ItemDefinition Item;
        public readonly int Count;
        public ItemPickedUp(ItemDefinition item, int count) { Item = item; Count = count; }
    }

    public readonly struct ItemDropped
    {
        public readonly ItemDefinition Item;
        public readonly int Count;
        public readonly Vector3 Position;
        public ItemDropped(ItemDefinition item, int count, Vector3 position)
        {
            Item = item; Count = count; Position = position;
        }
    }

    public readonly struct CurrencyCollected
    {
        public readonly ItemDefinition Currency;
        public readonly int Amount;
        public CurrencyCollected(ItemDefinition currency, int amount) { Currency = currency; Amount = amount; }
    }

    // ── Battery events ───────────────────────────────────────────────────────

    public readonly struct BatteryChanged
    {
        public readonly int Current;
        public readonly int Max;
        public readonly float Percent;
        public BatteryChanged(int current, int max)
        {
            Current = current; Max = max;
            Percent = max > 0 ? (float)current / max : 0f;
        }
    }

    public readonly struct BatteryEmpty { }

    public readonly struct BatteryLow
    {
        public readonly int Current;
        public readonly float Percent;
        public BatteryLow(int current, float percent) { Current = current; Percent = percent; }
    }

    // ── Power buildup events ─────────────────────────────────────────────────

    public enum ActionType { Swing, Throw }

    public readonly struct PowerBuildupStarted
    {
        public readonly ActionType Type;
        public PowerBuildupStarted(ActionType type) { Type = type; }
    }

    public readonly struct PowerBuildupChanged
    {
        public readonly float Normalized;
        public readonly bool InPerfectZone;
        public PowerBuildupChanged(float normalized, bool inPerfectZone)
        { Normalized = normalized; InPerfectZone = inPerfectZone; }
    }

    public readonly struct PowerBuildupReleased
    {
        public readonly float Normalized;
        public readonly bool IsPerfect;
        public readonly int Damage;
        public readonly float BatteryCost;
        public readonly ActionType Type;
        public PowerBuildupReleased(float normalized, bool isPerfect, int damage, float batteryCost, ActionType type)
        { Normalized = normalized; IsPerfect = isPerfect; Damage = damage; BatteryCost = batteryCost; Type = type; }
    }

    // ── Scan events (future) ─────────────────────────────────────────────────

    public readonly struct ScanStarted
    {
        public readonly ScanTarget Target;
        public ScanStarted(ScanTarget target) { Target = target; }
    }

    public readonly struct ScanProgressed
    {
        public readonly ScanTarget Target;
        public readonly float Percent;
        public ScanProgressed(ScanTarget target, float percent) { Target = target; Percent = percent; }
    }

    public readonly struct ScanCompleted
    {
        public readonly ScanTarget Target;
        public ScanCompleted(ScanTarget target) { Target = target; }
    }

    /// <summary>Scan finished and revealed info — HUD/world-tag shows the lines
    /// (resource stats, a lore fragment, a hint). Fired alongside ScanCompleted.</summary>
    public readonly struct ScanRevealed
    {
        public readonly ScanTarget Target;
        public readonly string[] Lines;
        public readonly Vector3 Position;
        public ScanRevealed(ScanTarget target, string[] lines, Vector3 position)
        {
            Target = target; Lines = lines; Position = position;
        }
    }

    // ── Transmitter (start-zone power objective) ──────────────────────────────

    /// <summary>Transmitter power changed — HUD gauge listens.</summary>
    public readonly struct TransmitterPowerChanged
    {
        public readonly float Current;
        public readonly float Max;
        public readonly float Percent;
        public TransmitterPowerChanged(float current, float max)
        {
            Current = current; Max = max;
            Percent = max > 0f ? current / max : 0f;
        }
    }

    /// <summary>The player fed fuel into the transmitter — feedback/SFX listens.</summary>
    public readonly struct TransmitterFed
    {
        public readonly ItemDefinition Fuel;
        public readonly float PowerAdded;
        public TransmitterFed(ItemDefinition fuel, float powerAdded) { Fuel = fuel; PowerAdded = powerAdded; }
    }

    /// <summary>Crossed the venture threshold (both directions). The exit gate listens.</summary>
    public readonly struct TransmitterVentureStateChanged
    {
        public readonly bool Open;
        public TransmitterVentureStateChanged(bool open) { Open = open; }
    }

    /// <summary>Transmitter hit zero — lockout. NPC warnings / emphasis listen.</summary>
    public readonly struct TransmitterDepleted { }

    // ── Tutorial ──────────────────────────────────────────────────────────────

    /// <summary>The active tutorial beat changed — HUD/world-tag shows the prompt.</summary>
    public readonly struct TutorialBeatChanged
    {
        public readonly int Index;
        public readonly string Prompt;
        public TutorialBeatChanged(int index, string prompt) { Index = index; Prompt = prompt; }
    }

    /// <summary>All tutorial beats finished.</summary>
    public readonly struct TutorialCompleted { }

    // ── NPC dialogue ──────────────────────────────────────────────────────────

    /// <summary>A dialogue with an NPC opened.</summary>
    public readonly struct NpcDialogueOpened
    {
        public readonly string NpcId;
        public NpcDialogueOpened(string npcId) { NpcId = npcId; }
    }

    /// <summary>A dialogue with an NPC closed (e.g. to trigger the tutorial after the
    /// intro chat).</summary>
    public readonly struct NpcDialogueClosed
    {
        public readonly string NpcId;
        public NpcDialogueClosed(string npcId) { NpcId = npcId; }
    }
}
