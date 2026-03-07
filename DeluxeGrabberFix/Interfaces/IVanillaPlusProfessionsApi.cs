using System.Collections.Generic;
using StardewValley;

namespace DeluxeGrabberFix.Interfaces;

/// <summary>Minimal subset of VPP's API needed for DGF compatibility.</summary>
public interface IVanillaPlusProfessionsApi
{
    IEnumerable<string> GetProfessionsForPlayer(Farmer who = null);
    IEnumerable<string> GetTalentsForPlayer(Farmer who = null);
}
