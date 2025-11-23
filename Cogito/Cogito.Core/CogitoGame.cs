using System;
using System.Linq;
using Cogito.Core.Effects;
using Cogito.Core.Localization;
using Cogito.Core.ScreenManagers;
using Cogito.Core.Screens;
using Cogito.Core.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Cogito.Core;

/// <summary>
///     The main class for the game, responsible for managing game components, settings,
///     and platform-specific configurations.
/// </summary>
/// <remarks>
///     This class is the entry point for the game and handles initialization, content loading,
///     and screen management.
/// </remarks>
/// }
public class CogitoGame : Microsoft.Xna.Framework.Game
{
    /// <summary>
    ///     Indicates if the game is running on a mobile platform.
    /// </summary>
    public static readonly bool IsMobile = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

    /// <summary>
    ///     Indicates if the game is running on a desktop platform.
    /// </summary>
    public static readonly bool IsDesktop = OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsWindows();

    // Resources for drawing.
    private readonly GraphicsDeviceManager graphicsDeviceManager;

    // Manages leaderboard data for tracking high scores and achievements.
    private readonly SettingsManager<CogitoLeaderboard> leaderboardManager;

    // Manages the game's screen transitions and screens.
    private readonly ScreenManager screenManager;

    // Manages game settings, such as preferences and configurations.
    private readonly SettingsManager<CogitoSettings> settingsManager;

    // Manages particle effects in the game.
    private ParticleManager particleManager;

    // Texture for rendering particles.
    private Texture2D particleTexture;

    /// <summary>
    ///     Initializes a new instance of the game. Configures platform-specific settings,
    ///     initializes services like settings and leaderboard managers, and sets up the
    ///     screen manager for screen transitions.
    /// </summary>
    public CogitoGame()
    {
        graphicsDeviceManager = new GraphicsDeviceManager(this);

        // Share GraphicsDeviceManager as a service.
        Services.AddService(graphicsDeviceManager);

        // Determine the appropriate settings storage based on the platform.
        ISettingsStorage storage;
        if (IsMobile)
        {
            storage = new MobileSettingsStorage();
            graphicsDeviceManager.IsFullScreen = true;
            IsMouseVisible = false;
        }
        else if (IsDesktop)
        {
            storage = new DesktopSettingsStorage();
            graphicsDeviceManager.IsFullScreen = false;
            IsMouseVisible = true;
        }
        else
        {
            throw new PlatformNotSupportedException();
        }

        // Initialize settings and leaderboard managers.
        settingsManager = new SettingsManager<CogitoSettings>(storage);
        Services.AddService(settingsManager);

        leaderboardManager = new SettingsManager<CogitoLeaderboard>(storage);
        Services.AddService(leaderboardManager);

        Content.RootDirectory = "Content";

        // Configure screen orientations.
        graphicsDeviceManager.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;

        // Initialize the screen manager.
        screenManager = new ScreenManager(this);
        Components.Add(screenManager);
    }

    /// <summary>
    ///     Initializes the game, including setting up localization and adding the
    ///     initial screens to the ScreenManager.
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();

        // Load supported languages and set the default language.
        var cultures = LocalizationManager.GetSupportedCultures();
        var languages = cultures.ToList();

        var selectedLanguage = languages[settingsManager.Settings.Language].Name;
        LocalizationManager.SetCulture(selectedLanguage);

        // Add background and main menu screens.
        screenManager.AddScreen(new BackgroundScreen(), null);
        screenManager.AddScreen(new MainMenuScreen(), null);
    }

    /// <summary>
    ///     Loads game content, such as textures and particle systems.
    /// </summary>
    protected override void LoadContent()
    {
        base.LoadContent();

        // Load a texture for particles and initialize the particle manager.
        particleTexture = Content.Load<Texture2D>("Sprites/blank");
        particleManager = new ParticleManager(particleTexture, new Vector2(400, 200));

        // Share the particle manager as a service.
        Services.AddService(particleManager);
    }
}