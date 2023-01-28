using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class BoidWithParallelReduction : MonoBehaviour
{
    // number of boids to spawn
    public int boidCount = 32;

    // used to populate boids, used at initialization
    public float3 boidExtent = new(32f, 32f, 32f);

    // boid properties
    public Boid.BoidConfig boidConfig;

    // compute shader file to dispatch
    public ComputeShader boidParallelReductionComputeShader;
    //
    public int prefixSumBlockSize = 32;

    // compute shader file to dispatch steering calculations
    public ComputeShader boidSteerComputeShader;

    // vfx graph assets to bind
    VisualEffect _boidVisualEffect;
    int _boidCountPoT;
    GraphicsBuffer _boidBuffer;
    GraphicsBuffer _boidPrefixSumBuffer;

    void OnEnable()
    {
        _boidCountPoT = math.ceilpow2(boidCount);
        _boidBuffer = Boid.PopulateBoids(_boidCountPoT, boidExtent);
        _boidPrefixSumBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _boidCountPoT,
            Marshal.SizeOf<Boid.BoidState>());

        InitializeBoids_Aggregate();

        InitializeBoids_Steer();

        _boidVisualEffect = GetComponent<VisualEffect>();
        _boidVisualEffect.SetGraphicsBuffer("Boids", _boidBuffer);
        _boidVisualEffect.SetUInt("BoidCount", (uint) _boidCountPoT);
        _boidVisualEffect.enabled = true;
    }

    void OnDisable()
    {
        _boidVisualEffect.enabled = false;
        _boidBuffer?.Dispose();
        _boidPrefixSumBuffer?.Dispose();
    }

    void Update()
    {
        UpdateBoids_Aggregate();
        UpdateBoids_Steer();
    }

    void InitializeBoids_Aggregate()
    {
        boidParallelReductionComputeShader.SetInt("numBoids", _boidCountPoT);
    }

    void InitializeBoids_Steer()
    {
        var kernelIndex = boidSteerComputeShader.FindKernel("CSMain");
        boidSteerComputeShader.SetBuffer(kernelIndex, "boidBuffer", _boidBuffer);
        boidSteerComputeShader.SetBuffer(kernelIndex, "boidPrefixSumBuffer", _boidPrefixSumBuffer);
        boidSteerComputeShader.SetInt("numBoids", _boidCountPoT);
    }

    void UpdateBoids_Aggregate()
    {
        var kernelIndex = boidParallelReductionComputeShader.FindKernel("CSMain");
        boidParallelReductionComputeShader.GetKernelThreadGroupSizes(kernelIndex, out var x, out var y, out var z);
        boidParallelReductionComputeShader.SetBuffer(kernelIndex, "boidBuffer", _boidBuffer);
        boidParallelReductionComputeShader.SetBuffer(kernelIndex, "boidPrefixSumBuffer", _boidPrefixSumBuffer);
        for (var n = _boidCountPoT; n >= prefixSumBlockSize; n /= prefixSumBlockSize)
        {
            boidParallelReductionComputeShader.Dispatch(kernelIndex, (int) (n / x), 1, 1);
            boidParallelReductionComputeShader.SetBuffer(kernelIndex, "boidBuffer", _boidPrefixSumBuffer);
        }
    }

    void UpdateBoids_Steer()
    {
        var boidTarget = boidConfig.boidTarget != null
            ? boidConfig.boidTarget.position
            : transform.position;
        var kernelIndex = boidSteerComputeShader.FindKernel("CSMain");
        boidSteerComputeShader.SetFloat("deltaTime", Time.deltaTime);
        boidSteerComputeShader.SetInt("numBoids", _boidCountPoT);
        boidSteerComputeShader.SetFloat("separationWeight", boidConfig.separationWeight);
        boidSteerComputeShader.SetFloat("alignmentWeight", boidConfig.alignmentWeight);
        boidSteerComputeShader.SetFloat("targetWeight", boidConfig.targetWeight);
        boidSteerComputeShader.SetFloat("moveSpeed", boidConfig.moveSpeed);
        boidSteerComputeShader.SetVector("targetPosition", boidTarget);
        boidSteerComputeShader.GetKernelThreadGroupSizes(kernelIndex, out var x, out var y, out var z);
        boidSteerComputeShader.Dispatch(kernelIndex, (int) (_boidCountPoT / x), 1, 1);
    }
}