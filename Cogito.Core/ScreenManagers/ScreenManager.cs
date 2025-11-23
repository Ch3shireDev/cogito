using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cogito.Core.Inputs;
using Cogito.Core.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace Cogito.Core.ScreenManagers;

/// <summary>
///     The ScreenManager is a component responsible for managing multiple <see cref="GameScreen" /> instances.
///     It maintains a stack of screens, invokes their Update and Draw methods, and automatically routes input
///     to the topmost active screen.
/// </summary>
public class ScreenManager : DrawableGameComponent
{
    // Manages player input.
    private readonly InputState inputState = new();

    // List of active screens and screens pending update.
    private readonly List<GameScreen> screens = [];
    private readonly List<GameScreen> screensToUpdate = [];

    private Vector2 baseScreenSize = new(800, 480);
    private Texture2D blankTexture;

    private bool isInitialized;

    // Shared resources for drawing and content management.

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScreenManager" /> class.
    /// </summary>
    /// <param name="game">The associated Game instance.</param>
    public ScreenManager(Microsoft.Xna.Framework.Game game) : base(game)
    {
        TouchPanel.EnabledGestures = GestureType.None;
    }

    /// <summary>Gets or sets the current backbuffer width.</summary>
    public int BackbufferWidth { get; set; }

    /// <summary>Gets or sets the current backbuffer height.</summary>
    public int BackbufferHeight { get; set; }

    /// <summary>Gets or sets the base screen size used for scaling calculations.</summary>
    public Vector2 BaseScreenSize
    {
        get => baseScreenSize;
        set => baseScreenSize = value;
    }

    /// <summary>Gets or sets the global transformation matrix for scaling and positioning.</summary>
    public Matrix GlobalTransformation { get; set; }

    /// <summary>
    ///     Provides access to a shared SpriteBatch instance for drawing operations.
    /// </summary>
    public SpriteBatch SpriteBatch { get; private set; }

    /// <summary>
    ///     Provides access to a shared SpriteFont instance for text rendering.
    /// </summary>
    public SpriteFont Font { get; private set; }

    /// <summary>
    ///     Enables or disables screen tracing for debugging purposes.
    ///     When enabled, the manager prints a list of active screens during updates.
    /// </summary>
    public bool TraceEnabled { get; set; }

    /// <summary>
    ///     Initializes the ScreenManager and any required services.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();
        isInitialized = true;
    }

    /// <summary>
    ///     Loads graphical content for the ScreenManager and all active screens.
    /// </summary>
    protected override void LoadContent()
    {
        var content = Game.Content;
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        Font = content.Load<SpriteFont>("Fonts/Hud");
        blankTexture = content.Load<Texture2D>("Sprites/blank");

        foreach (var screen in screens) screen.LoadContent();
    }

    /// <summary>
    ///     Unloads graphical content for all screens.
    /// </summary>
    protected override void UnloadContent()
    {
        foreach (var screen in screens) screen.UnloadContent();
    }

    /// <summary>
    ///     Updates the active screens and processes input.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of the game's timing state.</param>
    public override void Update(GameTime gameTime)
    {
        inputState.Update(gameTime, BaseScreenSize);
        screensToUpdate.Clear();
        screensToUpdate.AddRange(screens);

        var otherScreenHasFocus = !Game.IsActive;
        var coveredByOtherScreen = false;

        while (screensToUpdate.Count > 0)
        {
            var screen = screensToUpdate[^1];
            screensToUpdate.RemoveAt(screensToUpdate.Count - 1);

            screen.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            if (screen.ScreenState != ScreenState.TransitionOn && screen.ScreenState != ScreenState.Active)
            {
                continue;
            }

            if (!otherScreenHasFocus)
            {
                screen.HandleInput(gameTime, inputState);
                otherScreenHasFocus = true;
            }

            if (!screen.IsPopup)
            {
                coveredByOtherScreen = true;
            }
        }

        if (TraceEnabled)
        {
            TraceScreens();
        }
    }

    /// <summary>
    ///     Prints active screen names to the debug console for diagnostic purposes.
    /// </summary>
    private void TraceScreens()
    {
        var screenNames = screens.Select(screen => screen.GetType().Name).ToList();
        Debug.WriteLine(string.Join(", ", screenNames));
    }

    /// <summary>
    ///     Draws the active screens.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of the game's timing state.</param>
    public override void Draw(GameTime gameTime)
    {
        foreach (var screen in screens)
            if (screen.ScreenState != ScreenState.Hidden)
            {
                screen.Draw(gameTime);
            }
    }

    /// <summary>
    ///     Releases resources used by the <see cref="ScreenManager" /> object.
    /// </summary>
    /// <param name="disposing">
    ///     True to release both managed and unmanaged resources; false to release only unmanaged resources.
    /// </param>
    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                // Dispose of managed resources.
                SpriteBatch?.Dispose();
            }
            // No unmanaged resources to dispose in this example.
        }
        finally
        {
            // Call the base class's Dispose method to ensure proper cleanup.
            base.Dispose(disposing);
        }
    }

    /// <summary>
    ///     Adds a new screen to the ScreenManager.
    /// </summary>
    /// <param name="screen">The screen to add.</param>
    /// <param name="controllingPlayer">The controlling player, if applicable.</param>
    public void AddScreen(GameScreen screen, PlayerIndex? controllingPlayer)
    {
        screen.ControllingPlayer = controllingPlayer;
        screen.ScreenManager = this;
        screen.IsExiting = false;

        if (isInitialized)
        {
            screen.LoadContent();
        }

        screens.Add(screen);
        TouchPanel.EnabledGestures = screen.EnabledGestures;
    }

    /// <summary>
    ///     Removes a screen from the ScreenManager.
    /// </summary>
    /// <param name="screen">The screen to remove.</param>
    public void RemoveScreen(GameScreen screen)
    {
        if (isInitialized)
        {
            screen.UnloadContent();
        }

        screens.Remove(screen);
        screensToUpdate.Remove(screen);

        if (screens.Count > 0)
        {
            TouchPanel.EnabledGestures = screens[^1].EnabledGestures;
        }
    }

    /// <summary>
    ///     Returns an array of all active screens managed by the ScreenManager.
    /// </summary>
    /// <returns>
    ///     An array containing all current GameScreen instances. This array is a copy
    ///     of the internal list to ensure screens are only added or removed using
    ///     <see cref="AddScreen(GameScreen, PlayerIndex?)" /> and
    ///     <see cref="RemoveScreen(GameScreen)" />.
    /// </returns>
    public GameScreen[] GetScreens()
    {
        return screens.ToArray();
    }

    /// <summary>
    ///     Draws a translucent black fullscreen sprite. This is used for fading
    ///     screens in and out, or for darkening the background behind popups.
    /// </summary>
    /// <param name="alpha">The opacity level of the fade (0 = fully transparent, 1 = fully opaque).</param>
    public void FadeBackBufferToBlack(float alpha)
    {
        SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, GlobalTransformation);

        SpriteBatch.Draw(blankTexture,
            new Rectangle(0, 0, (int)BaseScreenSize.X, (int)BaseScreenSize.Y),
            Color.Black * alpha);

        SpriteBatch.End();
    }

    /// <summary>
    ///     Scales the game presentation area to match the screen's aspect ratio.
    /// </summary>
    public void ScalePresentationArea()
    {
        // Validate parameters before calculation
        if (GraphicsDevice == null || baseScreenSize.X <= 0 || baseScreenSize.Y <= 0)
        {
            throw new InvalidOperationException("Invalid graphics configuration");
        }

        // Fetch screen dimensions
        BackbufferWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
        BackbufferHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;

        // Prevent division by zero
        if (BackbufferHeight == 0 || baseScreenSize.Y == 0)
        {
            return;
        }

        // Calculate aspect ratios
        var baseAspectRatio = baseScreenSize.X / baseScreenSize.Y;
        var screenAspectRatio = BackbufferWidth / (float)BackbufferHeight;

        // Determine uniform scaling factor
        float scalingFactor;
        float horizontalOffset = 0;
        float verticalOffset = 0;

        if (screenAspectRatio > baseAspectRatio)
        {
            // Wider screen: scale by height
            scalingFactor = BackbufferHeight / baseScreenSize.Y;

            // Centre things horizontally.
            horizontalOffset = (BackbufferWidth - baseScreenSize.X * scalingFactor) / 2;
        }
        else
        {
            // Taller screen: scale by width
            scalingFactor = BackbufferWidth / baseScreenSize.X;

            // Centre things vertically.
            verticalOffset = (BackbufferHeight - baseScreenSize.Y * scalingFactor) / 2;
        }

        // Update the transformation matrix
        GlobalTransformation = Matrix.CreateScale(scalingFactor) *
                               Matrix.CreateTranslation(horizontalOffset, verticalOffset, 0);

        // Update the inputTransformation with the Inverted globalTransformation
        inputState.UpdateInputTransformation(Matrix.Invert(GlobalTransformation));

        // Debug info
        Debug.WriteLine($"Screen Size - Width[{BackbufferWidth}] Height[{BackbufferHeight}] ScalingFactor[{scalingFactor}]");
    }
}