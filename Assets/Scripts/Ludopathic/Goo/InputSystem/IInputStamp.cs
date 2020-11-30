using UnityEngine;

namespace Ludopathic
{
    //used to update a cursor
    public struct IInputStamp
    {
        public int GameFrame;//the frame of gameplay that this will apply. Possibly unneeded via logic but good for debug
        public Vector2 Delta;

        public float TriggerPressure; //maybe this is just a "byte" since triggers only really give you 255 values. for now it's okay to be a float.
    }
}
