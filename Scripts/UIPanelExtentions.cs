using UnityEngine;

public static class UIPanelExtentions
{
    public static (float delay, float duration) GetTimelineParams(this Vector2 timeline, float totalDuration)
    {
        float delay = timeline.x * totalDuration;
        float end = timeline.y * totalDuration;
        float duration = end - delay;
        return (delay, duration);
    }
}