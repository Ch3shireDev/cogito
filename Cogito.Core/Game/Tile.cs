using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Cogito.Core.Game;

/// <summary>
///     Represents the visual appearance and collision behavior of a tile.
/// </summary>
internal struct Tile
{
    /// <summary>
    ///     The texture that represents the tile's visual appearance.
    /// </summary>
    public Texture2D Texture;

    /// <summary>
    ///     The type of collision behavior this tile exhibits.
    /// </summary>
    public TileCollision Collision;

    /// <summary>
    ///     The standard width of a tile, measured in pixels.
    /// </summary>
    public const int Width = 40;

    /// <summary>
    ///     The standard height of a tile, measured in pixels.
    /// </summary>
    public const int Height = 32;

    /// <summary>
    ///     The size of a tile as a <see cref="Vector2" /> for convenience.
    /// </summary>
    public static readonly Vector2 Size = new(Width, Height);

    /// <summary>
    ///     Initializes a new instance of the <see cref="Tile" /> struct.
    /// </summary>
    /// <param name="texture">The texture representing the tile's appearance.<see cref="Texture2D" /></param>
    /// <param name="collision">The collision type that defines the tile's behavior.<see cref="TileCollision" /></param>
    public Tile(Texture2D texture, TileCollision collision)
    {
        Texture = texture;
        Collision = collision;
    }
}

/// <summary>
///     Controls the collision detection and response behavior of a tile.
/// </summary>
internal enum TileCollision
{
    /// <summary>
    ///     A passable tile is one which does not hinder player motion at all.
    /// </summary>
    Passable = 0,

    /// <summary>
    ///     An impassable tile is one which does not allow the player to move through
    ///     it at all. It is completely solid.
    /// </summary>
    Impassable = 1,

    /// <summary>
    ///     A platform tile is one which behaves like a passable tile except when the
    ///     player is above it. A player can jump up through a platform as well as move
    ///     past it to the left and right, but can not fall down through the top of it.
    /// </summary>
    Platform = 2,

    /// <summary>
    ///     A breakable tile is one which behaves like a platform tile except when the
    ///     player is below it, the player jumps up the tile breaks/disappears.
    ///     Our version of Mario :).
    /// </summary>
    Breakable = 3
}