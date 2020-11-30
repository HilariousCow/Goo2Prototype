using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ludopathic
{
    [Serializable]
    public struct InputUnityLegacy : IInputSource
    {
        [SerializeField]
        public List<IInputStamp> QueuedInputs;
        public void PollInputs(int gameFrame)
        {
            QueuedInputs ??= new List<IInputStamp>();
            
            Vector2 delta = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            float triggerPressure = Input.GetAxisRaw("Fire1") ;//yeah this becomes a target value for a cursor's actual strength. i.e. there's a max growth rate, maybe? Mostly to convert binary to analogue
            QueuedInputs.Add( new IInputStamp
            {
                GameFrame = gameFrame,
                Delta = delta,
                TriggerPressure = triggerPressure
            } );
        }

        public void PopOldInputs(int gameFrame, int oldestFrameAgeAllowed)
        {
            float cullBeforeGameFrame = gameFrame - oldestFrameAgeAllowed; 
            
            while (QueuedInputs.Count > 0 && QueuedInputs[0].GameFrame < cullBeforeGameFrame )
            {
                QueuedInputs.RemoveAt(0);
            }
        }

        public Vector2 GetInputAxis()
        {
            
            if(QueuedInputs.Count == 0) return Vector2.zero;
            return QueuedInputs[QueuedInputs.Count - 1].Delta;
        }

        public float GetPressure()
        {
            if(QueuedInputs.Count == 0) return 0.0f;
            return QueuedInputs[QueuedInputs.Count - 1].TriggerPressure;
        
        }
    }
}
