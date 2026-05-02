using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace DeluxeGrabberFix.Tests;

// ModBuildConfig sets Private=false on the SDV/MonoGame/SMAPI references so
// they don't get copied into mod folders. Test projects need them at runtime.
// Rather than fight ModBuildConfig's MSBuild targets, we hook
// AssemblyLoadContext.Default.Resolving once at module load and resolve
// against the Stardew Valley install directory.
//
// Override the install location by setting the STARDEW_VALLEY_PATH environment
// variable. Fallback is the default Steam install on this machine.
internal static class TestModuleInit
{
    [ModuleInitializer]
    internal static void Init()
    {
        string sdvPath = Environment.GetEnvironmentVariable("STARDEW_VALLEY_PATH")
            ?? @"D:\Steam\steamapps\common\Stardew Valley";

        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string candidate = Path.Combine(sdvPath, name.Name + ".dll");
            return File.Exists(candidate)
                ? context.LoadFromAssemblyPath(candidate)
                : null;
        };
    }
}
