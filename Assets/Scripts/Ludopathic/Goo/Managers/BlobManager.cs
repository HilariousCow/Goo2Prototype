
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
      public GameObject BlobPrefab;
      
      
      //One blob manager exists per game, or is possibly just a singleton to store persistent swap data for all blobs.
      [SerializeField]
      public List<BlobData> _ListOfAllBlobs;

      //Cursor properties
      private const int NUM_CURSORS = 1;
      private NativeArray<int> _cursorTeamIDs;
      private NativeArray<float2> _cursorInputDeltas;
      private NativeArray<float2> _cursorAccelerations;
      private NativeArray<float2> _cursorVelocities;
      private NativeArray<float2> _cursorPositions;
      private NativeArray<float> _cursorRadii;
      //TODO: effective radii
      
      //Cursor display
      
      
      
      
      //Blob Properties.
      private NativeArray<int> _blobTeamIDs;
      private NativeArray<float2> _blobAccelerations;
      private NativeArray<float2> _blobVelocities;
      private NativeArray<float2> _blobPositions;
      
      
      //Blob displays.
      private Transform[] _blobOutputTransforms;
      private TransformAccessArray _blobTransformAccessArray;

      
      //Job Handles
      
     
      
      
      private void OnEnable()
      {
         Application.targetFrameRate = 600;
         int TRANSFORM_ARRAY_SIZE = _ListOfAllBlobs.Count;
         
         
         _cursorTeamIDs = new NativeArray<int>(NUM_CURSORS, Allocator.Persistent);
         _cursorInputDeltas = new NativeArray<float2>(NUM_CURSORS, Allocator.Persistent);
         _cursorAccelerations= new NativeArray<float2>(NUM_CURSORS, Allocator.Persistent);
         _cursorVelocities= new NativeArray<float2>(NUM_CURSORS, Allocator.Persistent);
         _cursorPositions= new NativeArray<float2>(NUM_CURSORS, Allocator.Persistent);
         _cursorRadii= new NativeArray<float>(NUM_CURSORS, Allocator.Persistent);

         for (int index = 0; index < NUM_CURSORS; index++)
         {
            _cursorTeamIDs[index] = index;
            _cursorInputDeltas[index] = float2.zero;
            _cursorAccelerations[index] =  float2.zero;
            _cursorVelocities[index] = float2.zero;
            _cursorPositions[index] = Random.insideUnitCircle;
            _cursorRadii[index] = 1.0f;
         }
         
         //Blob enabling
         
         
         
         
         
         //spawn values.
         for (int index = 0; index < TRANSFORM_ARRAY_SIZE; index++)
         {
            Vector3 randomPos = Random.insideUnitCircle * 20.0f;
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
         _blobOutputTransforms = new Transform[TRANSFORM_ARRAY_SIZE];
         for (int index = 0; index < TRANSFORM_ARRAY_SIZE; index++)
         {
            GameObject blobInstance = Instantiate(BlobPrefab);
            blobInstance.name = $"BlobOutput{index}";
            blobInstance.transform.rotation = Quaternion.Euler(90f,0f,0f);
            _blobOutputTransforms[index] = blobInstance.transform;
         }

         _blobTransformAccessArray = new TransformAccessArray(_blobOutputTransforms);
      }

      private static int _GameFrame = 0;
      private JobHandle _jobHandleResetBlobAccelerations;
      private JobHandle _jobHandleResetCursorAccelerations;
      private JobHandle _jobHandleResetJobs;
      private JobHandle _jobHandleSetCursorAcceleration;
      private JobHandle _jobHandleApplyCursorFriction;
      private JobHandle _jobHandleApplyCursorAccelerationToVelocity;
      private JobHandle _jobHandleApplyCursorVelocityToPosition;
      private JobHandle _jobHandleCursorsInfluenceBlobs;
      private JobHandle _jobHandleApplyBlobAccelerationToVelocity;
      private JobHandle _jobHandleApplyBlobVelocityToPosition;
      private JobHandle _jobHandleCopyBlobsToTransforms;

      private void Update()
      {
         InputMan.PollAllInputs(_GameFrame, 10);

         Vector2 dir = InputMan.ListOfSources[0].GetInputAxis();
         float pressure = InputMan.ListOfSources[0].GetPressure();
         //next: do a test job that uses the above inputs to change the velocity
         
        // Debug.Log( $" Direction: {dir}, Pressure: {pressure}");

         //TODO Set up simulation data (i.e. going back in time) before processing it.
         
         
         
         UpdateSimulation(Time.deltaTime);
         
       
      
      }

     
      
      private void UpdateSimulation(float deltaTime)
      {
         
         //todo: break this down better. use delta time plus last sim time to figure out a list of game frames to step through, using a starting state.
         for (int index = 0; index < NUM_CURSORS; index++)
         {
            //_cursorTeamIDs[index] = index;
            _cursorInputDeltas[index] = InputMan.ListOfSources[index].GetInputAxis();//todo: needs a game frame to reference
            //_cursorAccelerations[index] =  float2.zero;
            //_cursorVelocities[index] = float2.zero;
            //_cursorPositions[index] = Random.insideUnitCircle;
            //_cursorRadii[index] = 1.0f;
         }
         
         
         #region JobDataSetup
         //
         //Init all job data here. Declare roughly inline. Optional brackets for things that can be parallel
         //

         #region ResetBeginningOfSimFrame
         var jobDataResetBlobAccelerations = new JobResetAcceleration
         {
            _accumulatedAcceleration = _blobAccelerations
         };

         var jobDataResetCursorAccelerations = new JobResetAcceleration
         {
            _accumulatedAcceleration = _cursorAccelerations
         };
         #endregion //ResetBeginningOfSimFrame
         
         #region Updates
         //
         // Cursors must be done first. Luckily there's very few
         //
         
         //update cursor accel based on inputs
         var jobDataSetCursorAcceleration = new JobSetAcceleration
         {
            ValueToSet = _cursorInputDeltas,
            AccumulatedAcceleration = _cursorAccelerations
         };
         
         //update cursor friction
         var jobDataApplyCursorFriction = new JobApplyLinearAndConstantFriction
         {
            DeltaTime = deltaTime,
            //TODO: maybe I want friction based on acceleration (t*t) since that's the freshest part of this.
            //So, constant + linear(t) + accelerative (t*t)
            LinearFriction = 0.5f,
            ConstantFriction = 0.5f,
            AccumulatedAcceleration = _cursorAccelerations,
            Velocity = _cursorVelocities
         };
         
         var jobDataApplyCursorAccelerationToVelocity = new JobApplyDerivative
         {
            DeltaTime = deltaTime,
            AccumulatedAcceleration = _cursorAccelerations,
            VelocityInAndOut = _cursorVelocities
         };
         
         //Now we can update the blobs with the new state of the cursors
         var jobDataCursorsInfluenceBlobs = new JobCursorsInfluenceBlobs
         {
            CursorPositions = _cursorPositions,
            CursorVelocities = _cursorVelocities,
            CursorRadius = _cursorRadii,
            BlobPositions = _blobPositions, 
            BlobAccelAccumulator = _blobAccelerations
         } ;
         
         //We update the cursor pos only after we've influenced the blobs, so that we don't "leave behind" blobs.
         //Consider moving this up one if that's no good
         var jobDataApplyCursorVelocityToPosition = new JobApplyDerivative
         {
            DeltaTime = deltaTime,
            AccumulatedAcceleration = _cursorVelocities,
            VelocityInAndOut = _cursorPositions
         };
         
         
         //Blob sim gets updated
         var jobDataApplyBlobAccelerationToVelocity = new JobApplyDerivative
         {
            DeltaTime = deltaTime,
            AccumulatedAcceleration = _blobAccelerations,
            VelocityInAndOut = _blobVelocities
         };
         
         var jobDataApplyBlobVelocityToPosition = new JobApplyDerivative
         {
            DeltaTime = deltaTime,
            AccumulatedAcceleration = _blobVelocities,
            VelocityInAndOut = _blobPositions
         };
            
         var jobDataCopyBlobsToTransforms = new JobCopyBlobsToTransforms
         {
            BlobPos = _blobPositions
         };
         #endregion //Updates
         
         #endregion // JobDataSetup
         
         #region Job Kickoff and Dependancy
         //
         // Fire off jobs with all the data that has been set up above. Prefer not to in-line job data and job scheduling due to dependancies
         //
         //aka var job = IJobParallelForTransformExtensions.Schedule<JobCopyBlobsToTransforms>( jobData, _blobTransforms, default);
         //_jobSplat1 = jobData.Schedule<JobCopyBlobsToTransforms>(_blobTransforms, default);
         //_jobSplat2 = jobData.Schedule<JobCopyBlobsToTransforms>(_blobTransforms, _jobSplat1);
         
         
         #region ResetBeginningOfSimFrame
         _jobHandleResetBlobAccelerations = jobDataResetBlobAccelerations.Schedule(_blobAccelerations.Length, 64);
         _jobHandleResetCursorAccelerations = jobDataResetCursorAccelerations.Schedule(_cursorAccelerations.Length, 1);
         
         #endregion //ResetBeginningOfSimFrame
         
         //todo require above jobs are complete in combo
         _jobHandleResetJobs = JobHandle.CombineDependencies(_jobHandleResetBlobAccelerations, _jobHandleResetCursorAccelerations);
      
         //_jobHandleResetJobs.Complete();
         
         #region SimUpdateFrame
         //
         // Cursors must be done first. Luckily there's very few
         //
         
         //update cursor accel based on inputs
         
         //to continue: keep turning these copies of the job datas into job handles/schedules with dependancies.
         //after that: make the job handles into members and .Complete() them in late update.
         
         //Note to self: when iterating the sim multiple times per frame, I will need to .Complete() them before iterating another sim step.
         
         //update cursors
         _jobHandleSetCursorAcceleration = jobDataSetCursorAcceleration.Schedule(_cursorInputDeltas.Length, 1, _jobHandleResetJobs);
         
         _jobHandleApplyCursorFriction = jobDataApplyCursorFriction.Schedule(_cursorInputDeltas.Length, 1, _jobHandleSetCursorAcceleration);
         
         _jobHandleApplyCursorAccelerationToVelocity = jobDataApplyCursorAccelerationToVelocity.Schedule(_cursorInputDeltas.Length, 1, _jobHandleApplyCursorFriction);
         
         _jobHandleApplyCursorVelocityToPosition = jobDataApplyCursorVelocityToPosition.Schedule(_cursorInputDeltas.Length, 1, _jobHandleApplyCursorAccelerationToVelocity);
         
         
         _jobHandleCursorsInfluenceBlobs = jobDataCursorsInfluenceBlobs.Schedule(_blobPositions.Length, 64, _jobHandleApplyCursorVelocityToPosition);
         
         
         
         //Cursor Influences blobs once it's ready
         
         //   _jobHandleCursorsInfluenceBlobs.Complete();
            
         //Blob sim gets updated after cursor influence
         _jobHandleApplyBlobAccelerationToVelocity = jobDataApplyBlobAccelerationToVelocity.Schedule(_blobAccelerations.Length, 64, _jobHandleCursorsInfluenceBlobs);
            //_jobHandleApplyBlobAccelerationToVelocity.Complete();
         _jobHandleApplyBlobVelocityToPosition = jobDataApplyBlobVelocityToPosition.Schedule(_blobVelocities.Length, 64, _jobHandleApplyBlobAccelerationToVelocity);
            //_jobHandleApplyBlobVelocityToPosition.Complete();
         #endregion //SimUpdateFrame
      
         //temp - needs an interpolator job
         _jobHandleCopyBlobsToTransforms = jobDataCopyBlobsToTransforms.Schedule<JobCopyBlobsToTransforms>(_blobTransformAccessArray, _jobHandleApplyBlobVelocityToPosition);
         _jobHandleCopyBlobsToTransforms.Complete();
         
         #endregion // Job Kickoff and Dependancy
         
         
         //No. You must call "complete" on any handle that has something dependant on it. Which is all of them, you'd expect.
         //maybe i only need to complete the last, since that's dependant.
      }

      private void LateUpdate()
      {
         
         
         
         
         //_jobSplat2.Complete();//force this job to hold up the thread until complete
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
         
         
         if(_cursorTeamIDs.IsCreated) _cursorTeamIDs.Dispose();
         if(_cursorInputDeltas .IsCreated) _cursorInputDeltas.Dispose();
         if(_cursorAccelerations.IsCreated) _cursorAccelerations.Dispose();
         if(_cursorVelocities.IsCreated) _cursorVelocities.Dispose();
         if(_cursorPositions.IsCreated) _cursorPositions.Dispose();
         if(_cursorRadii.IsCreated) _cursorRadii.Dispose();
         
         
         if(_blobVelocities.IsCreated) _blobVelocities.Dispose();
         if(_blobPositions .IsCreated) _blobPositions.Dispose();
         if(_blobAccelerations.IsCreated) _blobAccelerations.Dispose();
         if(_blobTeamIDs.IsCreated) _blobTeamIDs.Dispose();
         
         if(_blobTransformAccessArray.isCreated) _blobTransformAccessArray.Dispose();
      }
   }
}