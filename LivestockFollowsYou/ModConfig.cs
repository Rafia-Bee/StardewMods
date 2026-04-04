namespace LivestockFollowsYou;

/// <summary>Mod configuration options, editable via config.json or Generic Mod Config Menu.</summary>
internal class ModConfig
{
    /// <summary>Whether the mod is active. When disabled, animals teleport to barns as normal.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Speed multiplier for how fast animals catch up when falling behind.</summary>
    public float FollowSpeedMultiplier { get; set; } = 1.0f;

    /// <summary>If the animal falls this many tiles behind, it teleports closer to the player.</summary>
    public int RubberBandDistance { get; set; } = 10;

    /// <summary>Game time at which undelivered animals are automatically sent to their barn/coop (e.g. 2000 = 8:00 PM).</summary>
    public int AutoDeliverTime { get; set; } = 2000;

    /// <summary>Whether following animals occasionally make sounds while walking.</summary>
    public bool AnimalSoundsWhileFollowing { get; set; } = true;

    /// <summary>Seconds between animal sounds while following.</summary>
    public int SoundIntervalSeconds { get; set; } = 15;

    /// <summary>Whether to show HUD notifications for follow and delivery events.</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Whether nearby NPCs react with speech bubbles when you escort animals.</summary>
    public bool NpcReactionsEnabled { get; set; } = true;

    /// <summary>Whether to log debug messages to the SMAPI console.</summary>
    public bool DebugLogging { get; set; } = false;

    /// <summary>Happiness gained per grass eaten during a walk (0-255 scale).</summary>
    public int GrazingHappinessBoost { get; set; } = 15;

    /// <summary>Friendship points needed to send an animal home alone via the Grazing Bell (750 = 3 hearts).</summary>
    public int MinFriendshipToSendHome { get; set; } = 750;

    /// <summary>Seconds the player must stand still before walking animals start grazing nearby grass.</summary>
    public int GrazingIdleSeconds { get; set; } = 2;
}
