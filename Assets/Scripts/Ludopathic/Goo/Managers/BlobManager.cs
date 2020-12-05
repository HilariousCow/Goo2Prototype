
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

      //Gameplay properties
      
      public float CursorAccel = 5.0f;
      public float CursorLinearFriction = 0.1f;
      public float CursorConstantFriction = 0.8f;
      
      [Space]
      public float BlobLinearFriction;
      public float BlobConstantFriction;
      
      [Space]
      public float MaxSpringDistance = 4f;

      [Space]
      public float PetriDishRadius = 2f;
      //One blob manager exists per game, or is possibly just a singleton to store persistent swap data for all blobs.
      [SerializeField]
      public List<BlobData> _ListOfAllBlobs;//CPU side starting values.

      //
      //Job memory
      //
      
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
      private Transform[] _cursorOutputTransforms;
      private TransformAccessArray _cursorTransformAccessArray;
      
    
      
      //Blob Properties.
      private NativeArray<int> _blobTeamIDs;
      private NativeArray<float2> _blobAccelerations;
      private NativeArray<float2> _blobVelocities;
      private NativeArray<float2> _blobPositions;
      
      
      //Blob displays.
      private Transform[] _blobOutputTransforms;
      private Material[] _blobMaterialInstances;
      
      
      private TransformAccessArray _blobTransformAccessArray;

      
      //Goo Graph
      //think about slices for each blob which is just other-nearby-blobs. But have to remember their master index
      public int MAX_EDGES_PER_BLOB = 12;
      private NativeArray<BlobEdge> _blobEdges;
      private NativeArray<int> _blobEdgeCount;
      
      //Job Handles
      private static int _GameFrame = 0;
      private JobHandle _jobHandleResetBlobAccelerations;
      private JobHandle _jobHandleResetCursorAccelerations;
      private JobHandle _jobHandleResetJobs;
      private JobHandle _jobHandleSetCursorAcceleration;
      private JobHandle _jobHandleApplyCursorFriction;
      private JobHandle _jobHandleApplyCursorAccelAndVelocity;
      private JobHandle _jobHandleCursorsInfluenceBlobs;
      private JobHandle _jobHandleApplyBlobFriction;
      private JobHandle _jobHandleUpdateBlobPositions;
      private JobHandle _jobHandleCopyBlobsToTransforms;
      private JobHandle _jobHandleCopyCursorsToTransforms;
      private JobHandle _jobHandleBuildEdges;


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
            _cursorRadii[index] = 10.0f;
         }
         
         _cursorOutputTransforms = new Transform[NUM_CURSORS];
         //Cursor output things
         for (int index = 0; index < NUM_CURSORS; index++)
         {
            GameObject blobInstance = Instantiate(BlobPrefab);
            blobInstance.name = $"Cursor{index}";
            blobInstance.transform.rotation = Quaternion.Euler(90f,0f,0f);
            blobInstance.transform.localScale = Vector3.one * _cursorRadii[index];
            _cursorOutputTransforms[index] = blobInstance.transform;
            
            
         }
         _cursorTransformAccessArray = new TransformAccessArray(_cursorOutputTransforms);
         //Blob enabling
         
         
         //spawn values.
         for (int index = 0; index < TRANSFORM_ARRAY_SIZE; index++)
         {
            Vector3 randomPos = Random.insideUnitCircle * PetriDishRadius;
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
         _blobMaterialInstances = new Material[TRANSFORM_ARRAY_SIZE];
         for (int index = 0; index < TRANSFORM_ARRAY_SIZE; index++)
         {
            GameObject blobInstance = Instantiate(BlobPrefab);
            blobInstance.name = $"BlobOutput{index}";
            blobInstance.transform.rotation = Quaternion.Euler(90f,0f,0f);
            _blobOutputTransforms[index] = blobInstance.transform;
            _blobMaterialInstances[index] = Instantiate( blobInstance.GetComponent<MeshRenderer>().sharedMaterial );
            blobInstance.GetComponent<MeshRenderer>().sharedMaterial = _blobMaterialInstances[index];
         }

         
         //
         // Goo graph
         //

         
         _blobEdges = new NativeArray<BlobEdge>(TRANSFORM_ARRAY_SIZE * MAX_EDGES_PER_BLOB, Allocator.Persistent);
         _blobEdgeCount = new NativeArray<int>(TRANSFORM_ARRAY_SIZE, Allocator.Persistent);
         
         _blobTransformAccessArray = new TransformAccessArray(_blobOutputTransforms);
      }

    

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
            _cursorInputDeltas[index] = InputMan.ListOfSources[index].GetInputAxis() * CursorAccel;//todo: needs a game frame to reference
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
            AccumulatedAcceleration = _blobAccelerations
         };

         var jobDataResetCursorAccelerations = new JobResetAcceleration
         {
            AccumulatedAcceleration = _cursorAccelerations
         };
         #endregion //ResetBeginningOfSimFrame
         
         
         
         
         #region Updates
         //
         // Cursors must be done first. Luckily there's very few
         //
         
         
         //build edges with existing positions

         var jobBuildEdges = new JobFindEdges
         {
            Positions = _blobPositions,
            BlobEdges = _blobEdges,
            BlobEdgeCount = _blobEdgeCount,
            MaxEdgeDistanceSq = MaxSpringDistance * MaxSpringDistance,
            MaxEdgesPerBlob = MAX_EDGES_PER_BLOB
            
         };
         
         
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
            LinearFriction = CursorLinearFriction,
            ConstantFriction = CursorConstantFriction,
            AccumulatedAcceleration = _cursorAccelerations,
            Velocity = _cursorVelocities
         };
         
         
         var jobDataUpdateCursorPositions = new JobApplyAcceelrationAndVelocity
         {
            DeltaTime = deltaTime,
            AccumulatedAcceleration = _cursorAccelerations,
            VelocityInAndOut = _cursorVelocities,
            PositionInAndOut = _cursorPositions
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
         
      
         var jobDataApplyFrictionToBlobs = new JobApplyLinearAndConstantFriction
         {
            DeltaTime = deltaTime,
            //TODO: maybe I want friction based on acceleration (t*t) since that's the freshest part of this.
            //So, constant + linear(t) + accelerative (t*t)
            LinearFriction = BlobLinearFriction,
            ConstantFriction = BlobConstantFriction,
            AccumulatedAcceleration = _blobAccelerations,
            Velocity = _blobVelocities
         };
         
         //Blob sim gets updated
         var jobDataUpdateBlobPositions = new JobApplyAcceelrationAndVelocity
         {
            DeltaTime = deltaTime,
            AccumulatedAcceleration = _blobAccelerations,
            VelocityInAndOut = _blobVelocities,
            PositionInAndOut = _blobPositions
         };
     
            
         var jobDataCopyBlobsToTransforms = new JobCopyBlobsToTransforms
         {
            BlobPos = _blobPositions
         };
         
         var jobDataCopyCursorsToTransforms = new JobCopyBlobsToTransforms
         {
            BlobPos = _cursorPositions
         };
         #endregion //Updates
         
         #endregion // JobDataSetup
         
         #region Job Kickoff and Dependancy
         //
         // Fire off jobs with all the data that has been set up above. Prefer not to in-line job data and job scheduling due to dependancies
         //
         
         #region ResetBeginningOfSimFrame
         _jobHandleResetBlobAccelerations = jobDataResetBlobAccelerations.Schedule(_blobAccelerations.Length, 64);
         _jobHandleResetCursorAccelerations = jobDataResetCursorAccelerations.Schedule(_cursorAccelerations.Length, 1);
         
         #endregion //ResetBeginningOfSimFrame
         
         //_jobHandleResetJobs.Complete();

         #region Graph Building
         //Construct list of Edges
         
         //Build list of edges per node (limit: closest N per node)
         _jobHandleBuildEdges = jobBuildEdges.Schedule(_blobPositions.Length, 64);
         //_jobHandleBuildEdges = jobBuildEdges.Schedule();

         #endregion // Graph Building
         
         
         //todo require above jobs are complete in combo
         _jobHandleResetJobs = JobHandle.CombineDependencies(_jobHandleResetBlobAccelerations, _jobHandleResetCursorAccelerations, _jobHandleBuildEdges);
         
         #region SimUpdateFrame
         //
         // Cursors must be done first. Luckily there's very few
         //
         
      
         //update cursors
         _jobHandleSetCursorAcceleration = jobDataSetCursorAcceleration.Schedule(_cursorInputDeltas.Length, 1, _jobHandleResetJobs);
         
         _jobHandleApplyCursorFriction = jobDataApplyCursorFriction.Schedule(_cursorInputDeltas.Length, 1, _jobHandleSetCursorAcceleration);
         
         _jobHandleApplyCursorAccelAndVelocity = jobDataUpdateCursorPositions.Schedule(_cursorInputDeltas.Length, 1, _jobHandleApplyCursorFriction);
         
         _jobHandleCursorsInfluenceBlobs = jobDataCursorsInfluenceBlobs.Schedule(_blobPositions.Length, 64, _jobHandleApplyCursorAccelAndVelocity);
         
         //Cursor Influences blobs once it's ready
         
         
         //todo: blob spring forces.
         
         //Blob sim gets updated after cursor influence
         _jobHandleApplyBlobFriction = jobDataApplyFrictionToBlobs.Schedule(_blobAccelerations.Length, 64, _jobHandleCursorsInfluenceBlobs);
         _jobHandleUpdateBlobPositions = jobDataUpdateBlobPositions.Schedule(_blobAccelerations.Length, 64, _jobHandleApplyBlobFriction);
         
         #endregion //SimUpdateFrame
      
         //temp - needs an interpolator job
         
         //Todo: spit out into a particle effect instead of transforms, which are probably slow as heck
         //but this is still somewhat useful for debug
         _jobHandleCopyBlobsToTransforms = jobDataCopyBlobsToTransforms.Schedule(_blobTransformAccessArray, _jobHandleUpdateBlobPositions);
         _jobHandleCopyCursorsToTransforms = jobDataCopyCursorsToTransforms.Schedule(_cursorTransformAccessArray, _jobHandleCopyBlobsToTransforms);
         
         _jobHandleCopyCursorsToTransforms.Complete();
         
         #endregion // Job Kickoff and Dependancy
         
         
         //No. You must call "complete" on any handle that has something dependant on it. Which is all of them, you'd expect.
         //maybe i only need to complete the last, since that's dependant.
      }


      public Gradient EdgeBlobGradient;
      private void LateUpdate()
      {

         for (int i = 0; i < _blobMaterialInstances.Length; i++)
         {
            float frac = ((float)_blobEdgeCount[i] / (float) MAX_EDGES_PER_BLOB);
            Color color = EdgeBlobGradient.Evaluate( frac );
            _blobMaterialInstances[i].SetColor("BlobColor", color);
         }
         
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


         if (_blobEdges.IsCreated) _blobEdges.Dispose();
         if (_blobEdgeCount.IsCreated) _blobEdgeCount.Dispose();
      }
   }
}