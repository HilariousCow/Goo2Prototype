using Unity.Mathematics;

namespace Ludopathic.Goo.Data
{
    public interface IMovablePoint
    {

        float2 Accel { get; set;}//Accumulates "force" (though we skip mass)
        public float2 Vel  { get; set;} //which is then applied to this in another job...
        public float2 Pos  { get; set;} //which is then used to update this.
    }
}