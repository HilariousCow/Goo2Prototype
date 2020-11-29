using System;
using Unity.Mathematics;

namespace Ludopathic.Goo.Data
{
    [Serializable]
    //This is all the data for a snapshot. Jobs themselves may pluck sub parts of blobs.
    public struct BlobData : IMovablePoint, ITeamID
    {
        public int TeamID { get; set; }
        public float2 Accel { get; set; }
        public float2 Vel { get; set; }
        public float2 Pos { get; set; }
    }
}