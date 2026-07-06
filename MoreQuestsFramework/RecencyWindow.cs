using System.Collections.Generic;

namespace MoreQuestsFramework;

// The bounded "remember the last N" window behind AntiRepetition's item and npc recency. Pulled
// out so its behavior can be tested on its own without touching Game1 or the static config. A max
// of 0 keeps nothing, which turns the repeat block off for that dimension.
internal static class RecencyWindow
{
    public static void Push(Queue<string> queue, string value, int max)
    {
        queue.Enqueue(value);
        while (queue.Count > max)
            queue.Dequeue();
    }
}
