using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace Ludopathic.Goo.Jobs
{
    public struct JobCopyBlobsToTransforms : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<float2> _blobPos;
        public void Execute(int index, TransformAccess transform)
        {
            transform.position =  new float3(_blobPos[index].x,0f,_blobPos[index].y) ;
        }
    }
}
