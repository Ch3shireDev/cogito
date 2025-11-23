using System;
using Cogito.Core.Inputs;
using Cogito.Core.ScreenManagers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;

namespace Cogito.Core.Screens;

/// <summary>
///     A screen is a single layer that has update and draw logic, and which
///     can be combined with other layers to build up a complex menu system.
///     For instance the main menu, the options menu, the "are you sure you
///     want to quit" message box, and the main game itself are all implemented
///     as screens.
/// </summary>
public abstract class GameScreen
{
    private GestureType enabledGestures = GestureType.None;

    private bool otherScreenHasFocus;

    /// <summary>
    ///     Normally when one screen is brought up over the top of another,
    ///     the first screen will transition off to make room for the new
    ///     one. This property indicates whether the screen is only a small
    ///     popup, in which case screens underneath it do not need to bother
    ///     transitioning off.
    /// </summary>
    public bool IsPopup { get; protected set; }

    /// <summary>
    ///     Indicates how long the screen takes to
    ///     transition on when it is activated.
    /// </summary>
    public TimeSpan TransitionOnTime { get; protected set; } = TimeSpan.Zero;

    /// <summary>
    ///     Indicates how long the screen takes to
    ///     transition off when it is deactivated.
    /// </summary>
    public TimeSpan TransitionOffTime { get; protected set; } = TimeSpan.Zero;

    /// <summary>
    ///     Gets the current position of the screen transition, ranging
    ///     from zero (fully active, no transition) to one (transitioned
    ///     fully off to nothing).
    /// </summary>
    public float TransitionPosition { get; protected set; } = 1;

    /// <summary>
    ///     Gets the current alpha of the screen transition, ranging
    ///     from 1 (fully active, no transition) to 0 (transitioned
    ///     fully off to nothing).
    /// </summary>
    public float TransitionAlpha => 1f - TransitionPosition;

    /// <summary>
    ///     Gets the current screen transition state.
    /// </summary>
    public ScreenState ScreenState { get; protected set; } = ScreenState.TransitionOn;

    /// <summary>
    ///     There are two possible reasons why a screen might be transitioning
    ///     off. It could be temporarily going away to make room for another
    ///     screen that is on top of it, or it could be going away for good.
    ///     This property indicates whether the screen is exiting for real:
    ///     if set, the screen will automatically remove itself as soon as the
    ///     transition finishes.
    /// </summary>
    public bool IsExiting { get; protected internal set; }

    /// <summary>
    ///     Checks whether this screen is active and can respond to user input.
    /// </summary>
    public bool IsActive =>
        !otherScreenHasFocus &&
        (ScreenState == ScreenState.TransitionOn ||
         ScreenState == ScreenState.Active);

    /// <summary>
    ///     Gets the manager that this screen belongs to.
    /// </summary>
    public ScreenManager ScreenManager { get; internal set; }

    /// <summary>
    ///     Gets the index of the player who is currently controlling this screen,
    ///     or null if it is accepting input from any player. This is used to lock
    ///     the game to a specific player profile. The main menu responds to input
    ///     from any connected gamepad, but whichever player makes a selection from
    ///     this menu is given control over all subsequent screens, so other gamepads
    ///     are inactive until the controlling player returns to the main menu.
    /// </summary>
    public PlayerIndex? ControllingPlayer { get; internal set; }

    /// <summary>
    ///     Gets the gestures the screen is interested in. Screens should be as specific
    ///     as possible with gestures to increase the accuracy of the gesture engine.
    ///     For example, most menus only need Tap or perhaps Tap and VerticalDrag to operate.
    ///     These gestures are handled by the ScreenManager when screens change and
    ///     all gestures are placed in the InputState passed to the HandleInput method.
    /// </summary>
    public GestureType EnabledGestures
    {
        get => enabledGestures;
        protected set
        {
            enabledGestures = value;

            // the screen manager handles this during screen changes, but
            // if this screen is active and the gesture types are changing,
            // we have to update the TouchPanel ourself.
            if (ScreenState == ScreenState.Active)
            {
                TouchPanel.EnabledGestures = value;
            }
        }
    }

    /// <summary>
    ///     Load graphics content for the screen, but 1st scale the presentation area.
    /// </summary>
    public virtual void LoadContent()
    {
        ScreenManager.ScalePresentationArea();
    }


    /// <summary>
    ///     Unload content for the screen.
    /// </summary>
    public virtual void UnloadContent()
    {
    }

    /// <summary>
    ///     Allows the screen to run logic, such as updating the transition position.
    ///     Unlike HandleInput, this method is called regardless of whether the screen
    ///     is active, hidden, or in the middle of a transition.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    /// <param name="otherScreenHasFocus">Indicates whether another screen has focus.</param>
    /// <param name="coveredByOtherScreen">Indicates whether the screen is covered by another screen.</param>
    public virtual void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        // Stores whether another screen has focus.
        this.otherScreenHasFocus = otherScreenHasFocus;

        if (IsExiting)
        {
            // If the screen is marked for exit, initiate the transition off.
            ScreenState = ScreenState.TransitionOff;

            // Update the transition position towards the "off" state.
            if (!UpdateTransition(gameTime, TransitionOffTime, 1))
            {
                // If the transition is complete, remove the screen.
                ScreenManager.RemoveScreen(this);
            }
        }
        else if (coveredByOtherScreen)
        {
            // If another screen is covering this one, start the transition off.
            if (UpdateTransition(gameTime, TransitionOffTime, 1))
            {
                // Still transitioning off.
                ScreenState = ScreenState.TransitionOff;
            }
            else
            {
                // Transition off complete, hide the screen.
                ScreenState = ScreenState.Hidden;
            }
        }
        else
        {
            // If no other screen is covering, start the transition on.
            if (UpdateTransition(gameTime, TransitionOnTime, -1))
            {
                // Still transitioning on.
                ScreenState = ScreenState.TransitionOn;
            }
            else
            {
                // Transition on complete, activate the screen.
                ScreenState = ScreenState.Active;
            }
        }

        // Check if the back buffer size has changed (e.g., window resize).
        if (ScreenManager.BackbufferHeight != ScreenManager.GraphicsDevice.PresentationParameters.BackBufferHeight
            || ScreenManager.BackbufferWidth != ScreenManager.GraphicsDevice.PresentationParameters.BackBufferWidth)
        {
            // Adjust the presentation area to match the new back buffer size.
            ScreenManager.ScalePresentationArea();
        }
    }

    /// <summary>
    ///     Helper for updating the screen transition position.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    /// <param name="time">The total time for the transition.</param>
    /// <param name="direction">The direction of the transition (-1 for on, 1 for off).</param>
    /// <returns>True if the transition is still in progress; otherwise, false.</returns>
    private bool UpdateTransition(GameTime gameTime, TimeSpan time, int direction)
    {
        // Calculate the amount to move the transition position.
        float transitionDelta;

        if (time == TimeSpan.Zero)
        {
            transitionDelta = 1;
        }
        else
        {
            transitionDelta = (float)(gameTime.ElapsedGameTime.TotalMilliseconds / time.TotalMilliseconds);
        }

        // Update the transition position.
        TransitionPosition += transitionDelta * direction;

        // Check if the transition has reached its end.
        if ((direction < 0 && TransitionPosition <= 0) || (direction > 0 && TransitionPosition >= 1))
        {
            // Clamp the transition position to the valid range.
            TransitionPosition = MathHelper.Clamp(TransitionPosition, 0, 1);
            return false; // Transition finished.
        }

        // Transition is still in progress.
        return true;
    }

    /// <summary>
    ///     Handles user input for the screen. Called only when the screen is active.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    /// <param name="inputState">The current input state.</param>
    public virtual void HandleInput(GameTime gameTime, InputState inputState)
    {
    }

    /// <summary>
    ///     Draws the screen content.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    public virtual void Draw(GameTime gameTime)
    {
    }

    /// <summary>
    ///     Initiates the screen's exit process, respecting transition timings.
    /// </summary>
    public void ExitScreen()
    {
        if (TransitionOffTime == TimeSpan.Zero)
        {
            // If no transition time, remove the screen immediately.
            ScreenManager.RemoveScreen(this);
        }
        else
        {
            // Mark the screen for exiting, which triggers the transition off.
            IsExiting = true;
        }
    }
}