using System;
using System.Diagnostics;

namespace Cogito.Core.Settings;

/// <summary>
///     Manages application settings using a specified storage implementation.
/// </summary>
/// <typeparam name="T">The type representing the settings data. Must have a parameterless constructor.</typeparam>
internal class SettingsManager<T> where T : new()
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SettingsManager{T}" /> class.
    /// </summary>
    /// <param name="storage">The storage implementation to handle saving and loading settings.</param>
    public SettingsManager(ISettingsStorage storage)
    {
        Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        Load();
    }

    /// <summary>
    ///     Provides access to the underlying settings storage mechanism.
    /// </summary>
    public ISettingsStorage Storage { get; }

    /// <summary>
    ///     Provides access to the currently loaded settings. Will never be null.
    /// </summary>
    public T Settings { get; private set; }

    /// <summary>
    ///     Occurs when settings are successfully loaded.
    /// </summary>
    public event Action<T> SettingsLoaded;

    /// <summary>
    ///     Occurs when settings are successfully saved.
    /// </summary>
    public event Action<T> SettingsSaved;

    /// <summary>
    ///     Saves the current settings to the specified storage.
    /// </summary>
    public void Save()
    {
        try
        {
            Storage.SaveSettings(Settings);
            SettingsSaved?.Invoke(Settings);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save settings: {ex}");
        }
    }

    /// <summary>
    ///     Loads settings from the specified storage. If loading fails, initializes with default values.
    /// </summary>
    public void Load()
    {
        try
        {
            Settings = Storage.LoadSettings<T>() ?? new T();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load settings, initializing defaults: {ex}");
            Settings = new T();
        }

        SettingsLoaded?.Invoke(Settings);
    }
}