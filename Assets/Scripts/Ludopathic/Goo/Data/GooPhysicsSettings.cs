using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GooPhysicsSettings", menuName = "Goo/PhysicsSettings", order = 1)]
public class GooPhysicsSettings : ScriptableObject
{
    [Range(0, 20)]
    public int MaxNearestNeighbours = 12;

 
    [Space]
    [Range(0.5f, 10.0f)]
    public float MaxSpringDistance = 4f;
    [Range(0.0f, 100.0f)]
    public float SpringForce = 15f;
    [Range(0.0f, 10.0f)]
    public float DampeningConstant = 15f;
    
    [Space]
    [Range(0.0f, 1.0f)]//stubborness
    public float LinearFriction;
    [Range(0.0f, 50.0f)]//stiffness
    public float ConstantFriction;
    
    [Space]
  
    [Range(-1.0f, 1.0f)]
    public float FluidInfluenceModulator = 0.5f;
}
