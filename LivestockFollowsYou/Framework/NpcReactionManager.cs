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
    private const int WalkSingleLineCount = 10;
    private const int WalkMultiLineCount = 6;

    // Character-specific line counts
    private static readonly Dictionary<string, (int Single, int Multi)> CharacterLineCounts = new()
    {
        ["Marnie"] = (5, 3),
        ["Shane"] = (5, 3),
        ["Jas"] = (5, 3)
    };

    // Weather line counts
    private const int RainSingleCount = 10;
    private const int RainMultiCount = 6;
    private const int StormSingleCount = 8;
    private const int StormMultiCount = 5;
    private const int SnowSingleCount = 8;
    private const int SnowMultiCount = 5;

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
            if (npc.IsInvisible || npc is Child or Horse or Pet)
                continue;

            if (reactedNpcs.Contains(npc.Name))
                continue;

            float distance = Vector2.Distance(player.Position, npc.Position) / 64f;
            if (distance > ReactionRadiusTiles)
                continue;

            if (random.NextDouble() > ReactionChance)
            {
                reactedNpcs.Add(npc.Name);
                continue;
            }

            string line = GetReactionLine(activeFollowers, npc, location);
            int preTimer = random.Next(500, 1500);
            npc.showTextAboveHead(line, duration: 3500, preTimer: preTimer);
            reactedNpcs.Add(npc.Name);

            break;
        }
    }

    public void Reset()
    {
        reactedNpcs.Clear();
        tickCounter = 0;
    }

    private string GetReactionLine(List<FollowingAnimal> activeFollowers, NPC npc, GameLocation location)
    {
        bool multi = activeFollowers.Count > 1;
        var animal = activeFollowers[random.Next(activeFollowers.Count)].Animal;
        bool isWalk = activeFollowers.Any(f => f.IsWalk);

        string key;

        if (isWalk && !CharacterLineCounts.ContainsKey(npc.Name))
        {
            key = multi
                ? $"npc.reaction.walk.multi.{random.Next(WalkMultiLineCount)}"
                : $"npc.reaction.walk.single.{random.Next(WalkSingleLineCount)}";
        }
        else if (CharacterLineCounts.TryGetValue(npc.Name, out var counts))
        {
            string charKey = npc.Name.ToLower();
            key = multi
                ? $"npc.reaction.{charKey}.multi.{random.Next(counts.Multi)}"
                : $"npc.reaction.{charKey}.single.{random.Next(counts.Single)}";
        }
        else if (location.IsLightningHere())
        {
            key = multi
                ? $"npc.reaction.storm.multi.{random.Next(StormMultiCount)}"
                : $"npc.reaction.storm.single.{random.Next(StormSingleCount)}";
        }
        else if (location.IsRainingHere())
        {
            key = multi
                ? $"npc.reaction.rain.multi.{random.Next(RainMultiCount)}"
                : $"npc.reaction.rain.single.{random.Next(RainSingleCount)}";
        }
        else if (location.IsSnowingHere())
        {
            key = multi
                ? $"npc.reaction.snow.multi.{random.Next(SnowMultiCount)}"
                : $"npc.reaction.snow.single.{random.Next(SnowSingleCount)}";
        }
        else
        {
            key = multi
                ? $"npc.reaction.multi.{random.Next(MultiLineCount)}"
                : $"npc.reaction.single.{random.Next(SingleLineCount)}";
        }

        return Helper.Translation.Get(key, new
        {
            name = animal.displayName,
            type = animal.type.Value,
            count = activeFollowers.Count.ToString()
        });
    }
}
