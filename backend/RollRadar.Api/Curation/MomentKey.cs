using RollRadar.Api.Techniques;

namespace RollRadar.Api.Curation;

public static class MomentKey
{
    /// <summary>
    /// "a1b2c3.rear-naked-choke.66": video, technique and second. Dots appear neither in YouTube ids nor in
    /// technique ids, and the key does not change when the data is rebuilt from the same captions.
    /// </summary>
    public static string For(string videoId, string? techniqueId, string technique, int startSeconds) =>
        $"{videoId}.{techniqueId ?? Technique.ToId(technique)}.{startSeconds}";
}
