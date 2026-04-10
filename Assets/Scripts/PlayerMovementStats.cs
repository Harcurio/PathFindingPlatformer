using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using JetBrains.Annotations;
using Unity.Burst.CompilerServices;
using Unity.VisualScripting;
using UnityEditor.ShaderKeywordFilter;
using UnityEngine;


[CreateAssetMenu(menuName = "Player Movement")]
public class PlayerMovementStats : ScriptableObject
{

    [Header("walk")]
    [Range(0f, 1f)][SerializeField] float _moveThreshold = 0.25f;
    [Range(1f, 100f)][SerializeField] float _maxWalkSpeed = 12.5f;

    [Range(0.2f, 50f)][SerializeField] float _groundAcceleration = 5f;
    [Range(0.2f, 50f)][SerializeField] float _groundDeceleration = 20f;
    [Range(0.25f, 50f)][SerializeField] float _airAcceleration = 5f;
    [Range(0.2f, 50f)][SerializeField] float _airDeceleration = 5f;


    [Header("Run")]
    [Range(1f, 100f)][SerializeField] float _maxRunSpeed = 20f;

    [Header("Grounded/Collision Checks")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float _groundDetectionRayLength = 0.02f;
    [SerializeField] float _headDetectionRayLength = 0.02f;
    [Range(0f, 1f)] public float headWidth = 0.75f;


    [Header("Jump")]
    [SerializeField] float _jumpHeight = 6.5f;
    [Range(1f, 1.1f)][SerializeField] float _jumpHeightCompensationFactor = 1.054f;
    [SerializeField] float _timeTillJumpApex = 0.35f;
    [Range(0.01f, 5f)][SerializeField] float _gravityOnReleaseMultiplier = 2f;
    [SerializeField] float _maxFallSpeed = 26f;
    [Range(1, 5)][SerializeField] int _numberOfJumpsAllowed = 2;


    [Header("Jump Cut")]
    [Range(0.02f, 0.3f)][SerializeField] float _timeForUpwardsCancel = 0.027f;

    [Header("Jump Apex")]
    [Range(0.5f, 1f)][SerializeField] float _apexTheshold = 0.97f;
    [Range(0.01f, 1f)][SerializeField] float _apexHangTime = 0.075f;

    [Header("Jump Biffer")]
    [Range(0f, 1f)][SerializeField] float _jumpBufferTime = 0.125f;

    [Header("Jump Coyote Time")]
    [Range(0f, 1f)][SerializeField] float _jumpCoyoteTime = 0.1f;




    [Header("Debug")]
    public bool DebugShowIsGroundedBox;
    public bool DebugShowHeadBumpBox;



    [Header("JumpVisualization tool")]
    public bool ShowWalkJumpArc = false;
    public bool ShowRunJumpArc = false;
    public bool StopOnCollision = true;
    public bool DrawRight = true;
    [Range(5, 100)] public int ArcResolution = 20;
    [Range(0, 500)] public int VisualizationSteps = 90;



    //Jump
    public float Gravity { get; private set; }
    public float InitialJumpVelocity { get; private set; }
    public float AdjustedJumpHeight { get; private set; }


    private void OnValidate()
    {
        CalculateValues();
    }
    private void Onnable()
    {
        CalculateValues();
    }

    private void CalculateValues()
    {
        //jump
        AdjustedJumpHeight = JumpHeight * JumpHeightCompensationFactor;
        Gravity = -(2f * AdjustedJumpHeight) / Mathf.Pow(TimeTillJumpApex, 2f);
        InitialJumpVelocity = Mathf.Abs(Gravity) * TimeTillJumpApex;


    }


    public float MoveTrheshold { get => _moveThreshold; set => _moveThreshold = value; }
    public float MaxWalkSpeed { get => _maxWalkSpeed; set => _maxWalkSpeed = value; }
    public float GroundAcceleration { get => _groundAcceleration; set => _groundAcceleration = value; }
    public float GroundDeceleration { get => _groundDeceleration; set => _groundDeceleration = value; }
    public float AirAcceleration { get => _airAcceleration; set => _airAcceleration = value; }
    public float AirDeceleration { get => _airDeceleration; set => _airDeceleration = value; }


    public float MaxRunSpeed { get => _maxRunSpeed; set => _maxRunSpeed = value; }

    public LayerMask GroundLayer { get => groundLayer; set => groundLayer = value; }
    public float GroundDetectionRayLength { get => _groundDetectionRayLength; set => _groundDetectionRayLength = value; }
    public float HeadDetectionRayLength { get => _headDetectionRayLength; set => _headDetectionRayLength = value; }


    public float JumpHeight { get => _jumpHeight; set => _jumpHeight = value; }
    public float JumpHeightCompensationFactor { get => _jumpHeightCompensationFactor; set => _jumpHeightCompensationFactor = value; }
    public float TimeTillJumpApex { get => _timeTillJumpApex; set => _timeTillJumpApex = value; }
    public float GravityOnReleaseMultiplier { get => _gravityOnReleaseMultiplier; set => _gravityOnReleaseMultiplier = value; }
    public float MaxFallSpeed { get => _maxFallSpeed; set => _maxFallSpeed = value; }
    public int NumberOfJumpsAllowed { get => _numberOfJumpsAllowed; set => _numberOfJumpsAllowed = value; }


    public float TimeForUpwardsCancel { get => _timeForUpwardsCancel; set => _timeForUpwardsCancel = value; }

    public float ApexTheshold { get => _apexTheshold; set => _apexTheshold = value; }
    public float ApexHangTime { get => _apexHangTime; set => _apexHangTime = value; }

    public float JumpBufferTime { get => _jumpBufferTime; set => _jumpBufferTime = value; }

    public float JumpCoyoteTime { get => _jumpCoyoteTime; set => _jumpCoyoteTime = value; }




}
