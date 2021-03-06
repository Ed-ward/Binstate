﻿using System;
using System.Threading.Tasks;

namespace Binstate
{
  internal static class EnterActionInvokerFactory<TEvent>
  {
    public static NoParameterEnterActionActionInvoker<TEvent> Create(Action<IStateMachine<TEvent>> action)
      => new(stateMachine =>
             {
               action(stateMachine);

               return null;
             });

    public static NoParameterEnterActionActionInvoker<TEvent> Create(Func<IStateMachine<TEvent>, Task?> action) => new(action);

    public static EnterActionInvoker<TEvent, TArg> Create<TArg>(Action<IStateMachine<TEvent>, TArg?> action)
      => new((stateMachine, arg) =>
             {
               action(stateMachine, arg);

               return null;
             });

    public static EnterActionInvoker<TEvent, TArgument> Create<TArgument>(Func<IStateMachine<TEvent>, TArgument?, Task?> action) => new(action);
  }
}
