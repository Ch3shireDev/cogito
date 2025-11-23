using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cogito.Core.Effects;
using Cogito.Core.Inputs;
using Cogito.Core.Localization;
using Cogito.Core.ScreenManagers;
using Cogito.Core.Screens;
using Cogito.Core.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Cogito.Core.Game;

/// <summary>
///     A uniform grid of tiles with collections of gems and enemies.
///     The level owns the player and controls the game's win and lose
///     conditions as well as scoring.
/// </summary>
internal class Level : IDisposable
{
    // The layer which entities are drawn on top of.
    private const int EntityLayer = 2;

    private const int PointsPerSecond = 5;

    // The number of levels in the Levels directory of our content. We assume that
    // levels in our content are 0-based and that all numbers under this constant
    // have a level file present. This allows us to not need to check for the file
    // or handle exceptions, both of which can add unnecessary time to level loading.
    public const int NUMBER_OF_LEVELS = 5;
    private const int NUMBER_OF_LAYERS = 3;

    private const float ViewMargin = 0.35f;

    private static readonly Point InvalidPosition = new(-1, -1);

    // When the time remaining is less than the warning time, it blinks on the hud
    private static readonly TimeSpan WarningTime = TimeSpan.FromSeconds(30);

    // Backpack related variables
    private readonly Texture2D backpack;

    private readonly List<Enemy> enemies = [];

    private readonly SoundEffect exitReachedSound;

    private readonly SpriteFont hudFont;
    private readonly Layer[] layers;
    private readonly bool onMainMenu;

    // Level game state.
    private readonly Random random = new(354668); // Arbitrary, but constant seed

    private readonly ScreenManager screenManager;
    private Vector2 backpackPosition;

    private float cameraPosition;
    private Vector2 collectionPoint;

    private string levelPath;

    private bool particlesExploding;

    private bool readyToPlay;
    private bool saved;

    private SettingsManager<CogitoLeaderboard> settingsManager;

    // Key locations in the level.        
    private Vector2 start;

    // Physical structure of the level.
    private Tile[,] tiles;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Level" /> class.
    /// </summary>
    /// <param name="screenManager">The screen manager for the game.</param>
    /// <param name="levelPath">The path to the level file.</param>
    /// <param name="levelIndex">The index of the level.</param>
    public Level(ScreenManager screenManager, string levelPath, int levelIndex)
    {
        this.screenManager = screenManager;

        // Create a new content manager to load content used just by this level.
        Content = new ContentManager(this.screenManager.Game.Services, "Content");

        TimeTaken = TimeSpan.Zero;
        this.levelPath = levelPath;

        // If it's the MainMenu/Tutorial level, ignore stats and giving it a score.
        onMainMenu = levelPath.Contains("00.txt");

        using (var fileStream = TitleContainer.OpenStream(levelPath))
        {
            LoadTiles(fileStream);
        }

        // Load background layer textures.
        layers = new Layer[3];

        var textures0 = new Texture2D[3];
        for (var i = 0; i < 3; ++i) textures0[i] = Content.Load<Texture2D>("Backgrounds/Layer0" + "_" + i);

        layers[0] = new Layer(textures0, 0.2f);

        var textures1 = new Texture2D[3];
        for (var i = 0; i < 3; ++i) textures1[i] = Content.Load<Texture2D>("Backgrounds/Layer1" + "_" + i);

        layers[1] = new Layer(textures1, 0.5f);

        var textures2 = new Texture2D[3];
        for (var i = 0; i < 3; ++i) textures2[i] = Content.Load<Texture2D>("Backgrounds/Layer2" + "_" + i);

        layers[2] = new Layer(textures2, 0.8f);

        // Load sounds.
        exitReachedSound = Content.Load<SoundEffect>("Sounds/PlayerExitReached");

        GemsCount = Gems.Count;

        // Load font
        hudFont = Content.Load<SpriteFont>("Fonts/Hud");

        // Our backpack to store the collected gems :) 
        backpack = Content.Load<Texture2D>("Sprites/backpack");

        // Hook into the GemCollected event
        GemCollected += Level_GemCollected;
    }

    /// <summary>
    ///     Gets the player instance in the level.
    /// </summary>
    public Player Player { get; private set; }

    /// <summary>
    ///     Gets or sets the list of gems in the level.
    /// </summary>
    internal List<Gem> Gems { get; set; } = new();

    /// <summary>
    ///     Gets or sets the exit position of the level.
    /// </summary>
    internal Point Exit { get; set; } = InvalidPosition;

    /// <summary>
    ///     Gets the current score of the level.
    /// </summary>
    public int Score { get; private set; }

    /// <summary>
    ///     Gets whether the player has reached the exit.
    /// </summary>
    public bool ReachedExit { get; private set; }

    /// <summary>
    ///     Gets the time taken to complete the level.
    /// </summary>
    public TimeSpan TimeTaken { get; private set; }

    /// <summary>
    ///     Gets the maximum time allowed to complete the level.
    /// </summary>
    public TimeSpan MaximumTimeToCompleteLevel { get; } = TimeSpan.FromMinutes(2.0);

    /// <summary>
    ///     Gets the number of gems collected by the player.
    /// </summary>
    public int GemsCollected { get; private set; }

    /// <summary>
    ///     Gets the total number of gems in the level.
    /// </summary>
    public int GemsCount { get; }

    /// <summary>
    ///     Gets whether a new high score has been achieved.
    /// </summary>
    public bool NewHighScore { get; private set; }

    /// <summary>
    ///     Gets the content manager for the level.
    /// </summary>
    public ContentManager Content { get; }

    /// <summary>
    ///     Gets the width of the level measured in tiles.
    /// </summary>
    public int Width => tiles.GetLength(0);

    /// <summary>
    ///     Gets the height of the level measured in tiles.
    /// </summary>
    public int Height => tiles.GetLength(1);

    /// <summary>
    ///     Gets or sets the particle manager for the level.
    /// </summary>
    public ParticleManager ParticleManager { get; set; }

    /// <summary>
    ///     Gets the position of the backpack in the level.
    /// </summary>
    public Vector2 BackpackPosition => backpackPosition;

    /// <summary>
    ///     Gets or sets the leaderboard manager for the level.
    /// </summary>
    public SettingsManager<CogitoLeaderboard> LeaderboardManager
    {
        get => settingsManager;

        set
        {
            if (value != null
                && settingsManager != value)
            {
                settingsManager = value;
                settingsManager.Load();
            }
        }
    }

    /// <summary>
    ///     Gets or sets whether the level is paused.
    /// </summary>
    public bool Paused { get; internal set; }

    /// <summary>
    ///     Unloads the level content.
    /// </summary>
    public void Dispose()
    {
        Content.Unload();
    }

    /// <summary>
    ///     Event triggered when a gem is collected by the player.
    /// </summary>
    public event EventHandler<(Gem, Player)> GemCollected;

    private void Level_GemCollected(object sender, (Gem gem, Player collectedBy) e)
    {
        Score += e.gem.Value;

        e.gem.OnCollected(e.collectedBy);
    }

    /// <summary>
    ///     Iterates over every tile in the structure file and loads its
    ///     appearance and behavior. This method also validates that the
    ///     file is well-formed with a player start point, exit, etc.
    /// </summary>
    /// <param name="fileStream">
    ///     A stream containing the tile data.
    /// </param>
    private void LoadTiles(Stream fileStream)
    {
        // Load the level and ensure all the lines are the same length.
        int width;
        var lines = new List<string>();
        using (var reader = new StreamReader(fileStream))
        {
            var line = reader.ReadLine();
            width = line.Length;
            while (line != null)
            {
                lines.Add(line);
                if (line.Length != width)
                {
                    throw new Exception(string.Format(Resources.ErrorLevelLineLength, lines.Count));
                }

                line = reader.ReadLine();
            }
        }

        // Allocate the tile grid.
        tiles = new Tile[width, lines.Count];

        // Loop over every tile position,
        for (var y = 0; y < Height; ++y)
        for (var x = 0; x < Width; ++x)
        {
            // to load each tile.
            var tileType = lines[y][x];
            tiles[x, y] = LoadTile(tileType, x, y);
        }

        // Verify that the level has a beginning and an end.
        if (Player == null)
        {
            throw new NotSupportedException(Resources.ErrorLevelStartingPoint);
        }

        if (Exit == InvalidPosition)
        {
            throw new NotSupportedException(Resources.ErrorLevelExit);
        }
    }

    /// <summary>
    ///     Loads an individual tile's appearance and behavior.
    /// </summary>
    /// <param name="tileType">
    ///     The character loaded from the structure file which
    ///     indicates what should be loaded.
    /// </param>
    /// <param name="x">
    ///     The X location of this tile in tile space.
    /// </param>
    /// <param name="y">
    ///     The Y location of this tile in tile space.
    /// </param>
    /// <returns>The loaded tile.</returns>
    private Tile LoadTile(char tileType, int x, int y)
    {
        return tileType switch
        {
            // Blank space
            '.' => new Tile(null, TileCollision.Passable),
            // Exit
            'X' => LoadExitTile(x, y),
            // Minimal value Gem
            // Medium value Gem
            // Maximum value Gem
            // PowerUp Gem
            '1' or '2' or '3' or '4' => LoadGemTile(x, y, tileType),
            // Floating platform
            '-' => LoadTile("Platform", TileCollision.Platform),
            // Various enemy types
            'A' or 'B' or 'C' or 'D' => LoadEnemyTile(x, y, tileType),
            // Platform block
            '~' => LoadVarietyTile("BlockB", 2, TileCollision.Platform),
            // Passable block
            ':' => LoadVarietyTile("BlockB", 2, TileCollision.Passable),
            // Impassable block
            '#' => LoadVarietyTile("BlockA", 7, TileCollision.Impassable),
            // Breakable block
            ';' => LoadVarietyTile("BlockB", 2, TileCollision.Breakable),
            // Player 1 start point
            'P' => LoadStartTile(x, y),
            _ => throw new NotSupportedException(string.Format(Resources.ErrorUnsupportedTileType, tileType, x, y))
        };
    }

    /// <summary>
    ///     Creates a new tile. The other tile loading methods typically chain to this
    ///     method after performing their special logic.
    /// </summary>
    /// <param name="name">
    ///     Path to a tile texture relative to the Content/Tiles directory.
    /// </param>
    /// <param name="collision">
    ///     The tile collision type for the new tile.
    /// </param>
    /// <returns>The new tile.</returns>
    private Tile LoadTile(string name, TileCollision collision)
    {
        return new Tile(Content.Load<Texture2D>("Tiles/" + name), collision);
    }

    /// <summary>
    ///     Loads a tile with a random appearance.
    /// </summary>
    /// <param name="baseName">
    ///     The content name prefix for this group of tile variations. Tile groups are
    ///     name LikeThis0.png and LikeThis1.png and LikeThis2.png.
    /// </param>
    /// <param name="variationCount">
    ///     The number of variations in this group.
    /// </param>
    /// <param name="collision"></param>
    private Tile LoadVarietyTile(string baseName, int variationCount, TileCollision collision)
    {
        var index = random.Next(variationCount);
        return LoadTile(baseName + index, collision);
    }

    /// <summary>
    ///     Instantiates a player, puts him in the level, and remembers where to put him when he is resurrected.
    /// </summary>
    private Tile LoadStartTile(int x, int y)
    {
        if (Player != null)
        {
            throw new NotSupportedException(Resources.ErrorLevelOneStartingPoint);
        }

        start = GetBounds(x, y).GetBottomCenter();
        Player = new Player(this, start);
        Player.Mode = PlayerMode.Playing;

        return new Tile(null, TileCollision.Passable);
    }

    /// <summary>
    ///     Remembers the location of the level's exit.
    /// </summary>
    private Tile LoadExitTile(int x, int y)
    {
        if (Exit != InvalidPosition)
        {
            throw new NotSupportedException(Resources.ErrorLevelOneExit);
        }

        Exit = GetBounds(x, y).Center;

        return LoadTile("Exit", TileCollision.Passable);
    }

    /// <summary>
    ///     Instantiates an enemy and puts him in the level.
    /// </summary>
    private Tile LoadEnemyTile(int x, int y, char monsterType)
    {
        var position = GetBounds(x, y).GetBottomCenter();
        enemies.Add(new Enemy(this, position, "Monster" + monsterType));

        return new Tile(null, TileCollision.Passable);
    }

    /// <summary>
    ///     Instantiates a gem and puts it in the level.
    /// </summary>
    private Tile LoadGemTile(int x, int y, char gemType)
    {
        var position = GetBounds(x, y).Center;
        Gems.Add(new Gem(this, new Vector2(position.X, position.Y), gemType, new Vector2(Width * Tile.Width, Height * Tile.Height)));

        return new Tile(null, TileCollision.Passable);
    }

    /// <summary>
    ///     Gets the collision mode of the tile at a particular location.
    ///     This method handles tiles outside the levels boundaries by making it
    ///     impossible to escape past the left or right edges, but allowing things
    ///     to jump beyond the top of the level and fall off the bottom.
    /// </summary>
    public TileCollision GetCollision(int x, int y)
    {
        // Prevent escaping past the level ends.
        if (x < 0 || x >= Width)
        {
            return TileCollision.Impassable;
        }

        // Allow jumping past the level top and falling through the bottom.
        if (y < 0 || y >= Height)
        {
            return TileCollision.Passable;
        }

        return tiles[x, y].Collision;
    }

    /// <summary>
    ///     Gets the bounding rectangle of a tile in world space.
    /// </summary>
    public Rectangle GetBounds(int x, int y)
    {
        return new Rectangle(x * Tile.Width, y * Tile.Height, Tile.Width, Tile.Height);
    }

    /// <summary>
    ///     Updates all objects in the world, performs collision between them,
    ///     and handles the time limit with scoring.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    /// <param name="inputState">Provides a snapshot of input states.</param>
    /// <param name="displayOrientation">Provides the current display orientation.</param>
    /// <param name="readyToPlay">Indicates whether the level is ready to be played.</param>
    public void Update(GameTime gameTime, InputState inputState, DisplayOrientation displayOrientation, bool readyToPlay = true)
    {
        if (gameTime == null)
        {
            throw new ArgumentNullException(nameof(gameTime));
        }

        if (inputState == null)
        {
            throw new ArgumentNullException(nameof(inputState));
        }

        this.readyToPlay = readyToPlay;
        ParticleManager.Update(gameTime);

        if (ReachedExit && !particlesExploding)
        {
            ParticleManager.Position = Player.Position;
            ParticleManager.Emit(100, SettingsScreen.CurrentParticleEffect);
            particlesExploding = true;
        }

        if (ReachedExit)
        {
            if (onMainMenu)
            {
                return;
            }

            if (!saved)
            {
                // We only flag a high score, if it's a faster time and all gems were collected.
                if (TimeTaken < settingsManager.Settings.FastestTime
                    && GemsCollected == GemsCount)
                {
                    NewHighScore = true;
                }

                if (NewHighScore)
                {
                    // If it already exists update it, otherwise add it
                    if (settingsManager.Settings.FastestTime != TimeTaken)
                    {
                        settingsManager.Settings.FastestTime = TimeTaken;
                    }

                    if (settingsManager.Settings.GemsCollected < GemsCollected)
                    {
                        settingsManager.Settings.GemsCollected = GemsCollected;
                    }

                    if (!saved)
                    {
                        settingsManager.Save();
                        saved = true;
                    }
                }
            }

            // Animate the time being converted into points.
            var seconds = (int)Math.Round(gameTime.ElapsedGameTime.TotalSeconds * 100.0f);
            seconds = Math.Min(seconds, (int)Math.Ceiling(TimeTaken.TotalSeconds));
            TimeTaken += TimeSpan.FromSeconds(seconds);
            Score += seconds * PointsPerSecond;
        }
        else
        {
            UpdateGems(gameTime);

            if (readyToPlay)
            {
                TimeTaken += gameTime.ElapsedGameTime;

                Player.Update(gameTime, inputState, displayOrientation);

                // Parallax Scroll if necessary
                UpdateCamera(screenManager.BaseScreenSize);

                UpdateEnemies(gameTime);

                // The player has reached the exit if they are standing on the ground and
                // his bounding rectangle contains the center of the exit tile. They can only
                // exit when they have collected all the gems.
                if (Player.IsAlive && Player.IsOnGround && Player.BoundingRectangle.Contains(Exit))
                {
                    OnExitReached();
                }
            }
        }

        if (TimeTaken > MaximumTimeToCompleteLevel)
        {
            TimeTaken = MaximumTimeToCompleteLevel;
        }
    }

    /// <summary>
    ///     Animates each gem and checks to allows the player to collect them.
    /// </summary>
    private void UpdateGems(GameTime gameTime)
    {
        // We don't recreate a new Vector2 object each frame, we just update it
        // Calculate the collectionPoint relative to the current camera view
        // This will help the gems track the backpack, as the camera moves.
        // Like a homing missile :)
        collectionPoint.X = cameraPosition + backpackPosition.X + backpack.Width / 2;
        collectionPoint.Y = backpackPosition.Y + backpack.Height / 2;

        for (var i = 0; i < Gems.Count; ++i)
        {
            var gem = Gems[i];

            gem.Update(gameTime, collectionPoint);

            switch (gem.State)
            {
                case GemState.Collected:
                    Gems.RemoveAt(i--);
                    break;

                case GemState.Collecting:
                    break;

                case GemState.Waiting:
                    if (gem.BoundingCircle.Intersects(Player.BoundingRectangle))
                    {
                        GemsCollected++;
                        gem.Scale = new Vector2(1.5f, 1.5f);
                        gem.State = GemState.Collecting;
                        OnGemCollected(gem, Player);
                    }

                    break;
            }
        }
    }

    /// <summary>
    ///     Animates each enemy and allow them to kill the player.
    /// </summary>
    private void UpdateEnemies(GameTime gameTime)
    {
        foreach (var enemy in enemies)
        {
            enemy.Update(gameTime);

            if (!enemy.IsAlive || !enemy.BoundingRectangle.Intersects(Player.BoundingRectangle))
            {
                continue;
            }

            // Touching an enemy while having the power-up kills the enemy
            if (Player.IsPoweredUp)
            {
                OnEnemyKilled(enemy, Player);
            }
            // Touching an enemy instantly kills the player
            else
            {
                OnPlayerKilled(enemy);
            }
        }
    }

    /// <summary>
    ///     Called when a gem is collected.
    /// </summary>
    /// <param name="gem">The gem that was collected.</param>
    /// <param name="collectedBy">The player who collected this gem.</param>
    private void OnGemCollected(Gem gem, Player collectedBy)
    {
        // Call any associated events
        GemCollected?.Invoke(this, new ValueTuple<Gem, Player>(gem, collectedBy));
    }

    /// <summary>
    ///     Called when the player is killed.
    /// </summary>
    /// <param name="killedBy">
    ///     The enemy who killed the player. This is null if the player was not killed by an
    ///     enemy, such as when a player falls into a hole.
    /// </param>
    private void OnPlayerKilled(Enemy killedBy)
    {
        Player.OnKilled(killedBy);
    }

    /// <summary>
    ///     Called when the enemy is killed.
    /// </summary>
    /// <param name="enemy">
    ///     The enemy who died.
    /// </param>
    /// <param name="killedBy">
    ///     The player who killed the enemy. Could be used when we have extra players
    /// </param>
    private void OnEnemyKilled(Enemy enemy, Player killedBy)
    {
        enemy.OnKilled(killedBy);
    }

    /// <summary>
    ///     Called when the player reaches the level's exit.
    /// </summary>
    private void OnExitReached()
    {
        Player.OnReachedExit();
        exitReachedSound.Play();
        ReachedExit = true;
    }

    /// <summary>
    ///     Restores the player to the starting point to try the level again.
    /// </summary>
    public void StartNewLife()
    {
        Player.Reset(start);
    }

    /// <summary>
    ///     Draws everything in the level, including background layers, tiles, entities (gems, player, enemies),
    ///     foreground layers, and the HUD. This method ensures that all elements are rendered in the correct order
    ///     and with the appropriate transformations (e.g., parallax scrolling for background layers).
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values, used for animations and time-based effects.</param>
    /// <param name="spriteBatch">The SpriteBatch used to draw the level elements.</param>
    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        // Create a camera transformation matrix to simulate parallax scrolling.
        var cameraTransform = Matrix.CreateTranslation(-cameraPosition, 0.0f, 0.0f);

        // Get the global transformation scale for consistent rendering across resolutions.
        var transformScale = screenManager.GlobalTransformation.M11;

        // Draw background layers (layers behind entities).
        for (var i = 0; i <= EntityLayer; ++i)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, screenManager.GlobalTransformation);
            layers[i].Draw(gameTime, spriteBatch, cameraPosition / transformScale);
            spriteBatch.End();
        }

        // Draw main game elements (tiles, gems, player, enemies) with camera transformation.
        spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, cameraTransform * screenManager.GlobalTransformation);

        DrawTiles(spriteBatch);

        var cameraRight = cameraPosition + screenManager.BaseScreenSize.X;

        // Draw visible gems.
        foreach (var gem in Gems.Where(gem => IsInView(gem.Position.X, cameraPosition, cameraRight))) gem.Draw(gameTime, spriteBatch);

        // Draw the player.
        Player.Draw(gameTime, spriteBatch);

        // Draw visible enemies.
        foreach (var enemy in enemies.Where(enemy => IsInView(enemy.Position.X, cameraPosition, cameraRight))) enemy.Draw(gameTime, spriteBatch);

        spriteBatch.End();

        // Draw foreground layers (layers in front of entities).
        for (var i = EntityLayer + 1; i < layers.Length; ++i)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, screenManager.GlobalTransformation);
            layers[i].Draw(gameTime, spriteBatch, cameraPosition / transformScale);
            spriteBatch.End();
        }

        // Draw the HUD (time, score, backpack, etc.).
        spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, screenManager.GlobalTransformation);

        ParticleManager.Draw(spriteBatch);
        DrawHud(spriteBatch);

        spriteBatch.End();
    }

    /// <summary>
    ///     Determines whether a given position is within the visible area of the camera.
    /// </summary>
    /// <param name="positionX">The X-coordinate of the position to check.</param>
    /// <param name="cameraLeft">The left edge of the camera's view.</param>
    /// <param name="cameraRight">The right edge of the camera's view.</param>
    /// <returns>True if the position is within the camera's view, otherwise false.</returns>
    private bool IsInView(float positionX, float cameraLeft, float cameraRight)
    {
        return positionX >= cameraLeft - Tile.Width && positionX <= cameraRight + Tile.Width;
    }

    /// <summary>
    ///     Draws all visible tiles in the level. This method calculates the range of tiles currently
    ///     visible within the camera's view and renders them.
    /// </summary>
    /// <param name="spriteBatch">The SpriteBatch used to draw the tiles.</param>
    private void DrawTiles(SpriteBatch spriteBatch)
    {
        // Calculate the visible range of tiles based on the camera's position.
        var left = (int)Math.Floor(cameraPosition / Tile.Width);
        var right = (int)(left + screenManager.BaseScreenSize.X / Tile.Width);
        right = Math.Min(right, Width - 1);

        // Reuse a single Vector2 object for tile positions to reduce memory allocations.
        var position = new Vector2();

        // Loop through each tile position within the visible range.
        for (var y = 0; y < Height; ++y)
        for (var x = left; x <= right; ++x)
        {
            // If the tile has a texture, draw it at its calculated screen position.
            var texture = tiles[x, y].Texture;
            if (texture == null)
            {
                continue;
            }

            position.X = x * Tile.Size.X;
            position.Y = y * Tile.Size.Y;

            spriteBatch.Draw(texture, position, Color.White);
        }
    }

    /// <summary>
    ///     Breaks a tile at the specified position, removing it from the level and triggering
    ///     a particle effect to simulate its destruction.
    /// </summary>
    /// <param name="x">The X-coordinate of the tile in tile space.</param>
    /// <param name="y">The Y-coordinate of the tile in tile space.</param>
    internal void BreakTile(int x, int y)
    {
        RemoveTile(x, y);

        // Use Particle effect to explode the removed tile, above the player's head
        ParticleManager.Position = new Vector2(Player.Position.X, Player.Position.Y - 20);
        ParticleManager.Emit(50, ParticleEffectType.Confetti, Color.SandyBrown);
    }

    /// <summary>
    ///     Removes a tile from the level by making it passable and removing its texture.
    ///     This effectively makes the tile "disappear" from the game world.
    ///     Thus making the level layout appear dynamic.
    /// </summary>
    /// <param name="x">The X-coordinate of the tile in tile space.</param>
    /// <param name="y">The Y-coordinate of the tile in tile space.</param>
    internal void RemoveTile(int x, int y)
    {
        // Replace the tile with a passable, textureless tile.
        tiles[x, y] = new Tile(null, TileCollision.Passable);
    }

    /// <summary>
    ///     Draws the Heads-Up Display (HUD), including the time remaining, score, and backpack.
    ///     The HUD is drawn in screen space and is not affected by the camera's position.
    /// </summary>
    /// <param name="spriteBatch">The SpriteBatch used to draw the HUD elements.</param>
    private void DrawHud(SpriteBatch spriteBatch)
    {
        // Only draw the full HUD if the level is ready to play.
        if (readyToPlay)
        {
            // Draw the time taken in the format "MM:SS".
            var drawableString = Resources.Time +
                                 TimeTaken.Minutes.ToString("00") + ":" +
                                 TimeTaken.Seconds.ToString("00");
            var timeColor = TimeTaken < MaximumTimeToCompleteLevel - WarningTime
                            || ReachedExit
                            || (int)TimeTaken.TotalSeconds % 2 == 0
                ? Color.Yellow
                : Color.Red;

            DrawShadowedString(spriteBatch, hudFont, drawableString,
                new Vector2(20, 20),
                timeColor);

            // Draw the score in the top-right corner of the screen.
            drawableString = Resources.Score + Score;
            var scoreDimensions = hudFont.MeasureString(drawableString);
            var scorePosition = new Vector2(
                screenManager.BaseScreenSize.X - scoreDimensions.X - 20,
                20
            );

            DrawShadowedString(spriteBatch, hudFont, drawableString, scorePosition, Color.Yellow);
        }

        // Draw the backpack in the center-top of the screen.
        backpackPosition = new Vector2((screenManager.BaseScreenSize.X - backpack.Width) / 2, 20);

        spriteBatch.Draw(backpack, backpackPosition, Color.White);
    }

    /// <summary>
    ///     Draws a string with a shadow effect, making it more readable against varying backgrounds.
    /// </summary>
    /// <param name="spriteBatch">The SpriteBatch used to draw the string.</param>
    /// <param name="font">The font to use for rendering the string.</param>
    /// <param name="value">The string to draw.</param>
    /// <param name="position">The position at which to draw the string.</param>
    /// <param name="color">The color of the string.</param>
    private void DrawShadowedString(SpriteBatch spriteBatch, SpriteFont font, string value, Vector2 position, Color color)
    {
        // Draw the shadow slightly offset from the main text.
        spriteBatch.DrawString(font, value, position + new Vector2(1.0f, 1.0f), Color.Black);
        // Draw the main text.
        spriteBatch.DrawString(font, value, position, color);
    }

    /// <summary>
    ///     Updates the camera's position based on the player's movement, ensuring the camera
    ///     stays centered on the player while preventing it from scrolling outside the level bounds.
    /// </summary>
    /// <param name="screenSize">The dimensions of the BaseScreenSize.</param>
    private void UpdateCamera(Vector2 screenSize)
    {
        if (!readyToPlay || Player == null)
        {
            return;
        }

        // Calculate the edges of the screen based on the view margin.
        var marginWidth = screenSize.X * ViewMargin;
        var marginLeft = cameraPosition + marginWidth;
        var marginRight = cameraPosition + screenSize.X - marginWidth;

        // Calculate how far to scroll the camera when the player approaches the screen edges.
        var cameraMovement = 0.0f;
        if (Player.Position.X < marginLeft)
        {
            cameraMovement = Player.Position.X - marginLeft;
        }
        else if (Player.Position.X > marginRight)
        {
            cameraMovement = Player.Position.X - marginRight;
        }

        // Update the camera position, ensuring it stays within the level bounds.
        var maxCameraPosition = Tile.Width * Width - screenSize.X;
        cameraPosition = MathHelper.Clamp(cameraPosition + cameraMovement, 0.0f, maxCameraPosition);
    }
}