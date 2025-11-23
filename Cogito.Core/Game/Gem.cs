using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Cogito.Core.Game;

/// <summary>
///     A valuable item the player can collect.
/// </summary>
internal class Gem
{
    // Bounce control constants
    private const float BounceHeight = 0.18f;
    private const float BounceRate = 3.0f;
    private const float BounceSync = -0.75f;

    /// <summary>
    ///     The color of this gem, which can be used for visual distinction.
    /// </summary>
    public readonly Color Color = Color.Green;

    // The gem is animated from a base position along the Y axis.
    private Vector2 basePosition;
    private float bounce;

    private Vector2 collectedPosition;
    private SoundEffect collectedSound;

    private Vector2 levelDimensions;
    private Vector2 origin;

    private Texture2D texture;

    /// <summary>
    ///     The point value of this gem when collected by the player.
    /// </summary>
    public int Value = 10;

    /// <summary>
    ///     Constructs a new gem.
    /// </summary>
    /// <param name="level">The level instance to which this gem belongs.</param>
    /// <param name="position">The initial position of the gem in world space.</param>
    /// <param name="gemType">The type of gem, which determines its value, color, and behavior.</param>
    /// <param name="levelDimensions">The dimensions of the level, used for positioning and bounds checking.</param>
    public Gem(Level level, Vector2 position, char gemType, Vector2 levelDimensions)
    {
        Level = level;
        basePosition = position;
        this.levelDimensions = levelDimensions;

        switch (gemType)
        {
            case '1':
                Value = 10;
                Color = Color.Green;
                break;

            case '2':
                Value = 30;
                Color = Color.Yellow;
                break;

            case '3':
                Value = 50;
                Color = Color.Red;
                break;

            case '4':
                Value = 100;
                Color = Color.Blue; // Only because blue it is my favourite colour
                IsPowerUp = true;
                break;
        }

        LoadContent();
    }

    /// <summary>
    ///     Gets the level instance to which this gem belongs.
    /// </summary>
    public Level Level { get; }

    /// <summary>
    ///     Gets the current position of this gem in world space.
    /// </summary>
    public Vector2 Position => basePosition + new Vector2(0.0f, bounce);

    /// <summary>
    ///     Gets a circle which bounds this gem in world space.
    /// </summary>
    public Circle BoundingCircle => new(Position, Tile.Width / 3.0f);

    /// <summary>
    ///     Gets or sets the scale of the gem, used for visual effects like shrinking during collection.
    /// </summary>
    public Vector2 Scale { get; set; }

    /// <summary>
    ///     Gets or sets the current state of the gem (e.g., Waiting, Collecting, Collected).
    /// </summary>
    public GemState State { get; set; } = GemState.Waiting;

    /// <summary>
    ///     Gets or sets whether this gem is a power-up gem, providing special abilities to the player.
    /// </summary>
    public bool IsPowerUp { get; set; }

    /// <summary>
    ///     Loads the gem texture and collected sound.
    /// </summary>
    public void LoadContent()
    {
        texture = Level.Content.Load<Texture2D>("Sprites/Gem");
        origin = new Vector2(texture.Width / 2.0f, texture.Height / 2.0f);
        collectedSound = Level.Content.Load<SoundEffect>("Sounds/PlayerGemCollected");
    }

    /// <summary>
    ///     Bounces up and down in the air to entice players to collect them.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    /// <param name="collectionPoint">The point towards which the gem moves when collected.</param>
    public void Update(GameTime gameTime, Vector2 collectionPoint)
    {
        collectedPosition = collectionPoint;

        switch (State)
        {
            case GemState.Collected:
                break;

            case GemState.Collecting:
                if (basePosition.Y > collectedPosition.Y)
                {
                    // Move towards top centre of the screen.
                    var direction = collectedPosition - basePosition;
                    direction.Normalize();
                    basePosition += direction * 256 * (float)gameTime.ElapsedGameTime.TotalSeconds;
                    Scale /= 1.010f;
                }

                if (basePosition.Y <= collectedPosition.Y)
                {
                    State = GemState.Collected;
                }

                break;

            case GemState.Waiting:
                // Bounce along a sine curve over time.
                // Include the X coordinate so that neighboring gems bounce in a nice wave pattern.
                var t = gameTime.TotalGameTime.TotalSeconds * BounceRate + Position.X * BounceSync;
                bounce = (float)Math.Sin(t) * BounceHeight * texture.Height;
                Scale = new Vector2(1.0f, 1.0f);
                break;
        }
    }

    /// <summary>
    ///     Called when this gem has been collected by a player and removed from the level.
    /// </summary>
    /// <param name="collectedBy">
    ///     The player who collected this gem. Although currently not used, this parameter would be
    ///     useful for creating special power-up gems. For example, a gem could make the player invincible.
    /// </param>
    public void OnCollected(Player collectedBy)
    {
        collectedSound.Play();

        if (IsPowerUp)
        {
            collectedBy.PowerUp();
        }
    }

    /// <summary>
    ///     Draws a gem in the appropriate color.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    /// <param name="spriteBatch">The SpriteBatch used to draw the gem.</param>
    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        spriteBatch.Draw(texture, Position, null, Color, 0.0f, origin, Scale, SpriteEffects.None, 0.0f);
    }
}

/// <summary>
///     The various states the gem could be in.
/// </summary>
internal enum GemState
{
    Collected,
    Collecting,
    Waiting
}