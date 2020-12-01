using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ludopathic
{
    public class InputManager : MonoBehaviour
    {
       
        public List<IInputSource> ListOfSources;


        public InputUnityLegacy _LegacyInput;


        public Dictionary<int, IInputStamp> _CursorIDsToInputStamps;
        
        // Start is called before the first frame update
        void Start()
        {
            ListOfSources = new List<IInputSource>();
            
            _LegacyInput = new InputUnityLegacy();
            ListOfSources.Add(_LegacyInput);
        }

 
        public void PollAllInputs(int gameFrame, int oldestFrameAgeAllowed)
        {
            for (int i = 0; i < ListOfSources.Count; i++)
            {
                ListOfSources[i].PollInputs(gameFrame);
            }

            for (int i = 0; i < ListOfSources.Count; i++)
            {
                ListOfSources[i].PopOldInputs(gameFrame, oldestFrameAgeAllowed);
            }
        }
    }
}
