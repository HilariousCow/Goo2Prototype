using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ludopathic
{
    //Input sources can generate a list of input stamps since the last time they were updated.
    public interface IInputSource
    {
        void PollInputs(int gameFrame);//adds 

        void PopOldInputs(int gameFrame, int oldestFrameAgeAllowed);

        Vector2 GetInputAxis();
        float GetPressure();
    }
}
