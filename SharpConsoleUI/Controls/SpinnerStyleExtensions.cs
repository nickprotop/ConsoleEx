using SharpConsoleUI.Parsing;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpConsoleUI.Controls;

/// <summary>
/// SpinnerStyle Extensions
/// </summary>
public static class SpinnerStyleExtensions
{
    /// <summary>
    /// get frame width with SpinnerStyle
    /// </summary>
    /// <param name="spinnerStyle"></param>
    /// <returns></returns>
    public static int FrameWidth(this SpinnerStyle spinnerStyle)
    {
        TryReadyFrameWidthCache();

        if(FrameWidthCache.TryGetValue(spinnerStyle, out var frameWidth)) return frameWidth;
        
        return 0;
    }

    private static Dictionary<SpinnerStyle, int> FrameWidthCache = [];
    private static readonly object FrameWidthCacheLock = new object();

    private static void TryReadyFrameWidthCache()
    {        
        if (FrameWidthCache.Count == 0)
        {
            lock (FrameWidthCacheLock)
            {
                if (FrameWidthCache.Count == 0)
                {
                    string[] frames;
                    int width = 0;

                    foreach (SpinnerStyle style in Enum.GetValues(typeof(SpinnerStyle)))
                    {
                        frames = SpinnerControl.FramesForStyle(style);

                        width = 0;

                        foreach (var f in frames)
                            width = Math.Max(width, MarkupParser.StripLength(f));

                        FrameWidthCache[style] = width;
                    }
                }
            } // end by: lock()
        }
    }
}
