using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;

namespace LivestockFollowsYou.Framework;

/// <summary>Shows speech bubble reactions from nearby NPCs when the player is escorting animals.</summary>
internal class NpcReactionManager
{
    private readonly IMonitor Monitor;
    private readonly IModHelper Helper;
    private readonly Func<ModConfig> GetConfig;

    private readonly HashSet<string> reactedNpcs = new();
    private readonly Random random = new();
    private int tickCounter;

    private const int CheckIntervalTicks = 60;
    private const float ReactionRadiusTiles = 7f;
    private const double ReactionChance = 0.85;
    private const int SingleLineCount = 35;
    private const int MultiLineCount = 20;

    public NpcReactionManager(IMonitor monitor, IModHelper helper, Func<ModConfig> getConfig)
    {
        Monitor = monitor;
        Helper = helper;
        GetConfig = getConfig;
    }

    public void Update(IReadOnlyList<FollowingAnimal> followers)
    {
        if (!GetConfig().NpcReactionsEnabled)
            return;

        tickCounter++;
        if (tickCounter < CheckIntervalTicks)
            return;
        tickCounter = 0;

        var activeFollowers = followers.Where(f => f.State == FollowState.FollowingPlayer).ToList();
        if (activeFollowers.Count == 0)
            return;

        var player = Game1.player;
        var location = player.currentLocation;
        if (location == null || Game1.isFestival())
            return;

        foreach (NPC npc in location.characters)
        {
            if (npc.IsInvisible || npc is Child)
                continue;

            if (reactedNpcs.Contains(npc.Name))
                continue;

            float distance = Vector2.Distance(player.Position, npc.Position) / 64f;
            if (distance > ReactionRadiusTiles)
                continue;

            // Not every NPC reacts - skip some for variety
            if (random.NextDouble() > ReactionChance)
            {
                reactedNpcs.Add(npc.Name);
                continue;
            }

            string line = GetReactionLine(activeFollowers);
            int preTimer = random.Next(500, 1500);
            npc.showTextAboveHead(line, duration: 3500, preTimer: preTimer);
            reactedNpcs.Add(npc.Name);

            if (GetConfig().DebugLogging)
                Monitor.Log($"{npc.Name} reacted: \"{line}\"", LogLevel.Debug);

            break;
        }
    }

    public void Reset()
    {
        reactedNpcs.Clear();
        tickCounter = 0;
    }

    private string GetReactionLine(List<FollowingAnimal> activeFollowers)
    {
        bool multi = activeFollowers.Count > 1;
        var animal = activeFollowers[random.Next(activeFollowers.Count)].Animal;

        string key = multi
            ? $"npc.reaction.multi.{random.Next(MultiLineCount)}"
            : $"npc.reaction.single.{random.Next(SingleLineCount)}";

        return Helper.Translation.Get(key, new
        {
            name = animal.displayName,
            type = animal.type.Value,
            count = activeFollowers.Count.ToString()
        });
    }
}
