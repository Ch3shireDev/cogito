using System.Linq;
using Microsoft.Xna.Framework.Input.Touch;

namespace Cogito.Core.Inputs;

/// <summary>
///     Provides extension methods for the TouchCollection type.
/// </summary>
public static class TouchCollectionExtensions
{
    /// <summary>
    ///     Determines if there are any touches on the screen.
    /// </summary>
    /// <param name="touchState">The current TouchCollection.</param>
    /// <returns>True if there are any touches in the Pressed or Moved state, false otherwise</returns>
    public static bool AnyTouch(this TouchCollection touchState)
    {
        return touchState.Any(location => location.State is TouchLocationState.Pressed or TouchLocationState.Moved);
    }
}