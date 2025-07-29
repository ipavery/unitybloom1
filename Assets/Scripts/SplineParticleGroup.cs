using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SplineParticleGroup
{
    public BezierSpline spline;              // or whatever your “line” type is
    public ParticleSystem.Particle[] particles; // list of particles for this line
}