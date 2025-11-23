using System.Collections.Generic;
using System.Globalization;
using Cogito.Core.Effects;
using Cogito.Core.Localization;
using Cogito.Core.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Cogito.Core.Screens;

/// <summary>
///     The settings screen is brought up over the top of the main menu
///     screen, and gives the user a chance to configure the game
///     in various hopefully useful ways.
/// </summary>
internal class SettingsScreen : MenuScreen
{
    private static List<CultureInfo> languages;
    private static int currentLanguage;

    private readonly MenuEntry backMenuEntry;
    private readonly MenuEntry fullscreenMenuEntry;
    private readonly MenuEntry languageMenuEntry;
    private readonly MenuEntry particleEffectMenuEntry;

    private GraphicsDeviceManager gdm;
    private ParticleManager particleManager;

    private SettingsManager<CogitoSettings> settingsManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SettingsScreen" /> class.
    /// </summary>
    public SettingsScreen()
        : base(Resources.Settings)
    {
        var cultures = LocalizationManager.GetSupportedCultures();
        languages = cultures;

        // Create our menu entries.
        fullscreenMenuEntry = new MenuEntry(string.Empty);
        languageMenuEntry = new MenuEntry(string.Empty);
        particleEffectMenuEntry = new MenuEntry(string.Empty);
        backMenuEntry = new MenuEntry(string.Empty);

        // Hook up menu event handlers.
        fullscreenMenuEntry.Selected += FullScreenMenuEntrySelected;
        languageMenuEntry.Selected += LanguageMenuEntrySelected;
        particleEffectMenuEntry.Selected += ParticleEffectMenuEntrySelected;
        backMenuEntry.Selected += OnCancel;

        // Add entries to the menu.
        MenuEntries.Add(fullscreenMenuEntry);
        MenuEntries.Add(languageMenuEntry);
        MenuEntries.Add(particleEffectMenuEntry);
        MenuEntries.Add(backMenuEntry);
    }

    /// <summary>
    ///     Gets the currently selected particle effect type.
    /// </summary>
    public static ParticleEffectType CurrentParticleEffect { get; private set; } = ParticleEffectType.Fireworks;

    /// <summary>
    ///     Loads content for the settings screen, including lazy loading services and setting initial values.
    /// </summary>
    public override void LoadContent()
    {
        base.LoadContent();

        // Lazy Load some things
        gdm ??= ScreenManager.Game.Services.GetService<GraphicsDeviceManager>();

        settingsManager ??= ScreenManager.Game.Services.GetService<SettingsManager<CogitoSettings>>();

        settingsManager.Settings.PropertyChanged += (s, e) =>
        {
            SetLanguageText();

            settingsManager.Save();
        };

        currentLanguage = settingsManager.Settings.Language;
        CurrentParticleEffect = settingsManager.Settings.ParticleEffect;
        gdm.IsFullScreen = settingsManager.Settings.FullScreen;

        SetLanguageText();

        particleManager ??= ScreenManager.Game.Services.GetService<ParticleManager>();
    }

    /// <summary>
    ///     Updates the settings screen, including particle effects.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    /// <param name="otherScreenHasFocus">Indicates whether another screen has focus.</param>
    /// <param name="coveredByOtherScreen">Indicates whether the screen is covered by another screen.</param>
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

        particleManager.Update(gameTime);
    }

    /// <summary>
    ///     Draws the settings screen, including particle effects.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    public override void Draw(GameTime gameTime)
    {
        var spriteBatch = ScreenManager.SpriteBatch;

        spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, ScreenManager.GlobalTransformation);

        particleManager.Draw(spriteBatch);

        spriteBatch.End();

        base.Draw(gameTime);
    }

    /// <summary>
    ///     Fills in the latest values for the options screen menu text.
    /// </summary>
    private void SetLanguageText()
    {
        fullscreenMenuEntry.Text = string.Format(Resources.DisplayMode, gdm.IsFullScreen ? Resources.FullScreen : Resources.Windowed);

        var selectedLanguage = languages[currentLanguage].DisplayName;
        if (selectedLanguage.Contains("Invariant"))
        {
            selectedLanguage = Resources.English;
        }

        languageMenuEntry.Text = Resources.Language + selectedLanguage;

        particleEffectMenuEntry.Text = Resources.ParticleEffect + CurrentParticleEffect;

        backMenuEntry.Text = Resources.Back;

        Title = Resources.Settings;
    }

    /// <summary>
    ///     Event handler for when the Fullscreen menu entry is selected.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="PlayerIndexEventArgs" /> instance containing the event data.</param>
    private void FullScreenMenuEntrySelected(object sender, PlayerIndexEventArgs e)
    {
        gdm.ToggleFullScreen();

        settingsManager.Settings.FullScreen = gdm.IsFullScreen;
    }

    /// <summary>
    ///     Event handler for when the Language menu entry is selected.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="PlayerIndexEventArgs" /> instance containing the event data.</param>
    private void LanguageMenuEntrySelected(object sender, PlayerIndexEventArgs e)
    {
        currentLanguage = (currentLanguage + 1) % languages.Count;

        var selectedLanguage = languages[currentLanguage].Name;
        LocalizationManager.SetCulture(selectedLanguage);

        settingsManager.Settings.Language = currentLanguage;
    }

    /// <summary>
    ///     Event handler for when the Particle menu entry is selected.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="PlayerIndexEventArgs" /> instance containing the event data.</param>
    private void ParticleEffectMenuEntrySelected(object sender, PlayerIndexEventArgs e)
    {
        CurrentParticleEffect++;

        if (CurrentParticleEffect > ParticleEffectType.Sparkles)
        {
            CurrentParticleEffect = 0;
        }

        settingsManager.Settings.ParticleEffect = CurrentParticleEffect;

        particleManager.Emit(100, CurrentParticleEffect); // Emit 100 particles
    }
}