using System;

namespace Binstate
{
  // ReSharper disable once UnusedTypeParameter
  public static partial class Config<TState, TEvent>
  {
    /// <summary>
    /// This class is used to configure exit action of the currently configured state.
    /// </summary>
    public class Exit : Transitions
    {
      internal Action? ExitAction;

      /// <inheritdoc />
      protected Exit(TState stateId) : base(stateId) { }
      
      /// <summary>
      /// Specifies the action to be called on exiting the currently configured state.
      /// </summary>
      public Transitions OnExit(Action exitAction)
      {
        ExitAction = exitAction ?? throw new ArgumentNullException(nameof(exitAction));
        return this;
      }
    }
  }
}