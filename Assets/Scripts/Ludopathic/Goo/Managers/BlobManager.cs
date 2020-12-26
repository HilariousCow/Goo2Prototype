
using System;
using Ludopathic.Goo.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.ParticleSystemJobs;

using KNN;
using KNN.Jobs;

using Random = UnityEngine.Random;

namespace Ludopathic.Goo.Managers
{
   public class BlobManager : MonoBehaviour
   {
      public InputManager InputMan;
      public GameObject BlobPrefab;
      public ParticleSystem BlobParticleSystemOutput;
      
      //Gameplay properties
      public float CursorRadius = 10.0f;
      public float CursorAccel = 5.0f;
      public float CursorLinearFriction = 0.1f;
      public float CursorConstantFriction = 0.8f;

      public GooPhysicsSettings GooPhysics;
    
      //Match settings.
      //Move to an SO
      [Space]
      public float PetriDishRadius = 2f;
      public int NumTeams = 2;
      
      //Eventually make obsolute (yes we want the knn tree!)
      public bool bUseKNNTree = false;
      
      
      
      
      
      //
      // Persistent Entity Memroy
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
      
      public int NUM_BLOBS = 1000;
      
      //Blob Properties.
      private NativeArray<int> _blobTeamIDs;
      //private NativeArray<int> _blobGroupIDs;//next up!
      private NativeArray<float2> _blobAccelerations;
      private NativeArray<float2> _blobVelocities;
      private NativeArray<float2> _blobPositions;
      private NativeArray<Color> _blobColors;
      
      private NativeArray<float3> _blobPositionsV3;
      
      
      //Goo Graph
      //think about slices for each blob which is just other-nearby-blobs. But have to remember their master index
      private const int ALLOCATE_MAX_EDGES_PER_BLOB = 20;
      private NativeArray<RangeQueryResult> _blobKNNNearestNeighbourQueryResults;
      
      [Obsolete]
      private NativeArray<BlobEdge> _blobEdges;
      [Obsolete]
      private NativeArray<int> _blobEdgeCount;
      
      //
      //Job Data
      //
      private JobZeroFloat2Array _jobDataResetBlobAccelerations;
      private JobZeroFloat2Array _jobDataResetCursorAccelerations;

      private JobCopyBlobInfoToFloat3 _jobDataCopyBlobInfoToFloat3;
      
      
      private KnnContainer _knnContainer;
      private KnnRebuildJob _jobBuildKnnTree;
      private QueryRangeBatchJob _jobDataQueryNearestNeighboursKNN;
      
      private JobFindEdges _jobDataQueryNearestNeighbours;
      private JobSpringForceUsingKNNResults _jobSpringForcesUsingKnn;
      private JobSpringForce _jobSpringForces;
      private JobSetAcceleration _jobDataSetCursorAcceleration;
      private JobApplyLinearAndConstantFriction _jobDataApplyCursorFriction;
      private JobApplyAcceelrationAndVelocity _jobDataUpdateCursorPositions;
      private JobCursorsInfluenceBlobs _jobDataCursorsInfluenceBlobs;
      private JobApplyLinearAndConstantFriction _jobDataApplyFrictionToBlobs;
      private JobApplyAcceelrationAndVelocity _jobDataUpdateBlobPositions;


      private JobDebugColorisationInt _jobDataDebugColorisationInt;
      //private JobDebugColorisationFloat _jobDataDebugColorisationFloat;//as yet unused
      private JobDebugColorisationFloat2Magnitude _jobDataDebugColorisationFloat2Magnitude;
      private JobDebugColorisationKNNRangeQuery _jobDataDebugColorisationKNNLength;
      
      
      private JopCopyBlobsToParticleSystem _jobDataCopyBlobsToParticleSystem;
      private JobCopyBlobsToTransforms _jobDataCopyCursorsToTransforms;
      
      
      //
      //Job Handles (probably don't need to be members)
      //
      
      private static int _GameFrame = 0;
      private JobHandle _jobHandleResetBlobAccelerations;
      private JobHandle _jobHandleResetCursorAccelerations;
      
      [Obsolete("Replacing with KNNQueries")]
      private JobHandle _jobHandleBuildEdges;
      private JobHandle _jobHandleResetJobs;
      private JobHandle _jobCopy2DArrayTo3DArray;//TODO




      private JobHandle _jobHandleCopyBlobInfoToFloat3;
      private JobHandle _jobHandleBuildKNNTree;
      
      private JobHandle _jobHandleSpringForces;
      
      private JobHandle _jobHandleSetCursorAcceleration;
      private JobHandle _jobHandleApplyCursorFriction;
      private JobHandle _jobHandleUpdateCursorPositions;
      private JobHandle _jobHandleCursorsInfluenceBlobs;
      private JobHandle _jobHandleApplyBlobFriction;
      private JobHandle _jobHandleUpdateBlobPositions;
      private JobHandle _jobHandleCopyCursorsToTransforms;
      private JobHandle _jobHandleCopyBlobsToParticleSystem;


      
      //Debug vis
      public enum BlobColorDebugStyle
      {
         Edges,
         Velocity,
         Acceleration,
         TeamID
      }

      
      //Debug drawing.
      public BlobColorDebugStyle DebugStyle;
      
      private void OnEnable()
      {
         Application.targetFrameRate = 600;
         
         _cursorTeamIDs = new NativeArray<int>(NUM_CURSORS, Allocator.Persistent);
         _cursorInputDeltas = new NativeArray<float2>(NUM_CURSORS, Allocator.Persistent);
         _cursorAccelerations= new NativeArray<float2>(NUM_CURSORS, Allocator.Persistent);
         _cursorVelocities= new NativeArray<float2>(NUM_CURSORS, Allocator.Persistent);
         _cursorPositions= new NativeArray<float2>(NUM_CURSORS, Allocator.Persistent);
         _cursorRadii= new NativeArray<float>(NUM_CURSORS, Allocator.Persistent);

         for (int index = 0; index < NUM_CURSORS; index++)
         {
            _cursorTeamIDs[index] = index % NUM_CURSORS;
            _cursorInputDeltas[index] = float2.zero;
            _cursorAccelerations[index] =  float2.zero;
            _cursorVelocities[index] = float2.zero;
            _cursorPositions[index] = Random.insideUnitCircle;
            _cursorRadii[index] = CursorRadius;
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


         InitBlobData(NUM_BLOBS, BlobParticleSystemOutput);

      }

      private void InitBlobData(int numBlobs, ParticleSystem blobParticleSystemOutput)
      {
         var main = BlobParticleSystemOutput.main;
         main.maxParticles = NUM_BLOBS;
         BlobParticleSystemOutput.Emit(NUM_BLOBS);
         //Blob enabling
     
         //create scratch data
         _blobVelocities = new NativeArray<float2>(NUM_BLOBS, Allocator.Persistent);
         _blobPositions = new NativeArray<float2>(NUM_BLOBS, Allocator.Persistent);
         _blobAccelerations = new NativeArray<float2>(NUM_BLOBS, Allocator.Persistent);
         _blobTeamIDs = new NativeArray<int>(NUM_BLOBS, Allocator.Persistent);
         _blobColors = new NativeArray<Color>(NUM_BLOBS, Allocator.Persistent);
         _blobPositionsV3 = new NativeArray<float3>(NUM_BLOBS, Allocator.Persistent);
         
         //copy init values into scratch data
         for (int index = 0; index < NUM_BLOBS; index++)
         {
            Vector3 randomPos = Random.insideUnitCircle * PetriDishRadius;
            Vector3 randomVel = Random.insideUnitCircle;
            _blobTeamIDs[index] = index % NumTeams;
            _blobVelocities[index] = new float2(randomVel.x, randomVel.y);
            _blobPositions[index] = new float2(randomPos.x, randomPos.y);
            _blobAccelerations[index] =  float2.zero;
            _blobColors[index] = Color.magenta;
            _blobPositionsV3[index] = new float3(randomPos.x, randomPos.y, _blobTeamIDs[index]);
         }

      
         //output things
       

         main = BlobParticleSystemOutput.main;
         main.maxParticles = NUM_BLOBS;
         
     
         _knnContainer = new KnnContainer(_blobPositionsV3, false, Allocator.Persistent);
         
         _blobEdges = new NativeArray<BlobEdge>(NUM_BLOBS * ALLOCATE_MAX_EDGES_PER_BLOB, Allocator.Persistent);//will become obsolete
         _blobEdgeCount = new NativeArray<int>(NUM_BLOBS, Allocator.Persistent);//will become obsolete
         
         
         InitJobData();
      }


      private void Update()
      {
         InputMan.PollAllInputs(_GameFrame, 10);

         //todo: update all these properties

         _jobDataQueryNearestNeighbours.MaxEdgeDistanceSq = GooPhysics.MaxSpringDistance * GooPhysics.MaxSpringDistance;
         _jobDataQueryNearestNeighbours.MaxEdgesPerBlob = GooPhysics.MaxNearestNeighbours;
         
         _jobDataQueryNearestNeighboursKNN.m_range = GooPhysics.MaxSpringDistance;
         
         _jobSpringForcesUsingKnn.SpringConstant = GooPhysics.SpringForceConstant;
         _jobSpringForcesUsingKnn.MaxEdgeDistanceRaw = GooPhysics.SpringForceConstant;
         
         _jobSpringForces.SpringConstant = GooPhysics.SpringForceConstant;
         _jobSpringForces.MaxEdgeDistanceRaw = GooPhysics.SpringForceConstant;
         _jobSpringForces.MaxEdgesPerBlob = GooPhysics.MaxNearestNeighbours;
         
         
         UpdateSimulation(Time.deltaTime);
      }

      private void InitJobData()
      {

         float deltaTime = 0f;
            #region JobDataSetup
         //
         //Init all job data here. Declare roughly inline. Optional brackets for things that can be parallel
         //

         #region ResetBeginningOfSimFrame
         _jobDataResetBlobAccelerations = new JobZeroFloat2Array
         {
            AccumulatedAcceleration = _blobAccelerations
         };

         _jobDataResetCursorAccelerations = new JobZeroFloat2Array
         {
            AccumulatedAcceleration = _cursorAccelerations
         };

      
         _jobDataCopyBlobInfoToFloat3 = new JobCopyBlobInfoToFloat3
         {
            BlobPos = _blobPositions,
            BlobTeams = _blobTeamIDs,
            BlobPosFloat3 = _blobPositionsV3
         };

         
         _jobBuildKnnTree = new KnnRebuildJob(_knnContainer);
      
         // Initialize all the range query results
          _blobKNNNearestNeighbourQueryResults = new NativeArray<RangeQueryResult>(_blobPositions.Length, Allocator.Persistent);

         // Each range query result object needs to declare upfront what the maximum number of points in range is
         for (int i = 0; i < _blobKNNNearestNeighbourQueryResults.Length; ++i) {
            // Allow for a maximum of 1024 results
            _blobKNNNearestNeighbourQueryResults[i] = new RangeQueryResult(ALLOCATE_MAX_EDGES_PER_BLOB, Allocator.Persistent);
         }
         
         _jobDataQueryNearestNeighboursKNN = new QueryRangeBatchJob{ m_container = _knnContainer,
            m_queryPositions = _blobPositionsV3, 
            m_range = GooPhysics.MaxSpringDistance,
            Results = _blobKNNNearestNeighbourQueryResults};
         
      
         
         #endregion //ResetBeginningOfSimFrame
         
         
     
         
         
         #region Updates
         //
         // Cursors must be done first. Luckily there's very few
         //
         
         
         //build edges with existing positions

         _jobDataQueryNearestNeighbours = new JobFindEdges
         {
            Positions = _blobPositions,
            BlobEdges = _blobEdges,
            BlobEdgeCount = _blobEdgeCount,
            //MaxEdgeDistanceSq = MaxSpringDistance * MaxSpringDistance,
            MaxEdgesPerBlob = GooPhysics.MaxNearestNeighbours
            
         };

         _jobSpringForcesUsingKnn = new JobSpringForceUsingKNNResults()
         {
            AccelerationAccumulator = _blobAccelerations,
            BlobNearestNeighbours = _blobKNNNearestNeighbourQueryResults,
            MaxEdgeDistanceRaw = GooPhysics.MaxSpringDistance,
            SpringConstant = GooPhysics.SpringForceConstant,
            Positions = _blobPositions
            
         };
         
         _jobSpringForces = new JobSpringForce()
         {
            Positions = _blobPositions,
            BlobEdges = _blobEdges,
            BlobEdgeCount = _blobEdgeCount,
            MaxEdgesPerBlob =  GooPhysics.MaxNearestNeighbours,
            MaxEdgeDistanceRaw = GooPhysics.MaxSpringDistance,
            SpringConstant = GooPhysics.SpringForceConstant,
            AccelerationAccumulator = _blobAccelerations
         };
         
         //update cursor accel based on inputs
         _jobDataSetCursorAcceleration = new JobSetAcceleration
         {
            ValueToSet = _cursorInputDeltas,
            AccumulatedAcceleration = _cursorAccelerations
         };
         
         //update cursor friction
         _jobDataApplyCursorFriction = new JobApplyLinearAndConstantFriction
         {
            DeltaTime = deltaTime,
            //TODO: maybe I want friction based on acceleration (t*t) since that's the freshest part of this.
            //So, constant + linear(t) + accelerative (t*t)
            LinearFriction = CursorLinearFriction,
            ConstantFriction = CursorConstantFriction,
            AccumulatedAcceleration = _cursorAccelerations,
            Velocity = _cursorVelocities
         };
         
         
         _jobDataUpdateCursorPositions = new JobApplyAcceelrationAndVelocity
         {
            DeltaTime = deltaTime,
            AccumulatedAcceleration = _cursorAccelerations,
            VelocityInAndOut = _cursorVelocities,
            PositionInAndOut = _cursorPositions
         };
         
     
         
         //Now we can update the blobs with the new state of the cursors
         _jobDataCursorsInfluenceBlobs = new JobCursorsInfluenceBlobs
         {
            CursorPositions = _cursorPositions,
            CursorVelocities = _cursorVelocities,
            CursorRadius = _cursorRadii,
            BlobPositions = _blobPositions, 
            BlobAccelAccumulator = _blobAccelerations
         };
         
      
         _jobDataApplyFrictionToBlobs = new JobApplyLinearAndConstantFriction
         {
            DeltaTime = deltaTime,
            //TODO: maybe I want friction based on acceleration (t*t) since that's the freshest part of this.
            //So, constant + linear(t) + accelerative (t*t)
            LinearFriction = GooPhysics.BlobLinearFriction,
            ConstantFriction = GooPhysics.BlobConstantFriction,
            AccumulatedAcceleration = _blobAccelerations,
            Velocity = _blobVelocities
         };
         
         //Blob sim gets updated
         _jobDataUpdateBlobPositions = new JobApplyAcceelrationAndVelocity
         {
            DeltaTime = deltaTime,
            AccumulatedAcceleration = _blobAccelerations,
            VelocityInAndOut = _blobVelocities,
            PositionInAndOut = _blobPositions
         };
     
            
         //Output

         _jobDataDebugColorisationInt = new JobDebugColorisationInt()
         {
            minVal = 0,
            maxVal = 10,
            values = _blobEdgeCount,
            colors = _blobColors,
         };
      
         _jobDataDebugColorisationKNNLength = new JobDebugColorisationKNNRangeQuery()
         {
            minVal = 0,
            maxVal = 10,
            values = _blobKNNNearestNeighbourQueryResults,
            colors = _blobColors,
         };
         
       /*  _jobDataDebugColorisationFloat = new JobDebugColorisationFloat
         {
            minVal = 0,
            maxVal = 10,
            values = _blobEdgeCount,
            colors =_blobColors,
         }*/

       _jobDataDebugColorisationFloat2Magnitude = new JobDebugColorisationFloat2Magnitude
       {
          minVal = 0,
          maxVal = 10,
          values = _blobVelocities,
          colors = _blobColors
       };
            
      
            
         _jobDataCopyBlobsToParticleSystem = new JopCopyBlobsToParticleSystem
         {
            colors =  _blobColors,
            positions = _blobPositions,
            velocities = _blobVelocities
         };
         
         _jobDataCopyCursorsToTransforms = new JobCopyBlobsToTransforms
         {
            BlobPos = _cursorPositions
         };
         #endregion //Updates
         
         #endregion // JobDataSetup
       
      }


      void UpdateJobDeltaTimes(float deltaTime)
      {
         //update cursor friction
         _jobDataApplyCursorFriction.DeltaTime = deltaTime;
         _jobDataUpdateCursorPositions.DeltaTime = deltaTime;
         _jobDataApplyFrictionToBlobs.DeltaTime = deltaTime;
        
         _jobDataUpdateBlobPositions.DeltaTime = deltaTime;
      
       
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

         UpdateJobDeltaTimes( deltaTime );
         
        
         #region Job Kickoff and Dependancy
         //
         // Fire off jobs with all the data that has been set up above. Prefer not to in-line job data and job scheduling due to dependancies
         //
         
         #region ResetBeginningOfSimFrame
         _jobHandleResetBlobAccelerations = _jobDataResetBlobAccelerations.Schedule(_blobAccelerations.Length, 64);
         _jobHandleResetCursorAccelerations = _jobDataResetCursorAccelerations.Schedule(_cursorAccelerations.Length, 1);
         
         #endregion //ResetBeginningOfSimFrame
         
         //_jobHandleResetJobs.Complete();

         #region Graph Building
         
         JobHandle _graphSetup;
         if (bUseKNNTree)
         {
            #region KNN Tree Building
            //We need to copy values of positions over into the knn tree (one day we might be able to rule this out)
            _jobHandleCopyBlobInfoToFloat3 = _jobDataCopyBlobInfoToFloat3.Schedule(_blobPositionsV3.Length, 64);
            _jobHandleBuildKNNTree = _jobBuildKnnTree.Schedule(_jobHandleCopyBlobInfoToFloat3);
            
             //now build the edges
             _graphSetup = _jobDataQueryNearestNeighboursKNN.Schedule(_blobPositionsV3.Length, 64, _jobHandleBuildKNNTree);
             #endregion
         }
         else
         {
            //This was the old way. (0)n^2
            _graphSetup = _jobHandleBuildEdges = _jobDataQueryNearestNeighbours.Schedule(_blobPositions.Length, 64);
         }

         //now search it
      
         
         //now copy back - oh! We don't need to! positions aren't changed. we just wanted the indices.

         //_jobHandleCopyFloat3ToBlobInfo = _jobDataCopyFloat3ToBlobs.Schedule(_blobPositionsV3.Length, 64); 
         
         
         //Build list of edges per node (limit: closest N per node)
       
         //_jobHandleBuildEdges = jobBuildEdges.Schedule();

         #endregion // Graph Building
         
         
         //todo require above jobs are complete in combo
         
         
         _jobHandleResetJobs = JobHandle.CombineDependencies(_jobHandleResetBlobAccelerations, _jobHandleResetCursorAccelerations, _graphSetup);
         
         #region SimUpdateFrame
         //
         // Cursors must be done first. Luckily there's very few
         //
         
      
         //update cursors
         _jobHandleSetCursorAcceleration = _jobDataSetCursorAcceleration.Schedule(_cursorInputDeltas.Length, 1, _jobHandleResetJobs);
         
         _jobHandleApplyCursorFriction = _jobDataApplyCursorFriction.Schedule(_cursorInputDeltas.Length, 1, _jobHandleSetCursorAcceleration);
         
         _jobHandleUpdateCursorPositions = _jobDataUpdateCursorPositions.Schedule(_cursorInputDeltas.Length, 1, _jobHandleApplyCursorFriction);
         
         _jobHandleCursorsInfluenceBlobs = _jobDataCursorsInfluenceBlobs.Schedule(_blobPositions.Length, 64, _jobHandleUpdateCursorPositions);
         
         //Cursor Influences blobs once it's ready
         //Blob sim gets updated after cursor influence
         
         
         
         //blobs all figure out how much push and pull is coming from neighbouring blobs.

         if (bUseKNNTree)
         {
             
            _jobHandleSpringForces = _jobSpringForcesUsingKnn.Schedule(_blobKNNNearestNeighbourQueryResults.Length, 64, _jobHandleCursorsInfluenceBlobs);
         }
         else
         {
            _jobHandleSpringForces = _jobSpringForces.Schedule(_blobAccelerations.Length, 64, _jobHandleCursorsInfluenceBlobs);
         }

         _jobHandleApplyBlobFriction = _jobDataApplyFrictionToBlobs.Schedule(_blobAccelerations.Length, 64, _jobHandleSpringForces);
         _jobHandleUpdateBlobPositions = _jobDataUpdateBlobPositions.Schedule(_blobAccelerations.Length, 64, _jobHandleApplyBlobFriction);
         
         #endregion //SimUpdateFrame
      
         //temp - needs an interpolator job
         
         //Todo: spit out into a particle effect instead of transforms, which are probably slow as heck
         //but this is still somewhat useful for debug

         JobHandle jobHandleDebugColorization;

         switch (DebugStyle)
         {
            case BlobColorDebugStyle.Edges:
               jobHandleDebugColorization = bUseKNNTree ? 
                  _jobDataDebugColorisationKNNLength.Schedule(_blobKNNNearestNeighbourQueryResults.Length, 64, _jobHandleUpdateBlobPositions) :
                  _jobDataDebugColorisationInt.Schedule(_blobEdgeCount.Length, 64, _jobHandleUpdateBlobPositions);
               break;
            case BlobColorDebugStyle.Velocity:
               jobHandleDebugColorization =
                  _jobDataDebugColorisationFloat2Magnitude.Schedule(_blobVelocities.Length, 64, _jobHandleUpdateBlobPositions);
               break;
            case BlobColorDebugStyle.Acceleration:
               jobHandleDebugColorization =
                  _jobDataDebugColorisationFloat2Magnitude.Schedule(_blobAccelerations.Length, 64, _jobHandleUpdateBlobPositions);
               break;
            case BlobColorDebugStyle.TeamID:
               jobHandleDebugColorization =
                  _jobDataDebugColorisationInt.Schedule(_blobTeamIDs.Length, 64, _jobHandleUpdateBlobPositions);
               break;
            default:
               throw new ArgumentOutOfRangeException();
         }
         _jobHandleCopyBlobsToParticleSystem = _jobDataCopyBlobsToParticleSystem.ScheduleBatch(BlobParticleSystemOutput, 64, jobHandleDebugColorization);
         _jobHandleCopyCursorsToTransforms = _jobDataCopyCursorsToTransforms.Schedule(_cursorTransformAccessArray, _jobHandleCursorsInfluenceBlobs);
         
         
         _jobHandleCopyBlobsToParticleSystem.Complete();
         _jobHandleCopyCursorsToTransforms.Complete();
         
         
         #endregion // Job Kickoff and Dependancy
         
         
         //No. You must call "complete" on any handle that has something dependant on it. Which is all of them, you'd expect.
         //maybe i only need to complete the last, since that's dependant.
      }

  

      public Gradient EdgeBlobGradient;
      


      private void LateUpdate()
      {
         UpdateBlobColors(ref _blobColors);//wants to be a job tbh.
        
         //_jobSplat2.Complete();//force this job to hold up the thread until complete
         //also, looks like you can complete this in late update.
         //also, you only have to complete the job of last job in a chain.

         _GameFrame++;
      }

      //slow assed. Can jobify
      private void UpdateBlobColors(ref NativeArray<Color> colors)
      {
         float minVal = 0.0f;
         float maxVal = 1.0f;

         switch (DebugStyle)
         {
            case BlobColorDebugStyle.Edges:
               _jobDataDebugColorisationInt.minVal = 0;
               _jobDataDebugColorisationInt.maxVal = GooPhysics.MaxNearestNeighbours;
               _jobDataDebugColorisationInt.values = _blobEdgeCount;//based on whether knn trees are used or not. easy way to get at query results length for knn?
               
               _jobDataDebugColorisationKNNLength.minVal = 0;
               _jobDataDebugColorisationKNNLength.maxVal = GooPhysics.MaxNearestNeighbours;
               _jobDataDebugColorisationKNNLength.values = _blobKNNNearestNeighbourQueryResults;
               break;
            case BlobColorDebugStyle.Velocity:
               _jobDataDebugColorisationFloat2Magnitude.minVal = 0f;
               _jobDataDebugColorisationFloat2Magnitude.maxVal = 10f;
               
               _jobDataDebugColorisationFloat2Magnitude.values = _blobVelocities;
               break;
            case BlobColorDebugStyle.Acceleration:
               _jobDataDebugColorisationFloat2Magnitude.minVal = 0f;
               _jobDataDebugColorisationFloat2Magnitude.maxVal = 200f;
               
               _jobDataDebugColorisationFloat2Magnitude.values = _blobAccelerations;
               break;
            case BlobColorDebugStyle.TeamID:
               _jobDataDebugColorisationInt.minVal = 0;
               _jobDataDebugColorisationInt.maxVal = 5;
               _jobDataDebugColorisationInt.values = _blobTeamIDs;
               break;
            default:
               throw new ArgumentOutOfRangeException();
         }
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

         _knnContainer.Dispose();
         foreach (var result in _blobKNNNearestNeighbourQueryResults) {
            result.Dispose();
         }
         
         if(_blobPositionsV3.IsCreated) _blobPositionsV3.Dispose();
         if(_blobVelocities.IsCreated) _blobVelocities.Dispose();
         if(_blobPositions .IsCreated) _blobPositions.Dispose();
         if(_blobAccelerations.IsCreated) _blobAccelerations.Dispose();
         if(_blobTeamIDs.IsCreated) _blobTeamIDs.Dispose();
         if(_blobColors.IsCreated) _blobColors.Dispose();
         


         if (_blobEdges.IsCreated) _blobEdges.Dispose();
         if (_blobEdgeCount.IsCreated) _blobEdgeCount.Dispose();

     
      }
   }
}