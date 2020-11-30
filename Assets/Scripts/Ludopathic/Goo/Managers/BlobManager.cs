
using System;
using System.Collections.Generic;
using Ludopathic.Goo.Data;
using Ludopathic.Goo.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using Random = UnityEngine.Random;

namespace Ludopathic.Goo.Managers
{
   public class BlobManager : MonoBehaviour
   {

      public InputManager InputMan;
      //One blob manager exists per game, or is possibly just a singleton to store persistent swap data for all blobs.
      [SerializeField]
      public List<BlobData> _ListOfAllBlobs;

      //Should store a native array of blobs here i guess

      private NativeArray<float2> _blobVelocities;
      private NativeArray<float2> _blobPositions;
      private NativeArray<float2> _blobAccelerations;
      private NativeArray<int> _blobTeamIDs;

      private Transform[] _outputTransforms;
      private TransformAccessArray _blobTransforms;

      private JobHandle _jobSplat1;
      private JobHandle _jobSplat2;
      private void OnEnable()
      {
         Application.targetFrameRate = 600;
         int TRANSFORM_ARRAY_SIZE = _ListOfAllBlobs.Count;
         
         
         //spawn values.
         for (int index = 0; index < TRANSFORM_ARRAY_SIZE; index++)
         {
            Vector3 randomPos = Random.insideUnitCircle;
            Vector3 randomVel = Random.insideUnitCircle;

            _ListOfAllBlobs[index] = new BlobData
            {
               Accel = float2.zero,
               Pos = new float2(randomPos.x, randomPos.y),
               Vel = new float2(randomVel.x, randomVel.y),
               TeamID = index < TRANSFORM_ARRAY_SIZE / 2 ? 0 : 1
            };
         }
         
         
         
         //create scratch data
         _blobVelocities = new NativeArray<float2>(TRANSFORM_ARRAY_SIZE, Allocator.Persistent);
         _blobPositions = new NativeArray<float2>(TRANSFORM_ARRAY_SIZE, Allocator.Persistent);
         _blobAccelerations = new NativeArray<float2>(TRANSFORM_ARRAY_SIZE, Allocator.Persistent);
         _blobTeamIDs = new NativeArray<int>(TRANSFORM_ARRAY_SIZE, Allocator.Persistent);
         
         
         //copy init values into scratch data
         for (int index = 0; index < TRANSFORM_ARRAY_SIZE; index++)
         {
            _blobVelocities[index] = _ListOfAllBlobs[index].Vel;
            _blobPositions[index] = _ListOfAllBlobs[index].Pos;
            _blobAccelerations[index] = _ListOfAllBlobs[index].Accel;
         }
         
         //output things
         _outputTransforms = new Transform[TRANSFORM_ARRAY_SIZE];
         for (int index = 0; index < TRANSFORM_ARRAY_SIZE; index++)
         {
            _outputTransforms[index] = new GameObject($"BlobOutput{index}").transform;
         }

         _blobTransforms = new TransformAccessArray(_outputTransforms);
      }

      private static int _GameFrame = 0;

      private void Update()
      {
         InputMan.PollAllInputs(_GameFrame, 10);

         Vector2 dir = InputMan.ListOfSources[0].GetInputAxis();
         float pressure = InputMan.ListOfSources[0].GetPressure();
         //next: do a test job that uses the above inputs to change the velocity
         
         Debug.Log( $" Direction: {dir}, Pressure: {pressure}");
         
         
         //
         //Init all job data here
         //
        
         var jobData = new JobCopyBlobsToTransforms
         {
            _blobPos = _blobPositions
         };
         
         
         //
         // Fire off jobs/their dependancies here
         //
         
         
         //aka var job = IJobParallelForTransformExtensions.Schedule<JobCopyBlobsToTransforms>( jobData, _blobTransforms, default);
         _jobSplat1 = jobData.Schedule<JobCopyBlobsToTransforms>(_blobTransforms, default);
         _jobSplat2 = jobData.Schedule<JobCopyBlobsToTransforms>(_blobTransforms, _jobSplat1);
      
      }

      private void LateUpdate()
      {
         _jobSplat2.Complete();//force this job to hold up the thread until complete
         //also, looks like you can complete this in late update.
         //also, you only have to complete the job of last job in a chain.

         _GameFrame++;
      }

      private void OnDisable()
      {
         //All jobs must have .Complete called on them before trying to dispose any.
         
         //ALL native arrays will complain if jobs are not successully completed.
         
         //destroy scratch data
         //The "is created" is necessary because on disable may be called on this object even when it is not enabled (i.e. when application closest)
         if (_blobVelocities.IsCreated) _blobVelocities.Dispose();
         if(_blobPositions .IsCreated) _blobPositions.Dispose();
         if(_blobAccelerations.IsCreated) _blobAccelerations.Dispose();
         if(_blobTeamIDs.IsCreated) _blobTeamIDs.Dispose();
         
         if(_blobTransforms.isCreated) _blobTransforms.Dispose();
      }
   }
}