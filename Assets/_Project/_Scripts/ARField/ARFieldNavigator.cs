using System;
using UnityEngine;

namespace Artti.ARField
{
    public class ARFieldNavigator
    {
        public ARFieldState CurrentState { get; private set; } = ARFieldState.CameraScan;
        public event Action<ARFieldState> OnStateChanged;

        public void TransitionTo(ARFieldState newState)
        {
            if (CurrentState != newState)
            {
                CurrentState = newState;
                OnStateChanged?.Invoke(newState);
            }
        }
    }
}
