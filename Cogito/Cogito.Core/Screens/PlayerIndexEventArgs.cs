using System;
using Microsoft.Xna.Framework;

namespace Cogito.Core.Screens;

/// <summary>
///     Custom event argument which includes the index of the player who
///     triggered the event. This is used by the MenuEntry.Selected event.
/// </summary>
internal class PlayerIndexEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PlayerIndexEventArgs" /> class.
    /// </summary>
    /// <param name="playerIndex">The player index associated with the event.</param>
    public PlayerIndexEventArgs(PlayerIndex playerIndex)
    {
        PlayerIndex = playerIndex;
    }

    /// <summary>
    ///     Gets the index of the player who triggered this event.
    /// </summary>
    public PlayerIndex PlayerIndex { get; }
}