﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Binstate
{
  /// <summary>
  /// The state machine. Use <see cref="Builder{TState, TEvent}"/> to configure and build a state machine.
  /// </summary>
  [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
  public partial class StateMachine<TState, TEvent> : IStateMachine<TState, TEvent>
  {
    private readonly Action<Exception> _onException;
    
    /// <summary>
    /// The map of all defined states
    /// </summary>
    private readonly Dictionary<TState, State<TState, TEvent>> _states;

    private readonly AutoResetEvent _lock = new AutoResetEvent(true);
    private volatile State<TState, TEvent> _activeState;

    internal StateMachine(State<TState, TEvent> initialState, Dictionary<TState, State<TState, TEvent>> states, Action<Exception> onException)
    {
      _states = states;
      _onException = onException;

      _activeState = initialState;
      ActivateInitialState(initialState, onException);
    }

    /// <inheritdoc />
    public bool Raise([NotNull] TEvent @event)
    {
      if (@event.IsNull()) throw new ArgumentNullException(nameof(@event));
      return PerformTransitionSync<Unit, Unit>(@event, null);
    }

    /// <inheritdoc />
    public bool Raise<T>([NotNull] TEvent @event, [CanBeNull] T argument)
    {
      if (@event.IsNull()) throw new ArgumentNullException(nameof(@event));
      return PerformTransitionSync<T, Unit>(@event, argument);
    }

    /// <inheritdoc />
    public Task<bool> RaiseAsync([NotNull] TEvent @event)
    {
      if (@event.IsNull()) throw new ArgumentNullException(nameof(@event));
      return PerformTransitionAsync<Unit, Unit>(@event, default);
    }

    /// <inheritdoc />
    public Task<bool> RaiseAsync<T>([NotNull] TEvent @event, [CanBeNull] T argument)
    {
      if (@event.IsNull()) throw new ArgumentNullException(nameof(@event));
      return PerformTransitionAsync<T, Unit>(@event, argument);
    }

    /// <summary>
    /// Tell the state machine that it should get an argument attached to the currently active state (or any of parents) and pass it to the newly activated state
    /// </summary>
    /// <typeparam name="TRelay">The type of the argument. Should be exactly the same as the generic type passed into 
    /// <see cref="Config{TState,TEvent}.Enter.OnEnter{T}(Action{T})"/> or one of it's overload when configured currently active state (of one of it's parent).
    /// </typeparam>
    public IStateMachine<TState, TEvent> Relaying<TRelay>() => new Relayer<TRelay>(this);

    private bool PerformTransitionSync<TA, TP>(TEvent @event, TA argument)
    {
      var data = PrepareTransition<TA, TP>(@event, argument);
      return data != null && PerformTransition(data.Value);
    }

    private Task<bool> PerformTransitionAsync<TA, TP>(TEvent @event, TA argument)
    {
      var data = PrepareTransition<TA, TP>(@event, argument);

      return data == null
        ? Task.FromResult(false)
        : Task.Run(() => PerformTransition(data.Value));
    }

    private void ActivateInitialState(State<TState, TEvent> initialState, Action<Exception> onException)
    {
      if(initialState.EnterArgumentType != null)
        throw new TransitionException("The enter action of the initial state must not require argument.");
      
      var enterAction = ActivateStateNotGuarded<Unit, Unit>(initialState, default);
      try {
        enterAction();
      }
      catch (Exception exception) {
        onException(exception);
      }
    }
    
    private State<TState, TEvent> GetStateById([NotNull] TState state) => 
      _states.TryGetValue(state, out var result) ? result : throw new TransitionException($"State '{state}' is not defined");

    [CanBeNull]
    private static State<TState, TEvent> FindLeastCommonAncestor(State<TState, TEvent> l, State<TState, TEvent> r)
    {
      if (ReferenceEquals(l, r)) return null; // no common ancestor with yourself

      var lDepth = l.DepthInTree;
      var rDepth = r.DepthInTree;
      while (lDepth != rDepth)
      {
        if (lDepth > rDepth)
        {
          lDepth--;
          l = l.ParentState;
        }
        else
        {
          rDepth--;
          r = r.ParentState;
        }
      }

      while (!ReferenceEquals(l, r))
      {
        l = l.ParentState;
        r = r.ParentState;
      }

      return l;
    }

    /// <summary>
    /// Validates that all 'enter' actions match (not)passed argument. Throws the exception if not, because it is not runtime problem, but the problem
    /// of configuration.
    /// </summary>
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    private static void ValidateStates<TA, TP>(
      IEnumerable<State<TState, TEvent>> states,
      State<TState, TEvent> activeState,
      TEvent @event,
      MixOf<TA,TP> argument)
    {
      var enterWithArgumentCount = 0;

      foreach (var state in states)
      {
        if (state.EnterArgumentType != null)
        {
          if (!argument.HasAnyArgument)
            throw new TransitionException($"The enter action of the state '{state.Id}' is configured as required an argument but no argument was specified.");
            
          if (!argument.IsMatch(state.EnterArgumentType))
            throw new TransitionException($"The state '{state.Id}' requires argument of type '{state.EnterArgumentType}' but no argument of compatible type has passed nor relayed");

          enterWithArgumentCount++;
        }
      }

      if (argument.HasAnyArgument && enterWithArgumentCount == 0)
      {
        var statesToActivate = string.Join("->", states.Select(_ => _.Id.ToString()));

        throw new TransitionException(
          $"Transition from the state '{activeState.Id}' by the event '{@event}' will activate following states [{statesToActivate}]. No one of them are defined with "
          + "the enter action accepting an argument, but argument was passed or relayed");
      }
    }
  }
}